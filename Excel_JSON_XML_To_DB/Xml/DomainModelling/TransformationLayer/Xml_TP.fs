module Xml_TP

open System

open FsToolkit.ErrorHandling    

open EmbeddedTP.EmbeddedTP

open Helpers
open XmlIntoDtm

let readDataFromXmlTP () : Result<PersonXmlIntoDtm list, string> =    
    
    try    
        //let sample = XmlProviderTP.Load @"e:\source\repos\Excel_JSON_XML_To_DB\Excel_JSON_XML_To_DB\Xml\XmlFile\xmlNightwish2013.xml"
        //let sample = XmlProviderTP.Load pathXml 

        #if DEBUG
        let sample = XmlProviderTP.Load pathXml  //for testing only

        option
            {
                let! (sample : XmlProviderTP.TabA) = sample |> Option.ofNull' 
                let! data = sample.Rows |> Option.ofNull' 
                
                return 
                    data
                    |> Array.Parallel.map 
                        (fun item 
                            -> 
                            {
                                Jmeno         = item.Jmeno |> Option.ofNullEmptySpace
                                Prijmeni      = item.Prijmeni |> Option.ofNullEmptySpace
                                RC            = item.Rc |> Option.ofNullEmptySpace |> Option.map string
                                DatumNarozeni = 
                                    match item.DatumNarozeni with  
                                    | d when d = DateTime.MinValue -> None
                                    | d                            -> Some d
                            } 
                        )
                    |> Array.toList
            }
        |> Option.toResult "Failed to read data from JSON TypeProvider — null value encountered where non-null expected."

        #else

        // No reflection-free alternative exists.
        // Runs once at startup only.
        let assembly = typeof<EmbeddedTP.EmbeddedTP.EmbeddedTPMarker>.Assembly
        
        // this will now find the resource
        use stream = assembly.GetManifestResourceStream("EmbeddedTP.Xml.xmlNightwish2013.xml")
        use reader = new System.IO.StreamReader(stream)
        let sample = XmlProviderTP.Parse(reader.ReadToEnd())     
     
        option
            {
                let! (sample : XmlProviderTP.TabA) = sample |> Option.ofNull' 
                let! data = sample.Rows |> Option.ofNull' 
                
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
                                Jmeno         = item.Jmeno |> Option.ofNullEmptySpace
                                Prijmeni      = item.Prijmeni |> Option.ofNullEmptySpace
                                RC            = rc item.Rc |> Option.map string
                                DatumNarozeni = 
                                    match item.DatumNarozeni with  
                                    | d when d = DateTime.MinValue -> None
                                    | d                            -> Some d
                            } 
                        )
                    |> Array.toList
            }
        |> Option.toResult "Failed to read data from JSON TypeProvider — null value encountered where non-null expected."

        #endif    
    with
    | ex
        -> 
        Error <| string ex.Message