module ExcelClosedXML

open ClosedXML.Excel

open Helpers
open ExcelIntoDtm

let readDataFromExcelClosedXML (filePath: string) : Result<PersonExcelIntoDtm list, string> =

    try
        use workbook = new XLWorkbook(filePath)
        let sheet = workbook.Worksheet "List1"
        let rows = sheet.RangeUsed().RowsUsed() |> Seq.skip 1  // skip header
    
        rows
        |> Seq.map
            (fun row ->
                {
                    //ClosedXML's GetString() returns "" for empty cells, not null. So Option.ofNull will give you Some "" instead of None.
                    Jmeno         = row.Cell(1).GetString() |> Option.ofNullEmptySpace
                    Prijmeni      = row.Cell(2).GetString() |> Option.ofNullEmptySpace
                    RC            = row.Cell(3).GetString() |> Option.ofNullEmptySpace
                    DatumNarozeni = 
                        let cell = row.Cell(4)
                        match cell.IsEmpty() with true -> None | false -> cell.GetDateTime() |> Some
                }
            )
        |> Seq.toList
        |> Ok

    with
    | ex
        -> 
        Error <| string ex.Message