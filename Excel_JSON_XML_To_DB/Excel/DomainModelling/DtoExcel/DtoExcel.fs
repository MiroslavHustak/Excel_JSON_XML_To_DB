module ExcelIntoDto

open System

// Excel -> DTO
//*********************************************
type PersonExcelIntoDto = 
    {
        Jmeno         : string option
        Prijmeni      : string option
        RC            : string option
        DatumNarozeni : DateTime option
    }
