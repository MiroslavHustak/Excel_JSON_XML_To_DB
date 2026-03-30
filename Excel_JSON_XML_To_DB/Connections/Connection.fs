module Connection

open Microsoft.Data.SqlClient

//Switch between the databases (always comment out the option you will not use)
   
let [<Literal>] private connStringSomee =
    @"
    "
//localhost
let [<Literal>] private connStringLocal = @"Data Source=Misa\SQLEXPRESS;Initial Catalog=nterapieLocal;Integrated Security=True"

let internal getAsyncConnection () =

    async
        {
            try          
                //let connection = new SqlConnection(connStringLocal)
                let connection = new SqlConnection(connStringSomee)
                do! connection.OpenAsync() |> Async.AwaitTask

                return Ok connection   
            with 
            | ex -> return Error <| string ex.Message
        }          

let internal closeAsyncConnection (connection: Async<Result<SqlConnection, string>>) =

    async 
        {
            match! connection with
            | Ok connection 
                ->
                try
                    do! connection.DisposeAsync().AsTask() |> Async.AwaitTask
                    return Ok ()
                with
                | ex -> return Error (string ex.Message)
            | Error err 
                ->
                return Error err
        }