module DtmJsonIntoDb

open System

// DTM -> DB
//*********************************************
type PersonDtmJsonIntoDb = 
    {
        Jmeno         : string 
        Prijmeni      : string 
        RC            : string 
        DatumNarozeni : DateTime 
    }
