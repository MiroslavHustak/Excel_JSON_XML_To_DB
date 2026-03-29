module InsertOrUpdateFromJson

open System
open System.Data
open Microsoft.Data.SqlClient

open FSharp.Control 
open FsToolkit.ErrorHandling

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
    "
    USE Natalie;
    
    MERGE TabA AS target
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
                    | true  -> Ok ()
                    | false -> Ok <| transaction.Rollback()
                with
                | :? InvalidOperationException as ex 
                    when ex.Message.Contains("completed", StringComparison.OrdinalIgnoreCase) 
                      || ex.Message.Contains("no longer usable", StringComparison.OrdinalIgnoreCase) 
                      ->
                      // Transaction was already rolled back by SQL Server — this is expected and harmless
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
                transaction.DisposeAsync().AsTask() 
                |> Async.AwaitTask
                |> Async.StartImmediate
        }

//version with cmdInsert.Parameters.Clear()
let internal insertOrUpdateAsync (persons: Result<PersonDtmJsonIntoDb list, string>) (connection: Async<Result<SqlConnection, string>>) =

    asyncResult
        {
            let! persons = persons
            let! connection = connection

            let isolationLevel = IsolationLevel.Serializable

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

                                match results |> Array.contains false with
                                | true  -> return! Error "Operation failed (rolled back)"
                                | false -> return! Ok ()
                            }
                    )
        }

//version without cmdInsert.Parameters.Clear(), but with parameters added only once and then updated with new values
let internal insertOrUpdateAsyncFailFast (persons: Result<PersonDtmJsonIntoDb list, string>) (connection: Async<Result<SqlConnection, string>>) =

    asyncResult
        {
            let! persons = persons
            let! connection = connection

            let isolationLevel = IsolationLevel.Serializable

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

                                return! Ok ()
                            }
                    )
        }

//shall be the equivalent of insertOrUpdateAsync using Async.Sequential, for educational purposes only
let internal insertOrUpdateAsyncStream (persons: Result<PersonDtmJsonIntoDb list, string>) (connection: Async<Result<SqlConnection, string>>) =

    asyncResult
        {
            let! persons = persons
            let! connection = connection

            let isolationLevel = IsolationLevel.Serializable

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
                                    |> AsyncSeq.toArrayAsync

                                match results |> Array.contains false with
                                | true  -> return! Error "Operation failed (rolled back)"
                                | false -> return! Ok ()
                            }
                    )
        }