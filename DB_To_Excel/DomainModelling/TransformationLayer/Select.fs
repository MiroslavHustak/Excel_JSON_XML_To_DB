module Select 

open System.Data
open Microsoft.Data.SqlClient

open FSharp.Control
open FsToolkit.ErrorHandling
    
open Helpers
open DbIntoDtm

let internal selectAsync (connection: Async<Result<SqlConnection, string>>) tableName = 
        
    asyncResult
        {            
            // In SQL Server, table names cannot be parameterized.
            // To prevent SQL injection, tableName shall be a trusted, hard-coded string.

            let! connection = connection

            let query = sprintf "SELECT * FROM %s" tableName
            use cmd = new SqlCommand(query, connection)
                                
            (*
            let! reader = cmd.ExecuteReaderAsync() |> Async.AwaitTask
            Async<SqlDataReader> is auto-lifted by asyncResult's Bind overload — compiles fine.
            However, the next line...
            use reader = reader
            ...is a plain synchronous 'use', NOT 'use!' — CE's Using member is never called.
            Disposal is a bare CLR try/finally, not routed through async.Using.
            Consequence: no async-aware disposal guarantee; on cancellation or Error short-circuit,
            the reader may not be disposed reliably, risking connection leaks.
            *)

            (*
            use! reader = cmd.ExecuteReaderAsync() |> Async.AwaitTask
            Type is Async<SqlDataReader>, not Async<Result<SqlDataReader, 'e>>.
            'use!' desugars to Bind(...) then Using(...), and Using requires the IDisposable value to arrive via the Result-unwrapping Bind overload.
            Without |> Async.map Ok the wrong Bind overload fires, bypassing Using entirely — disposal is not registered through async.Using, so the reader is not safely disposed.
            *)

            use! reader = 
                //cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection) //....CloseConnection helps ensure the connection is released when the reader is disposed 
                cmd.ExecuteReaderAsync()  //CloseConnection to be chosen based on the connection close strategy
                |> Async.AwaitTask 
                |> Async.map Ok

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
                                let! successfullyRead = reader.ReadAsync() |> Async.AwaitTask //The danger of use! in AsyncSeq stems from lazy evaluation of the sequence potentially outliving the disposal scope.

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

            //The actual safeguard is that AsyncSeq.toListAsync forces full materialization synchronously within the CE,  
            let! results = records |> AsyncSeq.toListAsync  // Accumulate results asynchronously

            return! Ok results                  
        }
    |> AsyncResult.catch (fun ex -> string ex.Message)