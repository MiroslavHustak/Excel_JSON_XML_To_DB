module InsertOrUpdateFromJson

open System
open System.Data
open Microsoft.Data.SqlClient

open FSharp.Control 
open FsToolkit.ErrorHandling

//*************** THIS IS A TEMPLATE ONLY, DO NOT USE IT AS IS IN PRODUCTION ***************

open DtmJsonIntoDb

(*
IF EXISTS (SELECT 1 FROM TabA WHERE RC = @RC)
    UPDATE TabA SET Jmeno = @Jmeno, Prijmeni = @Prijmeni, DatumNarozeni = @DatumNarozeni
    WHERE RC = @RC
ELSE
    INSERT INTO TabA (Jmeno, Prijmeni, RC, DatumNarozeni)
    VALUES (@Jmeno, @Prijmeni, @RC, @DatumNarozeni)
*)

let private queryInsertOrUpdate =

    // !!! Do not remove HOLDLOCK without reading the isolationLevel comments
    // in the functions below (insertOrUpdateAsync, insertOrUpdateAsyncFailFast,
    // insertOrUpdateAsyncStream). HOLDLOCK is what protects this MERGE against
    // the classic "two concurrent upserts both INSERT the same key" race
    // condition; removing it silently reopens that race even if the
    // surrounding transaction is ReadCommitted.

    "
    USE Natalie;
    
    MERGE TabA WITH (HOLDLOCK) AS target
    USING 
        (SELECT @Jmeno, @Prijmeni, @RC, @DatumNarozeni) 
        AS source (Jmeno, Prijmeni, RC, DatumNarozeni)
    ON 
        target.RC = source.RC
    WHEN MATCHED THEN
        UPDATE SET 
            Jmeno         = source.Jmeno,
            Prijmeni      = source.Prijmeni,
            DatumNarozeni = source.DatumNarozeni
    WHEN NOT MATCHED THEN
        INSERT (Jmeno, Prijmeni, RC, DatumNarozeni)
        VALUES (source.Jmeno, source.Prijmeni, source.RC, source.DatumNarozeni);
    "

let private withTransaction (connection: SqlConnection) (isolationLevel: IsolationLevel) (work: SqlTransaction -> Async<Result<'a, string>>) =

    asyncResult
        {
            let! transaction =
                connection.BeginTransactionAsync(isolationLevel).AsTask()
                |> Async.AwaitTask

            let! transaction =
                match transaction with
                | :? SqlTransaction as sqlTx -> Ok sqlTx
                | _                          -> Error "Unexpected transaction type"           

            let safeRollback () =
                try
                    match isNull transaction.Connection with
                    | true  -> Ok ()  // Connection is null = transaction already detached = nothing left to roll back — we're already in a clean (or irrecoverably failed) state
                    | false -> Ok <| transaction.Rollback()
                with
                | :? InvalidOperationException as ex 
                    when ex.Message.Contains("no longer usable", StringComparison.OrdinalIgnoreCase) 
                    // Transaction was already rolled back/committed by SQL Server — harmless
                    ->                      
                    Ok()
                | ex 
                    ->
                    Error (sprintf "Rollback failed: %s" <| string ex.Message)

            try
                try
                    let! result = work transaction

                    try
                        transaction.Commit()
                        return! Ok result
                    with
                    | ex
                        ->
                        match safeRollback() with
                        | Ok ()   -> return! Error (sprintf "Commit failed: %s" <| string ex.Message)
                        | Error e -> return! Error (sprintf "Commit failed: %s | Rollback also failed: %s" <| string ex.Message <| e)

                with
                | ex
                    ->
                    match safeRollback() with
                    | Ok ()   -> return! Error (sprintf "Transaction failed: %s" <| string ex.Message)
                    | Error e -> return! Error (sprintf "Transaction failed: %s | Rollback also failed: %s" <| string ex.Message <| e)

            finally
                //Choose your connection close strategy
                transaction.Dispose()              
        }
    |> AsyncResult.catch (fun ex -> string ex.Message)

//version with cmdInsert.Parameters.Clear()
let internal insertOrUpdateAsync (persons: Result<PersonDtmJsonIntoDb list, string>) (connection: Async<Result<SqlConnection, string>>) =

    asyncResult
        {
            let! persons = persons
            let! connection = connection
                        
            // Both options below are safe against the MERGE race condition, given the
            // query already uses `MERGE TabA WITH (HOLDLOCK)`. Pick one:
            
            // Option A: minimal overhead — relies entirely on the HOLDLOCK hint in the
            // query for correctness; the transaction itself stays at the default level.
            let isolationLevel = IsolationLevel.ReadCommitted
            
            // Option B: belt-and-suspenders — same correctness as A here, but also
            // makes the whole transaction serializable, so it's the right choice if
            // you add more statements later that also need protection from the same
            // kind of race, not just this MERGE.
            // let isolationLevel = IsolationLevel.Serializable
            // Trade-off: every read/write in the transaction pays serializable locking
            // cost, even though only the MERGE target actually needs it — higher
            // chance of blocking/deadlocks under concurrent load.

            return!
                withTransaction connection isolationLevel
                    (fun transaction
                        ->
                        asyncResult
                            {
                                use cmdInsert = new SqlCommand(queryInsertOrUpdate, connection, transaction)

                                let! results =
                                    persons
                                    |> List.map
                                        (fun item
                                            ->
                                            async
                                                {
                                                    try
                                                        cmdInsert.Parameters.Clear()

                                                        cmdInsert.Parameters.AddWithValue("@Jmeno", item.Jmeno) |> ignore<SqlParameter>
                                                        cmdInsert.Parameters.AddWithValue("@Prijmeni", item.Prijmeni) |> ignore<SqlParameter>
                                                        cmdInsert.Parameters.AddWithValue("@RC", item.RC) |> ignore<SqlParameter>

                                                        let parameterDate = SqlParameter("@DatumNarozeni", SqlDbType.Date)
                                                        parameterDate.Value <- item.DatumNarozeni
                                                        cmdInsert.Parameters.Add parameterDate |> ignore<SqlParameter>

                                                        let! affected = cmdInsert.ExecuteNonQueryAsync() |> Async.AwaitTask
                                                        return affected > 0
                                                    with
                                                    | _ -> return false
                                                }
                                        )
                                    |> Async.Sequential
                                    // IMPORTANT: cmdInsert is a single mutable SqlCommand shared across all
                                    // iterations. This is only safe because Async.Sequential guarantees strict
                                    // one-at-a-time execution — each item's ExecuteNonQueryAsync() fully
                                    // completes before the next item's Parameters.Clear() runs. Do NOT change
                                    // this to Async.Parallel (or any concurrent execution) without giving each
                                    // iteration its own SqlCommand — concurrent execution would cause parameter
                                    // values to clash across in-flight calls.

                                match results |> Array.contains false with
                                | true  -> return! Error "Operation failed (rolled back)"
                                | false -> return! Ok ()
                            }
                    )
        }
    |> AsyncResult.catch (fun ex -> string ex.Message)

//version without cmdInsert.Parameters.Clear(), but with parameters added only once and then updated with new values
let internal insertOrUpdateAsyncFailFast (persons: Result<PersonDtmJsonIntoDb list, string>) (connection: Async<Result<SqlConnection, string>>) =

    asyncResult
        {
            let! persons = persons
            let! connection = connection

            // Both options below are safe against the MERGE race condition, given the
            // query already uses `MERGE TabA WITH (HOLDLOCK)`. Pick one:
            
            // Option A: minimal overhead — relies entirely on the HOLDLOCK hint in the
            // query for correctness; the transaction itself stays at the default level.
            let isolationLevel = IsolationLevel.ReadCommitted
            
            // Option B: belt-and-suspenders — same correctness as A here, but also
            // makes the whole transaction serializable, so it's the right choice if
            // you add more statements later that also need protection from the same
            // kind of race, not just this MERGE.
            // let isolationLevel = IsolationLevel.Serializable
            // Trade-off: every read/write in the transaction pays serializable locking
            // cost, even though only the MERGE target actually needs it — higher
            // chance of blocking/deadlocks under concurrent load.

            return!
                withTransaction connection isolationLevel
                    (fun transaction
                        ->
                        asyncResult
                            {
                                use cmdInsert = new SqlCommand(queryInsertOrUpdate, connection, transaction)

                                // NOTE: parameters are added ONCE, outside the loop, and their .Value is
                                // mutated per item below — unlike insertOrUpdateAsync, which calls
                                // Parameters.Clear() and re-adds them every iteration. Either style is fine,
                                // but see the shared-cmdInsert warning further down: it applies here too.
                                cmdInsert.Parameters.Add("@Jmeno", SqlDbType.NVarChar, 100) |> ignore<SqlParameter>
                                cmdInsert.Parameters.Add("@Prijmeni", SqlDbType.NVarChar, 100) |> ignore<SqlParameter>
                                cmdInsert.Parameters.Add("@RC", SqlDbType.NVarChar, 100) |> ignore<SqlParameter>
                                
                                let paramDate = SqlParameter("@DatumNarozeni", SqlDbType.Date)
                                cmdInsert.Parameters.Add paramDate |> ignore<SqlParameter>

                                let! _ =
                                    persons
                                    |> List.map
                                        (fun item
                                            ->
                                            asyncResult
                                                {
                                                    try
                                                        cmdInsert.Parameters["@Jmeno"].Value    <- item.Jmeno
                                                        cmdInsert.Parameters["@Prijmeni"].Value <- item.Prijmeni
                                                        cmdInsert.Parameters["@RC"].Value       <- item.RC
                                                        paramDate.Value                         <- item.DatumNarozeni

                                                        let! affected =
                                                            cmdInsert.ExecuteNonQueryAsync()
                                                            |> Async.AwaitTask

                                                        match affected = 0 with
                                                        | true  -> return! Error "No rows were affected by the insert or update operation"
                                                        | false -> return ()

                                                    with
                                                    | ex -> return! Error (sprintf "Row failed: %s" <| string ex.Message)
                                                }
                                        )
                                    |> List.sequenceAsyncResultM
                                    // IMPORTANT: cmdInsert (and paramDate) are shared mutable state across all
                                    // iterations. This is only safe because List.sequenceAsyncResultM runs the
                                    // asyncResult workflows strictly one at a time, fully completing each item's
                                    // ExecuteNonQueryAsync() before the next item's parameter values are set.
                                    // Do NOT replace this with a parallel traversal (e.g. mapping then
                                    // Async.Parallel, or any "sequenceAsyncResultA"/concurrent variant) without
                                    // giving each iteration its own SqlCommand — concurrent execution would
                                    // cause parameter values to clash across in-flight calls.

                                return! Ok ()
                            }
                    )
        }
    |> AsyncResult.catch (fun ex -> string ex.Message)

//shall be the equivalent of insertOrUpdateAsync using Async.Sequential, for educational purposes only
let internal insertOrUpdateAsyncStream (persons: Result<PersonDtmJsonIntoDb list, string>) (connection: Async<Result<SqlConnection, string>>) =

    asyncResult
        {
            let! persons = persons
            let! connection = connection

            // Both options below are safe against the MERGE race condition, given the
            // query already uses `MERGE TabA WITH (HOLDLOCK)`. Pick one:
            
            // Option A: minimal overhead — relies entirely on the HOLDLOCK hint in the
            // query for correctness; the transaction itself stays at the default level.
            let isolationLevel = IsolationLevel.ReadCommitted
            
            // Option B: belt-and-suspenders — same correctness as A here, but also
            // makes the whole transaction serializable, so it's the right choice if
            // you add more statements later that also need protection from the same
            // kind of race, not just this MERGE.
            // let isolationLevel = IsolationLevel.Serializable
            // Trade-off: every read/write in the transaction pays serializable locking
            // cost, even though only the MERGE target actually needs it — higher
            // chance of blocking/deadlocks under concurrent load.

            return!
                withTransaction connection isolationLevel
                    (fun transaction
                        ->
                        asyncResult
                            {
                                use cmdInsert = new SqlCommand(queryInsertOrUpdate, connection, transaction)
                                
                                cmdInsert.Parameters.Add("@Jmeno", SqlDbType.NVarChar, 100) |> ignore<SqlParameter>
                                cmdInsert.Parameters.Add("@Prijmeni", SqlDbType.NVarChar, 100) |> ignore<SqlParameter>
                                cmdInsert.Parameters.Add("@RC", SqlDbType.NVarChar, 100) |> ignore<SqlParameter>
                                                                
                                let paramDate = SqlParameter("@DatumNarozeni", SqlDbType.Date)
                                cmdInsert.Parameters.Add paramDate |> ignore<SqlParameter>

                                let! results =
                                    persons
                                    |> List.toSeq
                                    |> AsyncSeq.ofSeq
                                    |> AsyncSeq.mapAsync
                                        (fun item
                                            ->
                                            async
                                                {
                                                    try
                                                        cmdInsert.Parameters["@Jmeno"].Value    <- item.Jmeno
                                                        cmdInsert.Parameters["@Prijmeni"].Value <- item.Prijmeni
                                                        cmdInsert.Parameters["@RC"].Value       <- item.RC
                                                        paramDate.Value                         <- item.DatumNarozeni

                                                        let! affected = cmdInsert.ExecuteNonQueryAsync() |> Async.AwaitTask
                                                        return affected > 0
                                                    with
                                                    | _ -> return false
                                                }
                                        )
                                    // IMPORTANT: cmdInsert (and paramDate) are shared mutable state across all
                                    // iterations. This is only safe because AsyncSeq.mapAsync, as used here,
                                    // processes the sequence strictly one element at a time — each item's
                                    // ExecuteNonQueryAsync() fully completes before the next item's parameter
                                    // values are set. Do NOT switch to a parallel AsyncSeq combinator (e.g.
                                    // AsyncSeq.mapAsyncParallel, if available in your FSharp.Control version)
                                    // without giving each iteration its own SqlCommand — concurrent execution
                                    // would cause parameter values to clash across in-flight calls.
                                    |> AsyncSeq.toArrayAsync

                                match results |> Array.contains false with
                                | true  -> return! Error "Operation failed (rolled back)"
                                | false -> return! Ok ()
                            }
                    )
        }
    |> AsyncResult.catch (fun ex -> string ex.Message)