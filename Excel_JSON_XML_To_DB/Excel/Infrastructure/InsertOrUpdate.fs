module InsertOrUpdateFromExcel

open System.Data
open Microsoft.Data.SqlClient

open FsToolkit.ErrorHandling

open DtmExcelIntoDb

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
                | ex -> Error <| string ex.Message

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
                    | Error e -> return! Error (sprintf "Transaction failed and rollback also failed: %s %s" <| string ex.Message <| e)

            finally
                transaction.Dispose()
        }

let internal insertOrUpdateAsync (persons: Result<PersonDtmExcelIntoDb list, string>) (connection: Async<Result<SqlConnection, string>>) =

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

let internal insertOrUpdateAsyncFailFast (persons: Result<PersonDtmExcelIntoDb list, string>) (connection: Async<Result<SqlConnection, string>>) =

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

                                let! _ =
                                    persons
                                    |> List.map
                                        (fun item
                                            ->
                                            asyncResult
                                                {
                                                    try
                                                        cmdInsert.Parameters.Clear()

                                                        cmdInsert.Parameters.AddWithValue("@Jmeno", item.Jmeno) |> ignore<SqlParameter>
                                                        cmdInsert.Parameters.AddWithValue("@Prijmeni", item.Prijmeni) |> ignore<SqlParameter>
                                                        cmdInsert.Parameters.AddWithValue("@RC", item.RC) |> ignore<SqlParameter>

                                                        let parameterDate = SqlParameter("@DatumNarozeni", SqlDbType.Date)
                                                        parameterDate.Value <- item.DatumNarozeni
                                                        cmdInsert.Parameters.Add parameterDate |> ignore<SqlParameter>

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