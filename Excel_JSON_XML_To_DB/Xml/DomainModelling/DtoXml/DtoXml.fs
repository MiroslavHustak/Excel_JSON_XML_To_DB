module XmlIntoDto

open System

// Json -> DTO
//*********************************************
type PersonXmlIntoDto = 
    {
        Jmeno         : string option
        Prijmeni      : string option
        RC            : string option
        DatumNarozeni : DateTime option
    }
