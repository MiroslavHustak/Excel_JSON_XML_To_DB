module TransformationLayerJson

open System

open Json_TP
open JsonIntoDto
open DtoJsonIntoDb

// Transformation Layer: Json TypeProvider -> DB
//*********************************************

let internal transformedListTP () : Result<PersonDtoJsonIntoDb list, string> =

    readDataFromJsonTP ()
    |> Result.map
        (List.map 
            (fun dto 
                ->
                {
                    Jmeno = dto.Jmeno |> Option.defaultValue "N/A"
                    Prijmeni = dto.Prijmeni |> Option.defaultValue "N/A"
                    RC = dto.RC |> Option.defaultValue "N/A"
                    DatumNarozeni = dto.DatumNarozeni |> Option.defaultValue (DateTime(1900, 1, 1))                      
                }
            )
        )