module TransformationLayerExcel

open System

open Excel_TP
open ExcelIntoDto
open ExcelClosedXML
open DtoExcelIntoDb

// Transformation Layer: Excel TypeProvider -> DB
//*********************************************

let internal transformedListTP () : Result<PersonDtoExcelIntoDb list, string> =

    readDataFromExcelTP ()
    |> Result.map
        (List.map 
            (fun dto 
                ->
                {
                    Jmeno = dto.Jmeno |> Option.defaultValue "N/A"
                    Prijmeni = dto.Prijmeni |> Option.defaultValue "N/A"
                    RC = dto.RC |> Option.defaultValue "N/A"
                    // TypeProvider returns MM/dd/yyyy — month first (US locale)
                    DatumNarozeni = dto.DatumNarozeni |> Option.defaultValue (DateTime(1900, 1, 1))                      
                }
            )
        )

// Transformation Layer: Excel ClosedXML -> DB
//*********************************************

let internal transformedListClosedXML fullPath : Result<PersonDtoExcelIntoDb list, string> =

    readDataFromExcelClosedXML fullPath
    |> Result.map
        (List.map 
            (fun dto 
                ->
                {
                    Jmeno    = dto.Jmeno    |> Option.defaultValue "N/A"
                    Prijmeni = dto.Prijmeni |> Option.defaultValue "N/A"
                    RC       = dto.RC       |> Option.defaultValue "N/A"
                    DatumNarozeni = dto.DatumNarozeni |> Option.defaultValue (DateTime(1900, 1, 1))
                }
            )
        )