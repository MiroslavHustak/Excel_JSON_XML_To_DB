module InsertOrUpdateFromJson

open System.Data
open Microsoft.Data.SqlClient

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

let internal insertOrUpdateAsync (persons: Result<PersonDtmJsonIntoDb list, string>) (connection: Async<Result<SqlConnection, string>>) =

    asyncResult 
        {
            let! persons = persons      
            let! connection = connection  

            try
                let isolationLevel = IsolationLevel.Serializable

                let! transaction =
                    connection.BeginTransactionAsync(isolationLevel).AsTask()
                    |> Async.AwaitTask

                let! transaction = 
                    match transaction with
                    | :? SqlTransaction 
                        as sqlTx 
                            -> Ok sqlTx
                    | _     -> Error "Unexpected transaction type"

                try
                    use cmdInsert = new SqlCommand(queryInsertOrUpdate, connection, transaction)

                    let parameterDate = SqlParameter()
                    parameterDate.ParameterName <- "@DatumNarozeni"
                    parameterDate.SqlDbType <- SqlDbType.Date                    

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
                                                
                                            //failwith "simulated failure"
                                                
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
                    | true  
                        ->
                        try 
                            transaction.Rollback() 
                        with
                        | _ -> ()

                        return! Error "One or more rows failed to insert — transaction rolled back."
                    | false 
                        ->
                        transaction.Commit()
                        return! Ok ()
                finally
                    transaction.Dispose()
            with
            | ex
                ->
                return! Error (sprintf "Transaction failed: %s" <| string ex.Message)
        }

let internal insertOrUpdateAsyncFailFast (persons: Result<PersonDtmJsonIntoDb list, string>) (connection: Async<Result<SqlConnection, string>>) =

    asyncResult 
        {
            try
                let! persons = persons
                let! connection = connection

                let isolationLevel = IsolationLevel.Serializable

                let! transaction =
                    connection.BeginTransactionAsync(isolationLevel).AsTask()
                    |> Async.AwaitTask

                let! transaction =
                    match transaction with
                    | :? SqlTransaction as sqlTx 
                        -> Ok sqlTx
                    | _ -> Error "Unexpected transaction type"

                let safeRollback () =
                    try
                        transaction.Rollback()
                    with 
                    | _ -> ()

                try
                    use cmdInsert = new SqlCommand(queryInsertOrUpdate, connection, transaction)
                    let parameterDate = SqlParameter("@DatumNarozeni", SqlDbType.Date)

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
                        |> AsyncResult.mapError (fun e -> safeRollback(); e)

                    try
                        transaction.Commit()
                        return! Ok ()
                    with 
                    | ex
                        ->
                        safeRollback()
                        return! Error (sprintf "Commit failed: %s" <| string ex.Message)

                finally
                    transaction.Dispose()

            with 
            | ex -> return! Error (sprintf "Transaction failed: %s" <| string ex.Message)
        }