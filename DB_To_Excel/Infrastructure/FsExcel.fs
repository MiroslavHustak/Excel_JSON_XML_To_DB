module ExcelClosedXML

open System

open FsExcel
open FsToolkit.ErrorHandling

open DtmDbIntoExcel

let internal writeDataIntoExcelWithFsExcel (filePath: string) (data: Async<Result<PersonDtmDbIntoExcel list, string>>) : Async<Result<unit, string>> =

    asyncResult 
        {
            let! persons = data

            let headerCells =
                [
                    Cell [ String "Jmeno" ] 
                    Cell [ String "Prijmeni" ]
                    Cell [ String "RC" ]
                    Cell [ String "Datum Narozeni"; Next NewRow ]
                ]

            let dataCells =
                persons
                |> List.collect
                    (fun person 
                        ->
                        [
                            Cell [ String person.Jmeno ]
                            Cell [ String person.Prijmeni ]
                            Cell [ String person.RC ]
                            Cell [ String (
                                              match person.DatumNarozeni with
                                              | d when d = DateTime.MinValue -> "N/A"
                                              | d                            -> d.ToString "dd.MM.yyyy"
                                          )
                                   Next NewRow 
                                 ]
                        ]
                    )

            return
                headerCells @ dataCells
                |> Render.AsFile filePath
        }