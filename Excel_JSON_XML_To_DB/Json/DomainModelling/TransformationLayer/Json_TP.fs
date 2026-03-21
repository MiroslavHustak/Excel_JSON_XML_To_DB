module Json_TP

open FsToolkit.ErrorHandling    

open EmbeddedTP.EmbeddedTP

open Helpers
open JsonIntoDto

let readDataFromJsonTP () : Result<PersonJsonIntoDto list, string> =    
    
    try    
        let sample = JsonProviderTP.Load @"e:\source\repos\Excel_JSON_XML_To_DB\Excel_JSON_XML_To_DB\Json\JsonFile\jsonNightwish2013.json"
     
        option
            {
                let! (sample : JsonProviderTP.Root) = sample |> Option.ofNull 
                let! data = sample.List |> Option.ofNull 
                
                return 
                    data
                    |> Array.Parallel.map 
                        (fun item 
                            -> 
                            {
                                Jmeno         = item.Jmeno |> Option.ofNull
                                Prijmeni      = item.Prijmeni |> Option.ofNull
                                RC            = item.Rc |> Option.ofNull |> Option.map string
                                DatumNarozeni = item.DatumNarozeni |> Option.ofNull 
                            } 
                        )
                    |> Array.toList
            }
        |> Option.toResult "Failed to read data from JSON TypeProvider — null value encountered where non-null expected."
            
    with
    | ex
        -> 
        Error <| string ex.Message