module DtmExcelIntoDb

open System

// DTM -> DB
//*********************************************
type PersonDtmExcelIntoDb = 
    {
        Jmeno         : string 
        Prijmeni      : string 
        RC            : string 
        DatumNarozeni : DateTime 
    }
