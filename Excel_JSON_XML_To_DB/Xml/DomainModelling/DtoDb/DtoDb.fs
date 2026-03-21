module DtoXmlIntoDb

open System

// DTO -> DB
//*********************************************
type PersonDtoXmlIntoDb = 
    {
        Jmeno         : string 
        Prijmeni      : string 
        RC            : string 
        DatumNarozeni : DateTime 
    }
