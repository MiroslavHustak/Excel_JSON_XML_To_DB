module DbIntoDtm

open System

// DB -> DTM
//*********************************************
type PersonDbIntoDtm = 
    {
        Jmeno         : string option
        Prijmeni      : string option
        RC            : string option
        DatumNarozeni : DateTime option
    }
