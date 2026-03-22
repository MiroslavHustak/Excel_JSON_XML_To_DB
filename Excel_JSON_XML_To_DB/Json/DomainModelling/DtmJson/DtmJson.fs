module JsonIntoDtm

open System

// Json -> DTM
//*********************************************
type PersonJsonIntoDtm = 
    {
        Jmeno         : string option
        Prijmeni      : string option
        RC            : string option
        DatumNarozeni : DateTime option
    }
