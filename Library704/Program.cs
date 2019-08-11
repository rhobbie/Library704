using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Library704
{
    internal class Module
    {
        public enum Direction { undef, input, output, bus, buswrite, and, andwrite, manualinput, testpoint, connect, notused }; /* signal directinon for a pin */
        public string Name;  /* Name of Module */
        public Dictionary<string, Pin> Pins; /* all pins of module: pins of current module, connectins pins and pins of all submodules */
        public List<Submodule> Submodules; /* All submodules, including current Module and connections pins */
        public int thismodule; /* index of current module in submodules List (for Pindefinition of current Module), or -1 if not yet defined */
        public int NumPins; /* number of pins of this module,without submodules and without connection pins */
        public string[] Signals; /* all singnals, or null if pin is not signal*/
        public Direction[] SignalDirections; /* signal directions for the elements of Signals[] */
        public int numused; /* counts how often this module is used */
        public bool Logic; /* has .Logic part */
        public Dictionary<int, Bus> Busses; /* all bussed at line of last use */
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
            public int SubIndex;  /* Index of submodule to that the pin belong */
            public int PinIndex; /* Index of the pin in the submodule */
        }
        public class Connection
        {   /* one connection in a module*/
            public Connection(string V, Pin F, Pin T, int L)
            {
                Value = V;
                From = F;
                To = T;
                Linenumber = L;

            }
            public string Value; /* Circuit element that is used for the connection,  'W' for Wire */
            public Pin From; /* Startpin */
            public Pin To; /* Endpin */
            public int Linenumber; /* Linenumber in Module file */
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
            public void SetPinNames(List<string> AllPinNames) /* set pins */
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
        public class Bus
        {
            public HashSet<string> Readpins; /* Submodule pins that Reads from the bus */
            public HashSet<string> Writepins; /* Submodule pins that Writes into the bus */
            public bool isor;

            public string interfaceread;
            public string interfacewrite;
            public int lastline;
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
                if (t <= f)
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
                        l.Add(s1 + s2 + end); /* join parts and add to final list */
            }
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
        private enum States { before_Module, Module_just_read, after_Module, after_Signals, after_Connect, after_Logic, after_End }; /* states for module loader */
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
                    rl = "0V +220V +150V +10V -100V -30V -250V +15V +40V 40RETURN +150RELAY :";
                    state = States.after_Module; /* now parse content of .Module section */
                }
                else
                {
                    /* read next text line */
                    rl = fi.ReadLine();
                    line++;
                }
                /* position of comment in line*/
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
                                if (Path.GetFileName(filename) != M.Name + ".txt") /* check if filename matches with modulename */
                                {
                                    Error(string.Format("wrong filename:{0} vs {1}", Path.GetFileName(filename), M.Name + ".txt"));
                                }
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
                                    int i;
                                    for (i = 0; s[i] != ":"; i++) /* for all pindefs */
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
                                    if (i < s.Length - 2)
                                        Error("Wrong : in Pin definitions");

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
                                        if (name == ":")
                                            Error("Wrong : in Pin definitions");
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
                            else if (s.Length == 1 && s[0] == ".Logic")
                            {
                                state = States.after_Logic;
                                M.Logic = true;
                            }
                            else
                            {
                                if (s.Length >= 2 && (s[0] == "I" || s[0] == "O" || s[0] == "B" || s[0] == "BW" || s[0] == "A" || s[0] == "AW" || s[0] == "M" || s[0] == "T" || s[0] == "C" || s[0] == "N") && M.Pins.TryGetValue(s[1], out Module.Pin su) && su.SubIndex == M.thismodule)
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
                                    else if (s[0] == "B")
                                        d = Module.Direction.bus;
                                    else if (s[0] == "BW")
                                        d = Module.Direction.buswrite;
                                    else if (s[0] == "A")
                                        d = Module.Direction.and;
                                    else if (s[0] == "AW")
                                        d = Module.Direction.andwrite;
                                    else if (s[0] == "M")
                                        d = Module.Direction.manualinput;
                                    else if (s[0] == "T")
                                        d = Module.Direction.testpoint;
                                    else if (s[0] == "C")
                                        d = Module.Direction.connect;
                                    else if (s[0] == "N")
                                        d = Module.Direction.notused;
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
                                    if ((M.Signals[T.PinIndex] == null || M.Signals[T.PinIndex] != comm))
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
                                    if (s[0] == "I" || s[0] == "O" || s[0] == "B" || s[0] == "A" || s[0] == "M" || s[0] == "T")
                                    {
                                        Error("invalid line");
                                    }
                                    if (Links.ContainsKey(s[0]))
                                    {
                                        Links[s[0]]++;
                                    }
                                    else
                                    {
                                        Links[s[0]] = 1;
                                    }
                                }
                                Module.Connection C = new Module.Connection(s[0], F, T, line);
                                M.Submodules[F.SubIndex].To[F.PinIndex].Add(C);
                                M.Submodules[T.SubIndex].From[T.PinIndex].Add(C);
                            }
                            else
                                Error("invalid Line");
                            break;
                        case States.after_Logic:
                            if (s.Length == 1 && s[0] == ".End")
                                state = States.after_End;
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

    internal class ModuleConverter : IDisposable
    {
        StreamReader fi;
        StreamWriter fo;
        private int line;
        private readonly string filename;
        public ModuleConverter(string path, string DestDir)
        {
            filename = path;
            line = 0;
            fi = new StreamReader(path);
            fo = new StreamWriter(DestDir + sconv(Path.GetFileNameWithoutExtension(path)) + ".v");
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

        private enum States { before_Module, after_Module, after_Signals, after_Connect, after_Logic, after_End }; /* states for module loader */
        private string getcommonPrefix(string[] names, int start)
        {
            int i;
            for (i = start; i < names[0].Length; i++)
            {
                char c = names[0][i];
                int j;
                for (j = 1; j < names.Length; j++)
                {
                    if (names[j][i] != c)
                        break;
                }
                if (j < names.Length)
                    break;
            }
            if (i == names[0].Length)
                i--;
            if (i == start)
            {
                return names[0].Substring(0, start) + getcommonPrefix(names, start + 1);
            }
            return names[0].Substring(0, i);
        }
        private string sconv(string s)
        {
            string r = s;
            if (char.IsDigit(s[0]))
                r = "_" + s;
            return r.Replace('-', '_');
        }
        public void Converter(Dictionary<string, Module> Modules)
        {
            States state = States.before_Module;
            Module M = null;
            bool[][] Written = null;
            fo.WriteLine("`default_nettype none");
            
            while (state != States.after_End)
            {
                if (fi.EndOfStream) /* ENd of file reached? */
                {
                    if (state != States.before_Module) /* not an empty file?  */
                        Error("unexpected End of File");
                    return;
                }
                string rl; /* current text line */
                           /* read next text line */
                rl = fi.ReadLine();
                line++;

                /* position of comment in line*/
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

                switch (state) /* in what part of the file are we? */
                {
                    case States.before_Module: /* before .Module Keyword ? */
                        if (s.Length == 2 && s[0] == ".Module")
                        {
                            fo.Write("module {0} (", sconv(s[1]));
                            if (comm != "")
                            {
                                fo.Write(" // {0}", comm);
                            }
                            fo.WriteLine();
                            M = Modules[s[1]];
#if printbusses
                            foreach (KeyValuePair<int, Module.Bus> mkv in M.Busses)
                            {
                                fo.WriteLine("// Line={0}", mkv.Key);
                                Module.Bus B = mkv.Value;
                                fo.WriteLine("// isor={0}", B.isor);
                                fo.WriteLine("// interfaceread={0}", B.interfaceread);
                                fo.WriteLine("// interfacewrite={0}", B.interfacewrite);
                                fo.Write("// Readpins=");
                                foreach(String sr in B.Readpins)
                                    fo.Write(" {0}", sr);
                                fo.WriteLine();
                                fo.Write("// Writepins=");
                                foreach (String sr in B.Writepins)
                                    fo.Write(" {0}", sr);
                                fo.WriteLine();
                            }
#endif
                            Written = new bool[M.Submodules.Count][];
                            for (int i = 0; i < M.Submodules.Count; i++)
                            {
                                Written[i] = new bool[M.Submodules[i].numpins];
                            }

                            /* search for last signal*/
                            int lastsignal = -1;
                            for (lastsignal = M.NumPins - 1; lastsignal >= 0; lastsignal--)
                            {
                                if (M.SignalDirections[lastsignal] != Module.Direction.undef
                                    && M.SignalDirections[lastsignal] != Module.Direction.testpoint
                                    && M.SignalDirections[lastsignal] != Module.Direction.manualinput
                                    && M.SignalDirections[lastsignal] != Module.Direction.connect
                                    && M.SignalDirections[lastsignal] != Module.Direction.notused)
                                    break;
                            }
                            for (int i = 0; i < M.NumPins; i++)
                            {
                                bool printsig = true;
                                bool fullbus = false;
                                switch (M.SignalDirections[i])
                                {
                                    case Module.Direction.input:
                                        fo.Write("input wire ");
                                        break;
                                    case Module.Direction.bus:
                                    case Module.Direction.and:
                                        fo.Write("input wire ");
                                        fullbus = true;
                                        break;
                                    case Module.Direction.output:
                                    case Module.Direction.buswrite:
                                    case Module.Direction.andwrite:
                                        fo.Write("output wire ");
                                        break;
                                    case Module.Direction.undef:
                                    case Module.Direction.testpoint:
                                    case Module.Direction.notused:
                                    case Module.Direction.connect:
                                    case Module.Direction.manualinput:
                                        printsig = false;
                                        break;
                                    default:
                                        Error("wrong signal direction");
                                        break;
                                }
                                if (printsig)
                                {
                                    string sig = sconv(M.Submodules[M.thismodule].PinNames[i]);
                                    if (fullbus)
                                    {
                                        fo.WriteLine("{0}_i,", sig);
                                        fo.Write("output wire ");
                                        sig = sig + "_o";
                                    }
                                    if (i < lastsignal)
                                    {
                                        if (M.Signals[i] != "")
                                            fo.WriteLine("{0}, // {1} ", sig, M.Signals[i]);
                                        else
                                            fo.WriteLine("{0},", sig);
                                    }
                                    else
                                    {
                                        if (M.Signals[i] != "")
                                            fo.WriteLine("{0} // {1} ", sig, M.Signals[i]);
                                        else
                                            fo.WriteLine("{0}", sig);
                                    }
                                }
                            }
                            fo.WriteLine(");");

                            for (int i = 1; i < M.Submodules.Count; i++)
                            {
                                if (M.Submodules[i].Name == null)
                                {
                                    Module.Submodule S = M.Submodules[i];
                                    bool prif = false;
                                    int pcnt = 0;
                                    for (int j = 0; j < S.numpins; j++)
                                    {
                                        if (!prif)
                                        {
                                            fo.Write("wire ");
                                            prif = true;
                                        }
                                        else
                                            fo.Write(",");
                                        fo.Write(sconv(S.PinNames[j]));
                                        pcnt++;
                                        if (pcnt > 15)
                                        {
                                            fo.WriteLine(";");
                                            prif = false;
                                            pcnt = 0;
                                        }
                                    }
                                    if (prif)
                                        fo.WriteLine(";");
                                }
                                else if (i != M.thismodule && M.Submodules[i].Name != null)
                                {
                                    Module.Submodule S = M.Submodules[i];
                                    string instance = getcommonPrefix(S.PinNames, 0);
                                    if (instance[instance.Length - 1] == '-'|| instance[instance.Length - 1] == '_')
                                        instance = instance.Substring(0, instance.Length - 1);
                                    Module N = Modules[S.Name];

                                    lastsignal = -1;
                                    for (lastsignal = N.NumPins - 1; lastsignal >= 0; lastsignal--)
                                    {
                                        if (N.SignalDirections[lastsignal] != Module.Direction.undef
                                    && N.SignalDirections[lastsignal] != Module.Direction.testpoint
                                    && N.SignalDirections[lastsignal] != Module.Direction.manualinput
                                    && N.SignalDirections[lastsignal] != Module.Direction.connect
                                    && N.SignalDirections[lastsignal] != Module.Direction.notused)
                                            break;
                                    }
                                    bool prif = false;
                                    int pcnt = 0;
                                    for (int j = 0; j < N.NumPins; j++)
                                    {
                                        if (N.SignalDirections[j] == Module.Direction.input ||
                                            N.SignalDirections[j] == Module.Direction.output ||
                                            N.SignalDirections[j] == Module.Direction.connect ||
                                            N.SignalDirections[j] == Module.Direction.buswrite ||
                                            N.SignalDirections[j] == Module.Direction.andwrite ||
                                            N.SignalDirections[j] == Module.Direction.bus ||
                                            N.SignalDirections[j] == Module.Direction.and)
                                        {
                                            bool fullbus = N.SignalDirections[j] == Module.Direction.bus || N.SignalDirections[j] == Module.Direction.and;

                                            if (!prif)
                                            {
                                                fo.Write("wire ");
                                                prif = true;
                                            }
                                            else
                                                fo.Write(",");
                                            fo.Write(sconv(S.PinNames[j]));
                                            if (fullbus)
                                                fo.Write("_i");
                                            pcnt++;
                                            if (pcnt > 15)
                                            {
                                                fo.WriteLine(";");
                                                prif = false;
                                                pcnt = 0;
                                            }
                                            if (fullbus)
                                            {
                                                if (!prif)
                                                {
                                                    fo.Write("wire ");
                                                    prif = true;
                                                }
                                                else
                                                    fo.Write(",");
                                                fo.Write(sconv(S.PinNames[j]));
                                                fo.Write("_o");
                                                pcnt++;
                                                if (pcnt > 15)
                                                {
                                                    fo.WriteLine(";");
                                                    prif = false;
                                                    pcnt = 0;
                                                }
                                            }
                                        }
                                    }
                                    if (prif)
                                        fo.WriteLine(";");

                                    fo.Write("{0} {1}(", sconv(S.Name), sconv(instance));
                                    pcnt = 0;
                                    for (int j = 0; j < N.NumPins; j++)
                                    {
                                        bool printsig = true;
                                        bool fullbus = false;
                                        switch (N.SignalDirections[j])
                                        {
                                            case Module.Direction.input:
                                            case Module.Direction.output:
                                            case Module.Direction.buswrite:
                                            case Module.Direction.andwrite:
                                                break;
                                            case Module.Direction.bus:
                                            case Module.Direction.and:
                                                fullbus = true;
                                                break;
                                            case Module.Direction.connect:
                                            case Module.Direction.testpoint:
                                            case Module.Direction.undef:
                                            case Module.Direction.manualinput:
                                            case Module.Direction.notused:
                                                printsig = false;
                                                break;
                                            default:
                                                Error("wrong signal direction");
                                                break;
                                        }
                                        if (printsig)
                                        {
                                            fo.Write("{0}", sconv(S.PinNames[j]));
                                            if (fullbus)
                                                fo.Write("_i,");
                                            else
                                            {
                                                if (j < lastsignal)
                                                    fo.Write(",");
                                            }
                                            pcnt++;
                                            if (pcnt > 15)
                                            {
                                                fo.WriteLine();
                                                fo.Write("    ");
                                                pcnt = 0;
                                            }
                                            if (fullbus)
                                            {
                                                fo.Write("{0}_o", sconv(S.PinNames[j]));
                                                if (j < lastsignal)
                                                    fo.Write(",");
                                                pcnt++;
                                                if (pcnt > 15)
                                                {
                                                    fo.WriteLine();
                                                    fo.Write("    ");
                                                    pcnt = 0;
                                                }
                                            }
                                    
                                        }
                                    }
                                    fo.WriteLine(");");
                                }
                            }
                            state = States.after_Module;
                        }
                        break;
                    case States.after_Module: /* after .Module Keyword */
                        if (s.Length == 1 && s[0] == ".Signals")
                        {
                            state = States.after_Signals; /* now after .Signals Keyword */
                        }
                        else
                        {

                        }
                        break;
                    case States.after_Signals:
                        if (s.Length == 1 && s[0] == ".Connect")
                            state = States.after_Connect;
                        else if (s.Length == 1 && s[0] == ".Logic")

                            state = States.after_Logic;
                        break;
                    case States.after_Connect:
                        if (s.Length == 1 && s[0] == ".End")
                        {
                            state = States.after_End;
                            fo.WriteLine("endmodule");
                        }
                        else if (s.Length == 3)
                        {
                            if (s[0] == "W" || s[0] == "R" || s[0] == "N" || s[0] == "470R" || s[0] == "510R")
                            {
                                Module.Pin P2 = M.Pins[s[2]];
                                Module.Pin P1 = M.Pins[s[1]];
                                Module.Direction d1 = Module.Direction.undef;
                                if (P1.SubIndex != 0 && M.Submodules[P1.SubIndex].Name != null)
                                {
                                    d1 = Modules[M.Submodules[P1.SubIndex].Name].SignalDirections[P1.PinIndex];
                                    if (P1.SubIndex == M.thismodule)
                                    {
                                        if (d1 == Module.Direction.input)
                                            d1 = Module.Direction.output;
                                        else if (d1 == Module.Direction.output)
                                            d1 = Module.Direction.input;
                                    }
                                }

                                Module.Direction d2 = Module.Direction.undef;
                                if (P2.SubIndex != 0 && M.Submodules[P2.SubIndex].Name != null)
                                {
                                    d2 = Modules[M.Submodules[P2.SubIndex].Name].SignalDirections[P2.PinIndex];
                                    if (P2.SubIndex == M.thismodule)
                                    {
                                        if (d2 == Module.Direction.input)
                                            d2 = Module.Direction.output;
                                        else if (d2 == Module.Direction.output)
                                            d2 = Module.Direction.input;
                                    }
                                }
                                if (d1 == Module.Direction.bus || d1 == Module.Direction.buswrite || d1 == Module.Direction.and || d1 == Module.Direction.andwrite ||
                                    d2 == Module.Direction.bus || d2 == Module.Direction.buswrite || d2 == Module.Direction.and || d2 == Module.Direction.andwrite)
                                {
                                    if(M.Busses.TryGetValue(line,out Module.Bus B))
                                    {
                                        StringBuilder w=new StringBuilder();
                                        bool first = true;
                                        if(B.Writepins.Count==0)
                                        {
                                            Error("Bus without writepins");                                        
                                        }
                                        foreach (string S in B.Writepins)
                                        {
                                            if (first)
                                                first = false;
                                            else
                                            { 
                                                if (B.isor)
                                                    w.Append("||");
                                                else
                                                    w.Append("&&");
                                            }
                                            w.Append(sconv(S));
                                        }
                                        if (B.interfacewrite != null)
                                        {
                                            fo.WriteLine("assign {0}={1};", sconv(B.interfacewrite), w.ToString());
                                            w.Clear();
                                            if (B.interfaceread != null)
                                                w.Append(sconv(B.interfaceread));
                                            else
                                                w.Append(sconv(B.interfacewrite));
                                        }
                                        if (B.Readpins.Count > 0)
                                        {

                                            string firstx = null;
                                            foreach (string x in B.Readpins)
                                            {
                                                if (firstx == null)
                                                {
                                                    firstx = sconv(x);
                                                    fo.WriteLine("assign {0}={1};", firstx, w.ToString());
                                                }
                                                else
                                                {
                                                    fo.WriteLine("assign {0}={1};", sconv(x), firstx);
                                                }
                                            }
                                        }
                                        else if(B.interfaceread!=null)
                                            Console.WriteLine("Module {0}: Error:Bus without readpins",M.Name);
                                    }
                                    if (comm != "")
                                    {
                                        fo.WriteLine(" // {0}", comm);
                                    }
                                }
                                else
                                {
                                    if (d2 == Module.Direction.output || d1 == Module.Direction.input || d1 == Module.Direction.testpoint)
                                    {
                                        Console.WriteLine("{0} {1} {2}", s[0], s[1], s[2]);
                                        Console.WriteLine("{0}:{1} - {2}:{3}", M.Submodules[P1.SubIndex].Name, d1, M.Submodules[P2.SubIndex].Name, d2);
                                        Error("Wrong signal direction");
                                    }
                                    if (d2 == Module.Direction.input || d2 == Module.Direction.testpoint)
                                    {
                                        if (Written[P2.SubIndex][P2.PinIndex])
                                            Error(String.Format("Input {0} multiple written", s[2]));
                                        Written[P2.SubIndex][P2.PinIndex] = true;
                                    }
                                    /* check direction*/
                                    if (d2 != Module.Direction.testpoint
                                        && d1 != Module.Direction.manualinput
                                        && d2 != Module.Direction.manualinput)
                                    {
                                        if (s[1] == "-30V" || s[1] == "40RETURN" || s[1] == "+150RELAY")
                                            fo.Write("assign {0}=0;", sconv(s[2]));
                                        else if (s[1] == "+10V" || s[1] == "+40V" || s[1] == "+150V")
                                            fo.Write("assign {0}=1;", sconv(s[2]));
                                        else
                                            fo.Write("assign {0}={1};", sconv(s[2]), sconv(s[1]));

                                        if (comm != "")
                                        {
                                            fo.WriteLine(" // {0}", comm);
                                        }
                                        else
                                            fo.WriteLine();
                                    }
                                }
                            }
                            else if (s[0].StartsWith("|<"))
                            {
                                if (s[1] != "+10V" && s[1] != "+15V" && s[2] != "-30V")
                                    Error("wrong diode connection");
                            }
                            else if (s[0].StartsWith(">|"))
                            {
                                if (s[1] != "-30V" && s[2] != "+10V" && s[2] != "+15V")
                                    Error("wrong diode connection");
                            }
                            else if ((s[0] == "1MEG" || s[0] == "200k" || s[0] == "220C" || s[0] == "12k") && s[2] == "0V")
                            {

                            }
                            else if ((s[0] == "2.7k" || s[0] == "1MEG") && (s[2] == "-30V" || s[1] == "-30V"))
                            {

                            }
                            else
                            {
                                Error("wrong connection");
                            }
                        }
                        else if (s.Length == 0)
                        {

                            if (comm != "")
                            {
                                fo.WriteLine("// {0}", comm);
                            }
                            else
                                fo.WriteLine();
                        }
                        break;
                    case States.after_Logic:
                        if (s.Length == 1 && s[0] == ".End")
                        {
                            state = States.after_End;
                            fo.WriteLine("endmodule");
                        }
                        else
                            fo.WriteLine(rl);
                        break;
                }
            }
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
                if (fo != null)
                {
                    fo.Dispose();
                    fo = null;
                }
            }
        }
    }
    internal class Program
    {
        private static Dictionary<string, Module> Modules;
        private static HashSet<string> GetConnectedPins(Module M, int SubIndex, int PinIndex, bool[][] VisitedPins, out int lastline)
        {
            lastline = 0;
            int l2;
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
                    if (C.Linenumber > lastline)
                        lastline = C.Linenumber;
                    HashSet<string> S2 = GetConnectedPins(M, P.SubIndex, P.PinIndex, VisitedPins, out l2);
                    if (S2 != null)
                    {
                        x.UnionWith(S2);
                        if (l2 > lastline)
                            lastline = l2;
                    }
                }

            foreach (Module.Connection C in S.To[PinIndex])
                if (C.Value == "W")
                {
                    Module.Pin P = C.To;
                    if (C.Linenumber > lastline)
                        lastline = C.Linenumber;
                    HashSet<string> S2 = GetConnectedPins(M, P.SubIndex, P.PinIndex, VisitedPins, out l2);
                    if (S2 != null)
                    {
                        x.UnionWith(S2);
                        if (l2 > lastline)
                            lastline = l2;
                    }
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

                // bool nocheck =  (Mkvp.Key.StartsWith("MF") || Mkvp.Key == "SYSTEM" || Mkvp.Key == "SP" || Mkvp.Key == "OP"); /* vorerst überspringen */
                bool nocheck = (Mkvp.Key == "SP") || (Mkvp.Value.Logic);
                bool[][] readpin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls aus dem netzwerk liest */
                bool[][] writepin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls in das netzwerk schreibt */
                bool[][] buspin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls ein wired or bus pin ist */
                bool[][] buswritepin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls ein wired or buswrite pin ist */
                bool[][] busreadpin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls ein wired or buswrite pin ist */
                bool[][] andpin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls ein wired and bus pin ist */
                bool[][] andwritepin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls ein wired andwrite bus pin ist */
                bool[][] andreadpin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls ein wired andread bus pin ist */
                bool[][] manualinputpin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls ein manual output pin ist */
                bool[][] testpointpin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls ein testpoint pin ist */
                bool[][] testpointinterfacepin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls ein testpoint pin ist */
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
                        buspin[j] = new bool[S.numpins];
                        buswritepin[j] = new bool[S.numpins];
                        busreadpin[j] = new bool[S.numpins];
                        andpin[j] = new bool[S.numpins];
                        andwritepin[j] = new bool[S.numpins];
                        andreadpin[j] = new bool[S.numpins];
                        manualinputpin[j] = new bool[S.numpins];
                        testpointpin[j] = new bool[S.numpins];
                        testpointinterfacepin[j] = new bool[S.numpins];
                        connectpin[j] = new bool[S.numpins];
                        openpin[j] = new bool[S.numpins];
                        if (!Modules.TryGetValue(S.Name, out Module M2))
                        {
                            Console.WriteLine("Module {0} not found", S.Name);
                            Environment.Exit(-1);
                        }
                        for (int i = 0; i < S.numpins && i < M2.SignalDirections.Length; i++)
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
                                if (S.To[i].Count == 0 && S.From[i].Count == 0 && M2.Signals[i] != "" && M2.SignalDirections[i] != Module.Direction.manualinput && M2.SignalDirections[i] != Module.Direction.testpoint && M2.SignalDirections[i] != Module.Direction.connect && M2.SignalDirections[i] != Module.Direction.notused)
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
                                else if (M2.SignalDirections[i] == Module.Direction.bus)
                                {
                                    buspin[j][i] = true;
                                }
                                else if (M2.SignalDirections[i] == Module.Direction.buswrite)
                                {
                                    if (M2.thismodule == j)
                                        busreadpin[j][i] = true;
                                    else
                                        buswritepin[j][i] = true;
                                }
                                else if (M2.SignalDirections[i] == Module.Direction.and)
                                {
                                    andpin[j][i] = true;
                                }
                                else if (M2.SignalDirections[i] == Module.Direction.andwrite)
                                {
                                    if (M2.thismodule == j)
                                        andreadpin[j][i] = true;
                                    else
                                        andwritepin[j][i] = true;
                                }
                                else if (M2.SignalDirections[i] == Module.Direction.manualinput)
                                {
                                    //  if (M2.thismodule == j)
                                    //    writepin[j][i] = true;
                                    //else
                                    manualinputpin[j][i] = true;
                                }
                                else if (M2.SignalDirections[i] == Module.Direction.testpoint)
                                {
                                    if (M2.thismodule == j)
                                        testpointinterfacepin[j][i] = true;
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
                                else if (M2.SignalDirections[i] == Module.Direction.notused)
                                    openpin[j][i] = true;
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
                Mkvp.Value.Busses = new Dictionary<int, Module.Bus>();
                foreach (Module.Submodule S in Mkvp.Value.Submodules)
                {

                    for (int i = 0; i < S.numpins; i++)
                    {
                        HashSet<string> H = GetConnectedPins(Mkvp.Value, j, i, VisitedPin, out int lastline); /* alle Verbindungen raussuchen*/
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
                                        if (S.Name == null)
                                        {
                                            String s = "";
                                            foreach (string e in H)
                                                s = e;
                                            if (!nocheck)
                                            {
                                                Console.WriteLine("Module {0}: Pin {1} is not connected", Mkvp.Key, s);
                                            }
                                        }
                                        else if (i < Modules[S.Name].Signals.Length && Modules[S.Name].Signals[i] != null) /* Signal ist definiert?*/
                                        {
                                            String s = "";
                                            foreach (string e in H)
                                                s = e;
                                            if (!nocheck)
                                            {
                                                Module.Pin P = Mkvp.Value.Pins[s];
                                                if (!(testpointpin[P.SubIndex] != null && testpointpin[P.SubIndex][P.PinIndex]) &&  /* testpoints und manual inputs dürfen offen sein */
                                                    !(manualinputpin[P.SubIndex] != null && manualinputpin[P.SubIndex][P.PinIndex]) &&
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
                            int numbus = 0;
                            int numbuswrite = 0;
                            int numbusread = 0;
                            int numand = 0;
                            int numandwrite = 0;
                            int numandread = 0;
                            int numtestpoint = 0;
                            int numtestpointinterface = 0;
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
                                if (buspin[P.SubIndex] != null && buspin[P.SubIndex][P.PinIndex])
                                    numbus++;
                                if (buswritepin[P.SubIndex] != null && buswritepin[P.SubIndex][P.PinIndex])
                                    numbuswrite++;
                                if (busreadpin[P.SubIndex] != null && busreadpin[P.SubIndex][P.PinIndex])
                                    numbusread++;
                                if (andpin[P.SubIndex] != null && andpin[P.SubIndex][P.PinIndex])
                                    numand++;
                                if (andwritepin[P.SubIndex] != null && andwritepin[P.SubIndex][P.PinIndex])
                                    numandwrite++;
                                if (andreadpin[P.SubIndex] != null && andreadpin[P.SubIndex][P.PinIndex])
                                    numandread++;
                                if (testpointpin[P.SubIndex] != null && testpointpin[P.SubIndex][P.PinIndex])
                                    numtestpoint++;
                                if (testpointinterfacepin[P.SubIndex] != null && testpointinterfacepin[P.SubIndex][P.PinIndex])
                                    numtestpointinterface++;
                                if (manualinputpin[P.SubIndex] != null && manualinputpin[P.SubIndex][P.PinIndex])
                                    nummanualinput++;
                                if (openpin[P.SubIndex] != null && openpin[P.SubIndex][P.PinIndex])
                                    numopen++;
                                if (pinname == "-30V")
                                    numwrite++;
                                if (pinname == "+10V")
                                    numwrite++;
                                if (pinname == "+150V")
                                    numwrite++;
                                if (pinname == "+150RELAY")
                                    numwrite++;
                                if (pinname == "+40V")
                                    numwrite++;
                                if (pinname == "40RETURN")
                                    numwrite++;
                                if (pinname == "0V")
                                    numwrite++;
                                s.Append(pinname);
                                s.Append(' ');
                            }

                            if (numwrite == 0 && numbus == 0 && numand == 0 && nummanualinput == 0 && numbuswrite == 0 && numandwrite == 0)
                            {
                                Console.WriteLine("Module {0}: Connected pins {1}have no signal source", Mkvp.Key, s.ToString());
                            }
                            if (numwrite > 1)
                            {
                                Console.WriteLine("Module {0}: Connected pins {1}have multiple signal sources", Mkvp.Key, s.ToString());
                            }
                            if (numread + numandread + numbusread +numtestpointinterface== 0 && numbus == 0 && numand == 0 && nummanualinput == 0)
                            {
                                Console.WriteLine("Module {0}: Connected pins {1}have no signal sink", Mkvp.Key, s.ToString());
                            }
                            if ((numbus > 0 || numand > 0 || numbuswrite > 0 || numandwrite > 0) && numwrite > 0)
                            {
                                Console.WriteLine("Module {0}: Connected pins {1}have source and bus pins", Mkvp.Key, s.ToString());
                            }
                            if (numbus + numbuswrite > 0 && numand + numandwrite > 0)
                            {
                                Console.WriteLine("Module {0}: Connected pins {1}have \"wired and\" and \"wired or\" bus pins", Mkvp.Key, s.ToString());
                            }
                            if (numtestpoint > 0)
                            {
                                Console.WriteLine("Module {0}: Connected pins {1}have testpoint", Mkvp.Key, s.ToString());
                            }
                            if (nummanualinput > 0 && (nummanualinput != 2 || 0 != (numread + numandread + numbusread + numtestpointinterface + numwrite + numbus + numand + numtestpoint + numbuswrite + numandwrite)))
                            {
                                Console.WriteLine("Module {0}: Connected pins {1}have manual input and something else", Mkvp.Key, s.ToString());
                            }
                            if (numopen > 0)
                            {
                                Console.WriteLine("Module {0}: Connected  pins {1}have invalid pin", Mkvp.Key, s.ToString());
                            }
                            int numallbuspins = numbuswrite + numbus + numand + numandwrite + numbusread + numandread;
                            if (numallbuspins > 0)
                            {
                                if (numwrite + numtestpoint + nummanualinput + numopen > 0)
                                {
                                    Console.WriteLine("Module {0}: bus pins {1} are wrong connected", Mkvp.Key, s.ToString());
                                }
                                else
                                {
                                    bool or = false;
                                    bool and = false;
                                    Module.Bus B = new Module.Bus
                                    {
                                        Writepins = new HashSet<string>(),
                                        Readpins = new HashSet<string>()
                                    };
                                    foreach (string pinname in H)
                                    {
                                        Module.Pin P = Mkvp.Value.Pins[pinname];

                                        String rpin = null;
                                        String wpin = null;
                                        if (readpin[P.SubIndex][P.PinIndex]  )
                                        {
                                            if (P.SubIndex == Mkvp.Value.thismodule)
                                                wpin = pinname;
                                            else
                                                rpin =  pinname;
                                        }
                                        else if (buspin[P.SubIndex][P.PinIndex])
                                        {
                                            rpin = pinname + "_i";
                                            wpin = pinname + "_o";
                                            or = true;
                                        }
                                        else if (buswritepin[P.SubIndex][P.PinIndex] || busreadpin[P.SubIndex][P.PinIndex])
                                        {
                                            wpin = pinname;
                                            or = true;
                                        }
                                        else if (andpin[P.SubIndex][P.PinIndex])
                                        {

                                            rpin = pinname + "_i";
                                            wpin = pinname + "_o";
                                            and = true;
                                        }
                                        else if (andwritepin[P.SubIndex][P.PinIndex] || andreadpin[P.SubIndex][P.PinIndex])
                                        {
                                            wpin = pinname;
                                            and = true;
                                        }
                                        else if (connectpin[P.SubIndex][P.PinIndex]||testpointinterfacepin[P.SubIndex][P.PinIndex])
                                        {

                                        }
                                        else
                                            Console.WriteLine("no pin");
                                        if (P.SubIndex == Mkvp.Value.thismodule)
                                        {
                                            if (B.interfaceread != null || B.interfacewrite != null)
                                            {
                                                Console.Write("two bus pins on  interface");
                                            }
                                            B.interfaceread = rpin;
                                            B.interfacewrite = wpin;

                                        }
                                        else
                                        {
                                            if (rpin != null)
                                                B.Readpins.Add(rpin);
                                            if (wpin != null)
                                                B.Writepins.Add(wpin);
                                        }
                                    }
                                    if (or & !and)
                                        B.isor = true;
                                    else if (and & !or)
                                        B.isor = false;
                                    else
                                        Console.WriteLine("Module {0} B not or not and",Mkvp.Key);
                                    B.lastline = lastline;
                                    if (B.interfaceread != null && B.Readpins.Count == 0)
                                    {
                                        Console.WriteLine("Module {0}: bus pins {1} have and/or bus {2} on interface wich in only written -> change to bus write", Mkvp.Key, s.ToString(), B.interfaceread.Substring(0, B.interfaceread.Length - 2));
                                    }
                                    Mkvp.Value.Busses.Add(lastline,B);
                                }
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
        private static void Traverse(Module current)
        {
            if (current.numused != 0)
                return;
            current.numused = 1;
            foreach (Module.Submodule sub in current.Submodules)
                if (sub.Name != null && sub.Name != current.Name)
                    Traverse(Modules[sub.Name]);
        }
        private static void Check4()
        {
            /* check if all modules are used */
            Traverse(Modules["SYSTEM"]);
            foreach (KeyValuePair<string, Module> kvp in Modules)
            {
                if (kvp.Value.numused == 0)
                    Console.WriteLine("Module {0} is not used", kvp.Key);
            }
        }

        private static void Main(string[] args)
        {
            SortedDictionary<string, int> Links = new SortedDictionary<string, int>(); /* debug*/
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
            Check4();

            /* convert all *.txt files from source Dir to *.v files */
            foreach (string n in Directory.GetFiles(@"..\..\", "*.txt"))
            {
                using (ModuleConverter MC = new ModuleConverter(n, @"D:\"))
                {
                    while (!MC.Eof) /* more text in file ?*/
                    {
                        /* convert next Module */
                        MC.Converter(Modules);
                    }
                }
            }
#if print
            foreach(KeyValuePair<string,int> c in Links)
            {
                Console.WriteLine("{0} {1}",c.Key,c.Value);
            }
#endif
        }
    }
}
