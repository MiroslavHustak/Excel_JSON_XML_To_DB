module Select 

open Microsoft.Data.SqlClient

open FSharp.Control
open FsToolkit.ErrorHandling
    
open Helpers
open DbIntoDtm

let internal selectAsync (connection: Async<Result<SqlConnection, string>>) tableName = 
        
    asyncResult
        { 
            let! connection = connection

            let query = sprintf "SELECT * FROM %s" tableName
            use cmd = new SqlCommand(query, connection)
            
            use! reader = 
                cmd.ExecuteReaderAsync()  
                |> Async.AwaitTask 
                |> Async.map Ok
                        
            let indexName     = reader.GetOrdinal "Jmeno"
            let indexSurname  = reader.GetOrdinal "Prijmeni"
            let indexSSN      = reader.GetOrdinal "RC"
            let indexBirthDay = reader.GetOrdinal "DatumNarozeni"

            let records =
                ()
                |> AsyncSeq.unfoldAsync 
                    (fun () 
                        -> 
                        async 
                            {
                                let! successfullyRead = reader.ReadAsync() |> Async.AwaitTask 

                                match successfullyRead with
                                | true  
                                    ->
                                    let record : PersonDbIntoDtm = 
                                        {
                                            Jmeno         = reader.GetString indexName    |> Option.ofNullEmptySpace
                                            Prijmeni      = reader.GetString indexSurname |> Option.ofNullEmptySpace
                                            RC            = reader.GetString indexSSN     |> Option.ofNullEmptySpace
                                            DatumNarozeni = match reader.IsDBNull indexBirthDay with true -> None | false -> reader.GetDateTime indexBirthDay |> Some
                                        }

                                    return Some (record, ()) 

                                | false 
                                    ->
                                    return None
                            }
                    )

            let! results = records |> AsyncSeq.toListAsync  

            return! Ok results                  
        }
    |> AsyncResult.catch (fun ex -> string ex.Message)