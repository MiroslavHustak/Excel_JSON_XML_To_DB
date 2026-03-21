module InsertOrUpdateFromExcel

open System.Data
open Microsoft.Data.SqlClient

open FsToolkit.ErrorHandling

open DtoExcelIntoDb

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

let internal insertOrUpdateAsync (persons: Result<PersonDtoExcelIntoDb list, string>) (connection: Async<Result<SqlConnection, string>>) =

    asyncResult 
        {
            let! persons = persons      
            let! connection = connection  //single let! unwraps both layers at once in case of nested Async<Result<>> — no need for nested let!s 

            try
                let isolationLevel = IsolationLevel.Serializable

                let! transaction =
                    connection.BeginTransactionAsync(isolationLevel).AsTask()
                    |> Async.AwaitTask

                let transaction = 
                    match transaction with
                    | :? SqlTransaction 
                        as sqlTx 
                            -> Ok sqlTx
                    | _     -> Error "Unexpected transaction type"

                let! transaction = transaction

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
                                            cmdInsert.Parameters.Add(parameterDate) |> ignore<SqlParameter>
                                                
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
                        transaction.Rollback()
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