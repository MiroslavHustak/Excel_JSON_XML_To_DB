module JsonIntoDto

open System

// Json -> DTO
//*********************************************
type PersonJsonIntoDto = 
    {
        Jmeno         : string option
        Prijmeni      : string option
        RC            : string option
        DatumNarozeni : DateTime option
    }
