module DtmXmlIntoDb

open System

// DTM -> DB
//*********************************************
type PersonDtmXmlIntoDb = 
    {
        Jmeno         : string 
        Prijmeni      : string 
        RC            : string 
        DatumNarozeni : DateTime 
    }
