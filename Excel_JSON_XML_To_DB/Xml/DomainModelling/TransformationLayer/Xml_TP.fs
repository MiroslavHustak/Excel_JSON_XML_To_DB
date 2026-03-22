module Xml_TP

open FsToolkit.ErrorHandling    

open EmbeddedTP.EmbeddedTP

open Helpers
open XmlIntoDto

let readDataFromXmlTP () : Result<PersonXmlIntoDto list, string> =    
    
    try    
        //let sample = XmlProviderTP.Load @"e:\source\repos\Excel_JSON_XML_To_DB\Excel_JSON_XML_To_DB\Xml\XmlFile\xmlNightwish2013.xml"
        //let sample = XmlProviderTP.Load pathXml 

        #if DEBUG
        let sample = XmlProviderTP.Load pathXml  //for testing only

        option
            {
                let! (sample : XmlProviderTP.TabA) = sample |> Option.ofNull 
                let! data = sample.Rows |> Option.ofNull 
                
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

        #else

        let assembly = typeof<EmbeddedTP.EmbeddedTP.EmbeddedTPMarker>.Assembly
        
        // this will now find the resource
        use stream = assembly.GetManifestResourceStream("EmbeddedTP.Xml.xmlNightwish2013.xml")
        use reader = new System.IO.StreamReader(stream)
        let sample = XmlProviderTP.Parse(reader.ReadToEnd())     
     
        option
            {
                let! (sample : XmlProviderTP.TabA) = sample |> Option.ofNull 
                let! data = sample.Rows |> Option.ofNull 
                
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

        #endif    
    with
    | ex
        -> 
        Error <| string ex.Message