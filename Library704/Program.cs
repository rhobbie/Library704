using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
namespace Library704
{
    internal class Module
    {
        public enum Direction { undef, input, output, linedischarge, bus, and, manualinput,testpoint,connect };
        public string Name;  /* Name of Module */
        public Dictionary<string, Pin> Pins; /* all pins of module: pins of current module, connectins pins and pins of all submodules */
        public List<Submodule> Submodules; /* All submodule, including current Module and connections pins */
        public int thismodule; /* index of current module in submodules List (for Pindefinition of current Module), or -1 if not yet defined */
        public int NumPins; /* number of pins of this module,without submodules and without connection pins */
        public string[] Signals; /* all singnals, or null if pin is not signal*/
        public Direction[] SignalDirections;
        public Module(string N)
        {
            Name = N;
            Pins = new Dictionary<string, Pin>();
            Submodules = new List<Submodule>();
            thismodule = -1;
        }
        public class Pin /* one pin of the module */
        {
            public Pin(string N, int S, int P)
            {
                Name = N;
                SubIndex = S;
                PinIndex = P;
            }
            public string Name; /* Name of the pins */
            public int SubIndex;  /* Index of submoduls to that the pin belong */
            public int PinIndex; /* Index of the pin in the submodule */
        }
        public class Connection
        {   /* eine Connection in a Module*/
            public Connection(string V, Pin F, Pin T)
            {
                Value = V;
                From = F;
                To = T;
            }
            public string Value; /* Circuit element that is used for the connection,  'W' for Wire */
            public Pin From; /* Startpin */
            public Pin To; /* Endpin */
        }
        public class Submodule
        {
            public string Name; /* Name of Submodule; or null if connection pins, or same as "Name" of current nodule if pin definition of current module */
            public int numpins; /* Number of Pins */
            public List<Connection>[] To;    /* for each pin: connections to other pins */
            public List<Connection>[] From;  /* for each pin: Connections from other pins */
            public string[] PinNames; /* for each pin: Name of pin */
            public Submodule(string S)  /* Create empty Submodule */
            {
                if (S == null || S == "")
                    Name = null;
                else
                    Name = S;
            }
            public void SetPinNames(List<string> AllPinNames) /* add pins */
            {
                numpins = AllPinNames.Count; /* store number of Pins */
                To = new List<Module.Connection>[numpins];   /* create connections */
                From = new List<Module.Connection>[numpins];   /* create connections */
                for (int i = 0; i < numpins; i++)
                {
                    To[i] = new List<Module.Connection>();
                    From[i] = new List<Module.Connection>();
                }
                PinNames = AllPinNames.ToArray();
            }
        }
    }

    internal class ModuleLoader : IDisposable
    {
        private StreamReader fi;
        private int line;
        private readonly string filename;

        public ModuleLoader(string path)
        {
            filename = path;
            fi = new StreamReader(path);
            line = 0;
        }

        private void Error(string s)
        {
            Console.WriteLine("{0},{1}:{2}", filename, line, s);
            Environment.Exit(-1);
        }
        public bool Eof
        {
            get
            {
                return fi.EndOfStream;
            }
        }

        private static string Pin_join(string pin, int num) /* join basename with number */
        {
            StringBuilder s = new StringBuilder(pin);
            if (Char.IsDigit(pin[pin.Length - 1])) /* if basename ends with number then add '-' */
                s.Append('-');
            s.Append(num.ToString()); 
            return s.ToString();
        }

        private List<string> Create_elem(string from, string to) /* replaces a range into a list by counting from 'from' to 'to'*/
        {
            List<string> l = new List<string>();
            if (from.Length == 1 && to.Length == 1 && Char.IsLetter(from[0]) && char.IsLetter(to[0])) /* Letter range */
            {
                char f = from[0];
                char t = to[0];
                if (Char.IsLower(f) != Char.IsLower(t))  /* dont mix upper with lower case*/
                    Error("invalid range");
                if (t <= f)
                    Error("invalid range");
                for (char c = f; c <= t; c++)  /* create list of letters */
                {
                    if (c != 'o' && c != 'i' && c != 'O' && c != 'I') /* skip o i O I */
                        l.Add(new string(c, 1));
                }
            }
            else
            {   /* number range */
                bool test = true;
                /* check if from and to are numbers */
                foreach (char c in from)
                    if (!Char.IsDigit(c))
                        test = false;
                foreach (char c in to)
                    if (!Char.IsDigit(c))
                        test = false;
                if (!test)
                    Error("invalid range");
                int f = int.Parse(from);
                int t = int.Parse(to);
                if(t<=f)
                    Error("invalid range");
                int minlen = 0;
                if (from[0] == '0')  /* if from has leading zero then use length of from */
                    minlen = from.Length;
                for (int i = f; i <= t; i++) /* create list of numbers */
                    l.Add(Convert.ToString(i).PadLeft(minlen, '0'));
            }
            if (l.Count < 2)
                Error("invalid range");
            return l;
        }

        private List<string> Expand(string s) /* expand pindefs*/
        {
            List<string> l = new List<string>();
            int op = s.LastIndexOf('['); /* pos of [ of last range*/

            if (op != -1)
            {
                if (op > s.Length - 5) /* enough chars for range?*/
                    Error("invalid range");
                string start = s.Substring(op + 1); /* part after [ */
                s = s.Substring(0, op);  /* part before range */
                int m = start.IndexOf('-'); /* pos of - */
                if (m == -1 || m > start.Length - 3) /* not found or not enough char after -*/
                    Error("invalid range");
                string f = start.Substring(0, m); /* extract 'from' part */
                start = start.Substring(m + 1);  /* part after - */
                int cl = start.IndexOf(']');  /* pos of } */
                if (cl == -1)  /* not found */
                    Error("invalid List");
                string t = start.Substring(0, cl); /* extract 'to' part */
                string end = start.Substring(cl + 1); /* part after ] */
                List<string> l2 = Create_elem(f, t); /* build list from 'from' to 'to' */
                List<string> l1 = Expand(s);  /* expand the other ranges (recursive call) */
                foreach (string s1 in l1) /* all */
                    foreach (string s2 in l2) /* combinations */
                        l.Add(s1 + s2 + end); /* join parts and add to final list */            }
            else
                l.Add(s); /*no expansion, only one element in list */
                    return l;
        }
        #region Module file syntax and description 
        /* 
           Each line of a module file can contain a // followed by a comment 
           The following refers to the remaining part of the line after removing of the // and the comment
           The line is split into elements seperated by one or more spaces or tabs
           Lines without elements are skipped, the following only refers the non empty lines
           
           A module file contains one or multiple module definitions or can be empty
           A module definition contains the following sequence of lines

           .Module <ModuleName>
             <List of Submodule Definitions>
           .Signals
             <List of Signal Definitions>
           .Connect
             <List of Connections>
           .End

            The line .Module <ModuleName> defines the current module name
            <ModuleName> can be a sequence of any character except space and tab.

            The <List of Submodule Definitions> contains the pindefinitions of the submodule instances that are in the current module 
            and also the pindefinition of the current module.
            and the definitions of connection pins inside of the module.
            The Submodule Definition of the current module is mandatory, the othe definitions are optional.            
            A Submodule/Pin Definition has one the following forms:
            <List of pindefs> : <Modulname>
            <List of pindefs> :
            <List of numbered pin definitions> <Modulname>
            <List of numbered pin definitions>
            If <Modulename> matches the current Module name the line defines the Pins of the current Module
            If <Modulename> does not match the current Module name then the line defines the Pins of a module instance of <Modulname> inside the module.
            If a line contains no <Modulename> the the line defines connection pins inside the module.
            The Voltage pins 0V +220V +150V +10V -100V -30V -250V +15V are automatially added to each module.

            
            The Pinnames that are used for a Module instatiation can be different of the pinnames that are used of the definition of that Module.
            The Number of pins must be the same.
            The pins are matched by position.

            A duplicate pinname inside of a moudle is not allowed.

            The <List of pindefs> or <List of numbered pin definitions> are expanded to a list of pins in the following way:

            A <List of pindefs> is a seqence of <pindef>            
            A <pindef> is a sequence of characters that contain zero or more <range>
            A <range> has one of the following form
            [<uppercaseletter>-<uppercaseletter>]
            [<lowercaseletter>-<loweercaseletter>]
            [<number>-<number>]
            A <pindef> defines one or more pins.
            The pins are created by replacing the <range> by there respective letters or numbers from the range.
            The letters 'o' 'O' 'i' 'I' are skipped during the expansion.
            leading zeros of the first element can be used to define the min length of the resulting numbers.
            If multiple <ranges> are present in a <pindef> then all possible combinations are gererated, the last <ranges> counts fastest.
            Examples
            N[1-5] is expanded to N1 N2 N3 N4 N5
            A[a-c]X is expanded to AaX AbX AcX
            [h-k]C is exüanded to hC jC kC
            X[a-b]-[1-2]d is expanded to Xa-1d Xa-2d Xb-1d Xb-2d

            A <List of numbered pin definitions> is a seqence of <numbered pin definition>
            A <numbered pin definition> contains two elements
            The second element is a number.
            A <numbered pin definition> defines one or more pins.
            The pins are generated by replacing the sencond element by a list of numbers from 1 to the value of the second element. and then placing it after the first element.
            If the first elements ends with a digit then a '-' is inserted.
            Example
            P 2 is empanded to P1 P2 
            ab0 3 is expanded ab0-1 ab0-2 ab0-3
            
            The <List of Signal Definitions> contains the definitions of the interface signals of the current modules.

            A <Signal Definition> has the following form
            <Pin Direction> <Pinname> <Pin description>

            <Pin Direction> can be 
            I  for input
            O for output
            B for a wired or bus
            LD for a line discharge pin
            A for a wired and bus
            <Pinname> must be a pin from the Pin definition of the current module from the Module section
            <Pin description> can be any text
            A duplicate Signal definition for the same pin is not allowed.
            There can be pins in the pin definitions of the moudle with out a Signal definition, but these pins cannot be used in the connect section.

            A <Connections> has the following form

            <Connection Element> <Pin from> <Pin to>
            This places the <Connection Element> between <Pin from> to <Pin to>

            <Connection Element> can be W for Wire or any other text for a non-wire element.

            <Pin from> and <Pin to> must be a pin defined in the module section.

            if <Pin from> or <Pin to> is a pin of the current module then a comment must be present in the line were the text matches the <Pin description> of the <Signal Definiton> of that pin.

            */

        #endregion
        private enum States { before_Module, Module_just_read, after_Module, after_Signals, after_Connect, after_End };
        public Module Load(SortedDictionary<string, int> Links) /* parse text and load as Module */
        {   
            /* init return value */
            Module M = null;

            States state = States.before_Module;

            while (state != States.after_End)
            {
                if (fi.EndOfStream) /* ENd of file reached? */
                {
                    if (state != States.before_Module) /* not an empty file?  */
                        Error("unexpected End of File");
                    return null;
                }
                string rl; /* current text line */
                if (state == States.Module_just_read) /* was the .Module Keyword just read? */
                {
                    /* auto add Voltage pins */
                    rl = "0V +220V +150V +10V -100V -30V -250V +15V +40V 40RETURN :";
                    state = States.after_Module; /* now parse content of .Module section */
                }
                else
                {
                    /* read next text line */
                    rl = fi.ReadLine();
                    line++;
                }
                /* position  of comment in line*/
                int ci = rl.IndexOf("//");
                /* for comment part of line */
                string comm = "";
                if (ci >= 0) /* is there a comment part in the line ? */
                {
                    /* split comment from line*/
                    comm = rl.Substring(ci + 2).Trim();
                    rl = rl.Substring(0, ci);
                }
                /* split line in parts seperated by space or tab */
                string[] s = rl.Trim().Split(new char[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                /* not an empty line? */
                if (s.Length > 0)
                    switch (state) /* in what part of the file are we? */
                    {
                        case States.before_Module: /* before .Module Keyword ? */
                            if (s.Length == 2 && s[0] == ".Module")
                            {
                                /* create new module with name */
                                M = new Module(s[1]);
                                state = States.Module_just_read; /* new state: module Keyword was just read */
                            }
                            else
                                Error("wrong .Module line");
                            break;
                        case States.after_Module: /* after .Module Keyword */
                            if (s.Length == 1 && s[0] == ".Signals")
                            {
                                /* check if Pin Definition for this Module is missing*/
                                if (M.thismodule == -1)
                                    Error("Pin definition of current module is missing");
                                state = States.after_Signals; /* now after .Signals Keyword */
                            }
                            else
                            {
                                Module.Submodule S = null; /* Current Submodule */
                                List<string> AllPinnames = new List<string>(); /* all pinnames of submodule */
                                int SubPins = 0; /* index of current pin of submodule */
                                if ((s.Length >= 3 && s[s.Length - 2] == ":") || (s.Length >= 2 && s[s.Length - 1] == ":"))
                                {
                                    /* submodule definition
                                     *  <List of pindefs> : <Modulname>
                                     *  <List of pindefs> :
                                     */
                                    /* create submodule*/
                                    S = new Module.Submodule(s[s.Length - 1] == ":" ? null : s[s.Length - 1]);

                                    /* index of this submodule in module */
                                    int SubIndex = M.Submodules.Count;                                    
                                    if (S.Name == M.Name) /* pindefinition of current module ?*/
                                    {
                                        if (M.thismodule == -1) /* not defined yet */
                                            M.thismodule = SubIndex; /* store index of pindefinition for current module */
                                        else
                                            Error("Duplicate Module Pin definitions");
                                    }
                                    for (int i = 0; s[i] != ":"; i++) /* for all pindefs */
                                    {
                                        List<string> Pinnames = Expand(s[i]); /* expand pindefs*/
                                        foreach (string Pinname in Pinnames) /* for all pins of current pindef */
                                        {
                                            if (M.Pins.ContainsKey(Pinname)) /* pin already exist in current module */
                                                Error(string.Format("Module {0}, has duplicate Pin {1}", M.Name, Pinname));
                                            M.Pins.Add(Pinname, new Module.Pin(Pinname, SubIndex, SubPins)); /* create pin and add to module*/
                                            SubPins++; /* count pins */
                                            AllPinnames.Add(Pinname); /* collect pinnames */
                                        }
                                    }
                                   
                                }
                                else
                                {
                                    /* submodule definition
                                     <List of numbered pin definitions> <Modulname>
                                     <List of numbered pin definitions>
                                     */
                                    if (s.Length % 2 == 1) /* unequal: Submodule */
                                        S = new Module.Submodule(s[s.Length - 1]);
                                    else if (s.Length >= 2) /* equal: connection */
                                        S = new Module.Submodule(null);
                                    else
                                        Error("invalid Line");

                                    /* index of this submodule in module */
                                    int SubIndex = M.Submodules.Count;                                    

                                    if (S.Name == M.Name) /* pindefinition of current module ?*/
                                    {
                                        if (M.thismodule == -1) /* not defined yet */
                                            M.thismodule = SubIndex;  /* store index of pindefinition for current module */
                                        else
                                            Error("Duplicate Module Pin definitions");
                                    }
                                    for (int i = 0; i < s.Length - 1; i += 2)  /* for all numbered pin definitions */
                                    {
                                        string name = s[i]; /* first part: basename */
                                        if (!int.TryParse(s[i + 1], out int num)) /* second element: Number of pins */
                                            Error(String.Format("Invalid Number {0}", s[i + 1]));                                        
                                        for (int j = 1; j <= num; j++) /* for all numbers */
                                        {
                                            string Pinname = Pin_join(name, j); /* join basename with number */
                                            if (M.Pins.ContainsKey(Pinname))  /* pin already exist in current module */
                                                Error(string.Format("Module {0}, has duplicate Pin {1}", M.Name, Pinname));
                                            M.Pins.Add(Pinname, new Module.Pin(Pinname, SubIndex, SubPins)); /* create pin and add to module*/
                                            SubPins++; /* count pins */
                                            AllPinnames.Add(Pinname); /* collect pinnames */
                                        }
                                    }                                    
                                }
                                S.SetPinNames(AllPinnames); /* set all pinnames of current submodule */
                                M.Submodules.Add(S);       /* add Submodule to Modul*/
                                if (S.Name == M.Name)  /* pindefinition of current module ?*/
                                {
                                    /* create empty signal definitions */
                                    M.Signals = new string[SubPins]; 
                                    M.SignalDirections = new Module.Direction[SubPins];
                                    M.NumPins = SubPins;
                                }
                            }
                            break;
                        case States.after_Signals:
                            if (s.Length == 1 && s[0] == ".Connect")
                                state = States.after_Connect;
                            else
                            {
                                if (s.Length >= 2 && (s[0] == "I" || s[0] == "O" || s[0] == "B" || s[0] == "LD" || s[0] == "A" || s[0] == "M" || s[0] == "T" || s[0] == "C") && M.Pins.TryGetValue(s[1], out Module.Pin su) && su.SubIndex == M.thismodule)
                                {
                                    int p = su.PinIndex;
                                    int i = rl.IndexOf(s[1]);
                                    if (i == -1)
                                        Error("Error");
                                    if (M.Signals[p] != null)
                                    {
                                        Error("Duplicate Signal");
                                    }
                                    M.Signals[p] = rl.Substring(i + s[1].Length).Trim();
                                    Module.Direction d = Module.Direction.undef;
                                    if (s[0] == "I")
                                        d = Module.Direction.input;
                                    else if (s[0] == "O")
                                        d = Module.Direction.output;
                                    else if (s[0] == "LD")
                                        d = Module.Direction.linedischarge;
                                    else if (s[0] == "B")
                                        d = Module.Direction.bus;
                                    else if (s[0] == "A")
                                        d = Module.Direction.and;
                                    else if (s[0] == "M")
                                        d = Module.Direction.manualinput;
                                    else if (s[0] == "T")
                                        d = Module.Direction.testpoint;
                                    else if (s[0] == "C")
                                        d = Module.Direction.connect;
                                    M.SignalDirections[p] = d;
                                }
                                else
                                {
                                    Error("Invalid Signal entry");
                                }
                            }
                            break;
                        case States.after_Connect:
                            if (s.Length == 1 && s[0] == ".End")
                                state = States.after_End;
                            else if (s.Length == 3)
                            {
                                if (!M.Pins.TryGetValue(s[1], out Module.Pin F))
                                    Error(string.Format("Unkown Pin {0}", s[1]));
                                if (!M.Pins.TryGetValue(s[2], out Module.Pin T))
                                    Error(string.Format("Unkown Pin {0}", s[2]));
                                if (T.SubIndex == M.thismodule)
                                {
                                    if ( (M.Signals[T.PinIndex] == null || M.Signals[T.PinIndex] != comm))
                                    {

                                        Error("wrong Signal");
                                    }
                                }
                                if (F.SubIndex == M.thismodule)
                                {

                                    if ((M.Signals[F.PinIndex] == null || M.Signals[F.PinIndex] != comm))
                                    {
                                        Error("wrong Signal");
                                    }

                                }
                                if (s[0] != "W")
                                {
                                    if (s[0] == "I" || s[0] == "O" || s[0] == "B" || s[0] == "A" || s[0] == "LD" || s[0] == "M" || s[0] == "T")
                                    {
                                        Error("invalid line");
                                    }
                                    if(Links.ContainsKey(s[0]))
                                    {
                                        Links[s[0]]++;
                                    }                                        
                                    else
                                    {
                                        Links[s[0]] = 1;
                                    }
                                }                                
                                Module.Connection C = new Module.Connection(s[0], F, T);
                                M.Submodules[F.SubIndex].To[F.PinIndex].Add(C);
                                M.Submodules[T.SubIndex].From[T.PinIndex].Add(C);
                            }
                            else
                                Error("invalid Line");
                            break;
                    }
            }
            return M;
        }
        // Dispose() calls Dispose(true)  
        public void Dispose()
        {
            Dispose(true);
        }
        // The bulk of the clean-up code is implemented in Dispose(bool)  
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources  
                if (fi != null)
                {
                    fi.Dispose();
                    fi = null;
                }
            }
        }
    }

    internal class Program
    {
        private static Dictionary<string, Module> Modules;

        private static HashSet<string> GetConnectedPins(Module M, int SubIndex, int PinIndex, bool[][] VisitedPins)
        {
            if (VisitedPins[SubIndex][PinIndex]) /* Schon besucht? */
                return null; /* nicht auswerten */
            VisitedPins[SubIndex][PinIndex] = true;
            HashSet<string> x = new HashSet<string>();
            Module.Submodule S = M.Submodules[SubIndex];
            x.Add(S.PinNames[PinIndex]);
            foreach (Module.Connection C in S.From[PinIndex])
                if (C.Value == "W")
                {
                    Module.Pin P = C.From;
                    HashSet<string> S2 = GetConnectedPins(M, P.SubIndex, P.PinIndex, VisitedPins);
                    if (S2 != null)
                        x.UnionWith(S2);
                }

            foreach (Module.Connection C in S.To[PinIndex])
                if (C.Value == "W")
                {
                    Module.Pin P = C.To;
                    HashSet<string> S2 = GetConnectedPins(M, P.SubIndex, P.PinIndex, VisitedPins);
                    if (S2 != null)
                        x.UnionWith(S2);
                }
            return x;
        }

        private static void Check1()
        {
            foreach (KeyValuePair<string, Module> Mkvp in Modules)
            {
                Module M = Mkvp.Value;
                foreach (Module.Submodule S in M.Submodules)
                {
                    if (S.Name != null)
                    {
                        if (Modules.TryGetValue(S.Name, out Module IM)) //Alle verwendeten submodule existieren
                        {
                            if (IM.NumPins != S.numpins) // Alle verwendeten submodule haben die korrekte pinanzahl.
                            {
                                Console.WriteLine("Module {0} has reference to Module {1} with pin number mismatch", M.Name, S.Name);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Module {0} has reference to unknown Module {1}", M.Name, S.Name);
                        }
                    }
                }
            }

        }

        private static void Check2()
        {
            /* Prüfe: */
            /* Alle signal pins sind verbunden. */
            /* Bei allen mit W verbundenen Pinguppen gibt es genau ein Write und >=1 Read. */

            foreach (KeyValuePair<string, Module> Mkvp in Modules)
            {

                bool nocheck = (Mkvp.Key.StartsWith("MF") || Mkvp.Key == "SYSTEM" || Mkvp.Key == "SP"); /* vorerst überspringen */
                
                bool[][] readpin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls aus dem netzwerk liest */
                bool[][] writepin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls in das netzwerk schreibt */
                bool[][] ldpin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls ein linedischarge pin ist */
                bool[][] buspin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls ein wired or bus pin ist */
                bool[][] andpin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls ein wired and bus pin ist */
                bool[][] manualinputpin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls ein manual output pin ist */
                bool[][] testpointpin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls ein testpoint pin ist */
                bool[][] connectpin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls ein connect pin ist */
                bool[][] openpin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls unverbunden ist */
                //* fülle readpin und writepin  */
                int j = 0;
                foreach (Module.Submodule S in Mkvp.Value.Submodules)
                {
                    if (S.Name != null)
                    {
                        readpin[j] = new bool[S.numpins];
                        writepin[j] = new bool[S.numpins];
                        ldpin[j] = new bool[S.numpins];
                        buspin[j] = new bool[S.numpins];
                        andpin[j] = new bool[S.numpins];
                        manualinputpin[j] = new bool[S.numpins];
                        testpointpin[j] = new bool[S.numpins];
                        connectpin[j] = new bool[S.numpins];
                        openpin[j] = new bool[S.numpins];
                        if (!Modules.TryGetValue(S.Name, out Module M2))
                        {
                            Console.WriteLine("Module {0} not found", S.Name);
                            Environment.Exit(-1);
                        }
                        for (int i = 0; i < S.numpins && i<M2.SignalDirections.Length; i++)
                        {
                            if (M2.SignalDirections[i] == Module.Direction.undef)
                            {
                                if (S.To[i].Count > 0 || S.From[i].Count > 0)
                                {
                                    Console.WriteLine("Module {0} Submodule {1} Pin {2} is used without Signal Definiton", Mkvp.Key, S.Name, S.PinNames[i]);
                                }
                            }
                            else
                            {
                                if (S.To[i].Count == 0 && S.From[i].Count == 0 && M2.Signals[i] != ""&&M2.SignalDirections[i]!=Module.Direction.manualinput && M2.SignalDirections[i] != Module.Direction.testpoint && M2.SignalDirections[i] != Module.Direction.connect)
                                {
                                    if (!nocheck)
                                        Console.WriteLine("Module {0}: Signal \"{1}\" of Submodule {2} is not used", Mkvp.Key, M2.Signals[i], S.Name);
                                }
                                if (M2.SignalDirections[i] == Module.Direction.input)
                                {
                                    if (M2.thismodule == j)
                                        writepin[j][i] = true;
                                    else
                                        readpin[j][i] = true;
                                }
                                else if (M2.SignalDirections[i] == Module.Direction.output)
                                {
                                    if (M2.thismodule == j)
                                        readpin[j][i] = true;
                                    else
                                        writepin[j][i] = true;
                                }
                                else if (M2.SignalDirections[i] == Module.Direction.linedischarge)
                                {
                                    ldpin[j][i] = true;
                                }
                                else if (M2.SignalDirections[i] == Module.Direction.bus)
                                {
                                    buspin[j][i] = true;
                                }
                                else if (M2.SignalDirections[i] == Module.Direction.and)
                                {
                                   andpin[j][i] = true;
                                }
                                else if (M2.SignalDirections[i] == Module.Direction.manualinput)
                                {
                                    if (M2.thismodule == j)
                                        writepin[j][i] = true;
                                    else
                                        manualinputpin[j][i] = true;
                                }
                                else if (M2.SignalDirections[i] == Module.Direction.testpoint)
                                {
                                    if (M2.thismodule == j)
                                        readpin[j][i] = true;
                                    else
                                        testpointpin[j][i] = true;
                                }
                                else if (M2.SignalDirections[i] == Module.Direction.connect)
                                {
                                    if (M2.thismodule == j)
                                        openpin[j][i] = true;
                                    else
                                        connectpin[j][i] = true;                                    
                                }
                            }
                        }
                    }
                    j++;
                }

                bool[][] VisitedPin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls schon besucht wurde*/
                j = 0;
                foreach (Module.Submodule S in Mkvp.Value.Submodules)
                {
                    VisitedPin[j] = new bool[S.numpins];
                    j++;
                } /* initialisiere Visited Pins */
                j = 0;
                foreach (Module.Submodule S in Mkvp.Value.Submodules)
                {
                    for (int i = 0; i < S.numpins; i++)
                    {
                        HashSet<string> H = GetConnectedPins(Mkvp.Value, j, i, VisitedPin); /* alle Verbindungen raussuchen*/
                        if (H == null) /* schon besucht? */
                            continue;
                        if (H.Count == 1) /* unverbundener Pin*/
                        {
                            if (j > 0)   /* kein Spannungs Pin*/
                            {

                                if (S.To[i].Count == 0 && S.From[i].Count == 0) /* auch nicht über Bauteil verbunden? */
                                {
                                    if (j == Mkvp.Value.thismodule) /* auf interface */
                                    {
                                        if (Mkvp.Value.Signals[i] != "" && Mkvp.Value.Signals[i] != null) /* Signal ist definiert?*/
                                        {
                                            String s = "";
                                            foreach (string e in H)
                                                s = e;
                                            if (!nocheck)
                                            {
                                                Module.Pin P = Mkvp.Value.Pins[s];
                                                if (!(openpin[P.SubIndex] != null && openpin[P.SubIndex][P.PinIndex]))
                                                    Console.WriteLine("Module {0}: Pin {1} is not connected", Mkvp.Key, s);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Module ms = Modules[S.Name];
                                        if (/*ms.Signals[i] != "" &&*/ ms.Signals[i] != null) /* Signal ist definiert?*/
                                        {
                                            String s = "";
                                            foreach (string e in H)
                                                s = e;
                                            if (!nocheck)
                                            {
                                                Module.Pin P = Mkvp.Value.Pins[s];
                                                
                                                    if (!(testpointpin[P.SubIndex] != null && testpointpin[P.SubIndex][P.PinIndex]) &&  /* testpoints und manual inputs dürfen offen sein */
                                                        !(manualinputpin[P.SubIndex] != null && manualinputpin[P.SubIndex][P.PinIndex])&&
                                                        !(openpin[P.SubIndex] != null && openpin[P.SubIndex][P.PinIndex]))
                                                        Console.WriteLine("Module {0}: Pin {1} of {2} is not connected", Mkvp.Key, s, S.Name);
                                            }
                                        }
                                    }
                                }
                            }

                        }
                        else
                        {
                            int numread = 0;
                            int numwrite = 0;
                            int numld = 0;
                            int numbus = 0;
                            int numand = 0;
                            int numtestpoint = 0;
                            int nummanualinput = 0;
                            int numopen = 0;
                            StringBuilder s = new StringBuilder();
                            foreach (string pinname in H)
                            {  /* zähle wieoft in dieser Pingruppe gelesen oder geschrieben wird */
                                Module.Pin P = Mkvp.Value.Pins[pinname];
                                if (readpin[P.SubIndex] != null && readpin[P.SubIndex][P.PinIndex])
                                    numread++;
                                if (writepin[P.SubIndex] != null && writepin[P.SubIndex][P.PinIndex])
                                    numwrite++;
                                if (ldpin[P.SubIndex] != null && ldpin[P.SubIndex][P.PinIndex])
                                    numld++;
                                if (buspin[P.SubIndex] != null && buspin[P.SubIndex][P.PinIndex])
                                    numbus++;
                                if (andpin[P.SubIndex] != null && andpin[P.SubIndex][P.PinIndex])
                                    numand++;
                                if (testpointpin[P.SubIndex] != null && testpointpin[P.SubIndex][P.PinIndex])
                                    numtestpoint++;
                                if (manualinputpin[P.SubIndex] != null && manualinputpin[P.SubIndex][P.PinIndex])
                                    nummanualinput++;
                                if (openpin[P.SubIndex] !=null && openpin[P.SubIndex][P.PinIndex])
                                    numopen++;
                                if (pinname == "-30V")
                                    numwrite++;
                                if (pinname == "+10V")
                                    numwrite++;
                                if (pinname == "+40V")
                                    numwrite++;
                                if (pinname == "40RETURN")
                                    numread++;
                                s.Append(pinname);
                                s.Append(' ');
                            }
                            if (numwrite == 0 && numbus == 0 && numand==0 )
                            {
                                Console.WriteLine("Module {0}: Connected pins {1}have no signal source", Mkvp.Key, s.ToString());
                            }
                            if (numwrite > 1)
                            {
                                Console.WriteLine("Module {0}: Connected pins {1}have multiple signal sources", Mkvp.Key, s.ToString());
                            }
                            if (numread == 0 && numbus == 0 && numand==0)
                            {
                                Console.WriteLine("Module {0}: Connected pins {1}have no signal sink", Mkvp.Key, s.ToString());
                            }
                            if ((numbus > 0 || numand>0 ) && numwrite > 0)
                            {
                                Console.WriteLine("Module {0}: Connected pins {1}have source and bus pins", Mkvp.Key, s.ToString());
                            }
                            if (numbus > 0 && numand > 0)
                            {
                                Console.WriteLine("Module {0}: Connected pins {1}have \"wired and\" and \"wired or\" bus pins", Mkvp.Key, s.ToString());
                            }
                            if (numld > 1)
                            {
                                Console.WriteLine("Module {0}: Connected pins {1}have multiple line dischargers", Mkvp.Key, s.ToString());
                            }
                            if(numtestpoint>0)
                            {
                                Console.WriteLine("Module {0}: Connected pins {1}have testpoint", Mkvp.Key, s.ToString());
                            }
                            if (nummanualinput > 0)
                            {
                                Console.WriteLine("Module {0}: Connected pins {1}have manual input", Mkvp.Key, s.ToString());
                            }
                            if (numopen > 0)
                            {
                                Console.WriteLine("Module {0}: Connected  pins {1}have wrong connect pin", Mkvp.Key, s.ToString());
                            }
                        }
                    }
                    j++;
                }
            }
        }

        private static void Check3()
        {
            /* check if power supply or filament power pins are used for signals on PU */
            int[] invalid_pins = new int[] { 0, 1, 2, 4, 5, 11, 15, 20, 43, 47, 52, 57, 58, 59, 62, 63 };

            foreach (KeyValuePair<string, Module> Mkvp in Modules)
            {
                if (Mkvp.Key.StartsWith("PU"))
                {

                    Module M = Mkvp.Value;
                    if (M.SignalDirections.Length < 64)
                    {
                        Console.WriteLine("{0} {1}", M.Name, M.SignalDirections.Length);
                        break;
                    }
                    foreach (int ipin in invalid_pins)
                    {
                        if (M.SignalDirections[ipin] != Module.Direction.undef)
                        {
                            Console.WriteLine("Module {0} uses invalid pin {1}-{2} for \"{3}\"", M.Name, (ipin / 8) + 1, (ipin % 8) + 1, M.Signals[ipin]);
                        }
                    }

                }
            }
        }

        private static void Main(string[] args)
        {
            SortedDictionary<string,int> Links = new SortedDictionary<string, int>(); /* debug*/
            /* Library of all Modules */
            Modules = new Dictionary<string, Module>();

            /* load all *.txt files from source Directory and add content to module library*/
            foreach (string n in Directory.GetFiles(@"..\..\", "*.txt"))
            {
                using (ModuleLoader l = new ModuleLoader(n))
                {
                    while (!l.Eof) /* more text in file ?*/
                    {
                        /* parse text and load next Module */
                        Module M = l.Load(Links);
                        if (M != null) /* no error? */
                        {
                            /* Write Module Name */
                            Console.WriteLine(M.Name);
                            if (Modules.ContainsKey(M.Name)) /* Module with same name already exists in library ?*/
                            {
                                Console.WriteLine("Duplicate Module {0}", M.Name);
                                Environment.Exit(-1);
                            }
                            /* add new Module to Library */
                            Modules.Add(M.Name, M);
                        }
                    }
                }
            }

            /* perform checks of Module library */
            Check1();
            Check2();
            Check3();
#if print
            foreach(KeyValuePair<string,int> c in Links)
            {
                Console.WriteLine("{0} {1}",c.Key,c.Value);
            }
#endif
        }

    }
}


