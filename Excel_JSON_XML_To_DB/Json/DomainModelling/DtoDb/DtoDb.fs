module DtoJsonIntoDb

open System

// DTO -> DB
//*********************************************
type PersonDtoJsonIntoDb = 
    {
        Jmeno         : string 
        Prijmeni      : string 
        RC            : string 
        DatumNarozeni : DateTime 
    }
