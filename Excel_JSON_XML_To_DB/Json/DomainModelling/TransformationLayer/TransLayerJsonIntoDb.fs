module TransformationLayerJson

open System

open Json_TP
open JsonIntoDtm
open DtmJsonIntoDb

// Transformation Layer: Json TypeProvider -> DB
//*********************************************

let internal transformedListTP () : Result<PersonDtmJsonIntoDb list, string> =

    readDataFromJsonTP ()
    |> Result.map
        (List.map 
            (fun dtm 
                ->
                {
                    Jmeno = dtm.Jmeno |> Option.defaultValue "N/A"
                    Prijmeni = dtm.Prijmeni |> Option.defaultValue "N/A"
                    RC = dtm.RC |> Option.defaultValue "N/A"
                    DatumNarozeni = dtm.DatumNarozeni |> Option.defaultValue (DateTime(1900, 1, 1))                      
                }
            )
        )