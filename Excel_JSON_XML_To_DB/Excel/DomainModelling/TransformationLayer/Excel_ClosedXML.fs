module ExcelClosedXML

open ClosedXML.Excel

open Helpers
open ExcelIntoDto

let readDataFromExcelClosedXML (filePath: string) : Result<PersonExcelIntoDto list, string> =

    try
        use workbook = new XLWorkbook(filePath)
        let sheet = workbook.Worksheet("List1")
        let rows = sheet.RangeUsed().RowsUsed() |> Seq.skip 1  // skip header
    
        rows
        |> Seq.map
            (fun row 
                ->
                {
                    Jmeno         = row.Cell(1).GetString() |> Option.ofNull
                    Prijmeni      = row.Cell(2).GetString() |> Option.ofNull
                    RC            = row.Cell(3).GetString() |> Option.ofNull
                    DatumNarozeni = row.Cell(4).GetDateTime() |> Option.ofNull
                }
            )
        |> Seq.toList
        |> Ok

    with
    | ex
        -> 
        Error <| string ex.Message