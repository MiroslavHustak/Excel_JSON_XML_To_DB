module Start

open Connection

//*********** Excel************
//open Excel_TP
//open InsertOrUpdateFromExcel
//open TransformationLayerExcel
//*****************************

//************ JSON *********** 
//open Json_TP
//open InsertOrUpdateFromJson
//open TransformationLayerJson

//*****************************

//************ XML ************
open Xml_TP
open InsertOrUpdateFromXml
open TransformationLayerXml
//*****************************

//************ Db to Excel ************

open DbToExcel
open TransformationLayerDbToExcel

//*****************************


[<EntryPoint>] 
let main argv =    

    (*
    // TEMP — remove after debugging

    printfn "STARTING"
    // list ALL loaded assemblies and their resources
    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.iter
        (fun asm 
            ->
            let resources = asm.GetManifestResourceNames()
            match resources.Length > 0 with
            | true 
                ->
                printfn "\nAssembly: %s" <| asm.GetName().Name
                resources |> Array.iter (printfn "  -> %s")
            | false
                -> ()   
        )
    *)
       
    //let fullPath = @"e:\source\repos\Excel_JSON_XML_To_DB\Excel_JSON_XML_To_DB\Excel\ExcelFile\excelNightwish2013.xlsx"
    
    //*********** Excel************
    (*
    let result1 = readDataFromExcelClosedXML fullPath
    result1 |> printfn "%A"
    
    printfn "*************************************************"

    let result2 = readDataFromExcelTP() 
    result2 |> printfn "%A"

    match result1, result2 with
    | r1, r2 
        when r1 = r2
        -> printfn "Results are the same."  
    | _ -> printfn "Results are different."   
    *)

    (*
    let result1 = transformedListClosedXML fullPath
    result1 |> printfn "%A"
    
    printfn "*************************************************"

    let result2 = transformedListTP() 
    result2 |> printfn "%A"

    match result1, result2 with
    | r1, r2 
        when r1 = r2
        -> printfn "Results are the same."  
    | _ -> printfn "Results are different."   
    *)
    //*****************************

    //*********** JSON ************

    //let result2 = readDataFromJsonTP() 
    //result2 |> printfn "%A"
    //*****************************

     //*********** XML ************

    let result3 = readDataFromXmlTP() 
    result3 |> printfn "%A"
    //*****************************
   
    //let persons = transformedListClosedXML fullPath //For an Excel file only
    let persons = transformedListTP() 

    let result =
        async 
            {
                match! getAsyncConnection() with
                | Error err 
                    ->
                    return Error err
                | Ok connection 
                    ->
                    use connection = connection 
                    return! insertOrUpdateAsyncFailFast persons (async { return Ok connection })
            }
        |> Async.RunSynchronously
    
    match result with
    | Ok _      -> printfn "\nInserting or updating successful"
    | Error err -> err |> printfn "\n%s"


    //*********** DB to Excel ************
    let result4 =
        async 
            {
                let tableName = "TabA"
                let data = getAsyncConnection >> transformAsync <| () <| tableName
                return! writeDataIntoExcelWithFsExcel @"e:\source\repos\Excel_JSON_XML_To_DB\Nightwish2013_FromDb.xlsx" data
            }
        |> Async.RunSynchronously
    
    match result4 with
    | Ok _      -> printfn "\nTransferring data from DB into Excel successful"
    | Error err -> err |> printfn "\n%s"  
    
    0   