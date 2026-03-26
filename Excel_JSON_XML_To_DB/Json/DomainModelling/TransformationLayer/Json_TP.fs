module Json_TP

open System

open FsToolkit.ErrorHandling    

open EmbeddedTP.EmbeddedTP

open Helpers
open JsonIntoDtm

let readDataFromJsonTP () : Result<PersonJsonIntoDtm list, string> =    
    
    try    
        let sample = JsonProviderTP.Load @"e:\source\repos\Excel_JSON_XML_To_DB\Excel_JSON_XML_To_DB\Json\JsonFile\jsonNightwish2013.json"
     
        option
            {
                let! (sample : JsonProviderTP.Root) = sample |> Option.ofNull' 
                let! data = sample.List |> Option.ofNull' 

                let rc item =
                    match item with 
                    | 0L -> 
                        None
                    | rc -> 
                        let s = string rc
                        Some (s.PadLeft(9, '0'))
                
                return 
                    data
                    |> Array.Parallel.map 
                        (fun item 
                            -> 
                            {
                                Jmeno         = item.Jmeno    |> Option.ofNullEmptySpace
                                Prijmeni      = item.Prijmeni |> Option.ofNullEmptySpace
                                RC            = rc item.Rc    |> Option.map string
                                DatumNarozeni = 
                                    match item.DatumNarozeni with  
                                    | d when d = DateTime.MinValue -> None
                                    | d                            -> Some d
                            } 
                        )
                    |> Array.toList
            }
        |> Option.toResult "Failed to read data from JSON TypeProvider — null value encountered where non-null expected."
            
    with
    | ex
        -> 
        Error <| string ex.Message