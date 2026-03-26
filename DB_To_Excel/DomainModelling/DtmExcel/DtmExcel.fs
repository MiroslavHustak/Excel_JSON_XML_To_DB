module DtmDbIntoExcel

open System

// DTM -> Excel
//*********************************************
type PersonDtmDbIntoExcel = 
    {
        Jmeno         : string 
        Prijmeni      : string 
        RC            : string 
        DatumNarozeni : DateTime 
    }
