module DtoExcelIntoDb

open System

// DTO -> DB
//*********************************************
type PersonDtoExcelIntoDb = 
    {
        Jmeno         : string 
        Prijmeni      : string 
        RC            : string 
        DatumNarozeni : DateTime 
    }
