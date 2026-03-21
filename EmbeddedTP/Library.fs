namespace EmbeddedTP

open System
open FSharp.Data

module EmbeddedTP =

    let [<Literal>] ResolutionFolder = __SOURCE_DIRECTORY__
      
    type XmlProviderTP =
        XmlProvider<"Xml/xmlNightwish2013.xml", EmbeddedResource = "EmbeddedTP, EmbeddedTP.Xml.xmlNightwish2013.xml", ResolutionFolder = ResolutionFolder>
    
    type JsonProviderTP =
        JsonProvider<"Json/jsonNightwish2013.json", EmbeddedResource = "EmbeddedTP, EmbeddedTP.Json.jsonNightwish2013.json", ResolutionFolder = ResolutionFolder>

    let pathXml = 
        try
            System.IO.Path.Combine(ResolutionFolder, @"Xml/xmlNightwish2013.xml")
        with
        |_ -> String.Empty

    let pathJson = 
        try
            System.IO.Path.Combine(ResolutionFolder, @"Json/jsonNightwish2013.json")
        with
        |_ -> String.Empty