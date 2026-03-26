module TransformationLayerDbToExcel

open System
open Microsoft.Data.SqlClient

open FsToolkit.ErrorHandling

open Select
open DbIntoDtm
open DtmDbIntoExcel

// Transformation Layer: DB -> Excel
//*********************************************

let private transformPersonDbToDtmExcel (record: PersonDbIntoDtm) : PersonDtmDbIntoExcel =
    {
        Jmeno         = record.Jmeno         |> Option.defaultValue "N/A"
        Prijmeni      = record.Prijmeni      |> Option.defaultValue "N/A"
        RC            = record.RC            |> Option.defaultValue "N/A"
        DatumNarozeni = record.DatumNarozeni |> Option.defaultValue DateTime.MinValue
    }

let private transformListDbToDtmExcel (records: PersonDbIntoDtm list) : PersonDtmDbIntoExcel list =
    records
    |> List.map transformPersonDbToDtmExcel

let internal transformAsync (connection: Async<Result<SqlConnection, string>>) tableName : Async<Result<PersonDtmDbIntoExcel list, string>> =
    
    asyncResult 
        {
            let! records = selectAsync connection tableName
            return transformListDbToDtmExcel records
        }