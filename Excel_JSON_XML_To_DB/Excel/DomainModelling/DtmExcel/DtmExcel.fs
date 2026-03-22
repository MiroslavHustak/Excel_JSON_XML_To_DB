module ExcelIntoDtm

open System

// Excel -> DTM
//*********************************************
type PersonExcelIntoDtm = 
    {
        Jmeno         : string option
        Prijmeni      : string option
        RC            : string option
        DatumNarozeni : DateTime option
    }
