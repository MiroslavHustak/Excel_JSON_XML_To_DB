module Select 

open Microsoft.Data.SqlClient

open FSharp.Control
open FsToolkit.ErrorHandling
    
open Helpers
open DbIntoDtm

let internal selectAsync (connection: Async<Result<SqlConnection, string>>) tableName = 
        
    asyncResult
        {
            try
                // In SQL Server, table names cannot be parameterized.
                // To prevent SQL injection, tableName shall be a trusted, hard-coded string.

                let! connection = connection

                let query = sprintf "SELECT * FROM %s" tableName
                use cmd = new SqlCommand(query, connection)
                                
                // let! reader = cmd.ExecuteReaderAsync() |> Async.AwaitTask // Pay attention to why it is so (not use! reader = ... no disposal guarantee)
                // use reader = reader // Pay attention to why it is so 

                use! reader = cmd.ExecuteReaderAsync() |> Async.AwaitTask |> Async.map Ok // Pay attention to why it is so 

                (*
                cmd.ExecuteReaderAsync()          // Task<SqlDataReader>
                |> Async.AwaitTask                // Async<SqlDataReader>
                |> Async.map Ok                   // Async<Result<SqlDataReader, string>>
                *)
                                                      
                let indexName     = reader.GetOrdinal "Jmeno"
                let indexSurname  = reader.GetOrdinal "Prijmeni"
                let indexSSN      = reader.GetOrdinal "RC"
                let indexBirthDay = reader.GetOrdinal "DatumNarozeni"

                let records =
                    ()
                    |> AsyncSeq.unfoldAsync  // Generator is repeatedly called until it returns None.
                        (fun () 
                            -> 
                            async 
                                {
                                    let! successfullyRead = reader.ReadAsync() |> Async.AwaitTask //use! supposedly to behave improperly in the AsyncSeq loop

                                    match successfullyRead with
                                    | true  
                                        ->
                                        //reader.GetString returns "" for empty DB strings, not null
                                        let record : PersonDbIntoDtm = //DateTime is a struct, so it cannot be null. 
                                            {
                                                Jmeno         = reader.GetString indexName    |> Option.ofNullEmptySpace
                                                Prijmeni      = reader.GetString indexSurname |> Option.ofNullEmptySpace
                                                RC            = reader.GetString indexSSN     |> Option.ofNullEmptySpace
                                                DatumNarozeni = match reader.IsDBNull indexBirthDay with true -> None | false -> reader.GetDateTime indexBirthDay |> Some
                                            }

                                        return Some (record, ())  // (value, next_state)

                                    | false 
                                        ->
                                        return None
                                }
                        )

                let! results = records |> AsyncSeq.toListAsync  // Accumulate results asynchronously

                return! Ok results       
                    
            with
            | ex -> return! Error <| string ex.Message
        }