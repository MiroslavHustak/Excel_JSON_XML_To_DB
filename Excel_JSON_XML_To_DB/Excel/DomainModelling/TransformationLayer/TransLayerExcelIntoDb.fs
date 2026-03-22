module TransformationLayerExcel

open System

open Excel_TP
open ExcelIntoDtm
open ExcelClosedXML
open DtmExcelIntoDb

// Transformation Layer: Excel TypeProvider -> DB
//*********************************************

let internal transformedListTP () : Result<PersonDtmExcelIntoDb list, string> =

    readDataFromExcelTP ()
    |> Result.map
        (List.map 
            (fun dtm 
                ->
                {
                    Jmeno = dtm.Jmeno |> Option.defaultValue "N/A"
                    Prijmeni = dtm.Prijmeni |> Option.defaultValue "N/A"
                    RC = dtm.RC |> Option.defaultValue "N/A"
                    // TypeProvider returns MM/dd/yyyy — month first (US locale)
                    DatumNarozeni = dtm.DatumNarozeni |> Option.defaultValue (DateTime(1900, 1, 1))                      
                }
            )
        )

// Transformation Layer: Excel ClosedXML -> DB
//*********************************************

let internal transformedListClosedXML fullPath : Result<PersonDtmExcelIntoDb list, string> =

    readDataFromExcelClosedXML fullPath
    |> Result.map
        (List.map 
            (fun dtm 
                ->
                {
                    Jmeno    = dtm.Jmeno    |> Option.defaultValue "N/A"
                    Prijmeni = dtm.Prijmeni |> Option.defaultValue "N/A"
                    RC       = dtm.RC       |> Option.defaultValue "N/A"
                    DatumNarozeni = dtm.DatumNarozeni |> Option.defaultValue (DateTime(1900, 1, 1))
                }
            )
        )