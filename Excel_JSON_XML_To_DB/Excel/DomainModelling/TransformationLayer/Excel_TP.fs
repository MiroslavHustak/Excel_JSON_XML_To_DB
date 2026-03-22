module Excel_TP

open FSharp.Interop.Excel

open Helpers
open ExcelIntoDtm

// Unfortunately, ExcelProvider uses the sample file as both schema and runtime source.
// Unlike JSON/XML type providers, there is no way to load a different file at runtime.

// Formard slash is used in the path, because ExcelProvider doesn't work with backslashes in the path (even if they are escaped).
type ExcelProviderTP = ExcelFile<"e:/source/repos/Excel_JSON_XML_To_DB/Excel_JSON_XML_To_DB/Excel/ExcelFile/excelNightwish2013.xlsx", "List1">
   
let readDataFromExcelTP () : Result<PersonExcelIntoDtm list, string> =    
    
    try    
        let file = new ExcelProviderTP()
        let rows = file.Data |> Seq.toArray
        let rowCount = rows.Length
        let listRange = [ 0 .. rowCount - 1 ]

        let result = 
            // tail-recursive
            let rec loop list acc =  
                match list with 
                | [] 
                    -> 
                    List.rev acc
                | i :: tail
                    ->
                    let person = 
                        {
                            Jmeno         = rows.[i].Jmeno |> Option.ofNull
                            Prijmeni      = rows.[i].Prijmeni |> Option.ofNull
                            RC            = rows.[i].RC |> Option.ofNull |> Option.map string
                            DatumNarozeni = rows.[i].DatumNarozeni |> Option.ofNull //TypeProvider returns MM/dd/yyyy — month first (US locale)
                        }

                    loop tail (person :: acc)

            loop listRange []

        Ok result 
    with
    | ex
        -> 
        Error <| string ex.Message