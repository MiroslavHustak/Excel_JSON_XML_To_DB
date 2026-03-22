module XmlIntoDtm

open System

// Json -> DTM
//*********************************************
type PersonXmlIntoDtm = 
    {
        Jmeno         : string option
        Prijmeni      : string option
        RC            : string option
        DatumNarozeni : DateTime option
    }
