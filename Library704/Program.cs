
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Library704
{

    class Module
    {
        public enum direction { undef, input, output };
        public string Name;  /* Name des Moduls */
        public Dictionary<string, Pin> Pins; /* alle Pins des Moduls  */
        public List<Submodule> Submodules; /* Alle Submodule */
        public int thismodule; /* index in Submodules für Pindefinition des Modus */
        public int NumPins; /* Anzahl Pins des Moduls */
        public string[] Signals;
        public bool SignalsDefined;
        public direction[] SignalDirections;
        public Module(string N)
        {
            Name = N;
            Pins = new Dictionary<string, Pin>();
            Submodules = new List<Submodule>();
            thismodule = -1;
            SignalsDefined = false;
        }
        public class Pin /* Ein Pin des Moduls */
        {
            public Pin(string N, int S, int P)
            {
                Name = N;
                SubIndex = S;
                PinIndex = P;
            }
            public string Name; /* Name des Pins */
            public int SubIndex;  /* Index des Submoduls */
            public int PinIndex; /* Index des Pins innerhalb des Submodul */
        }
        public class Connection
        {   /* eine Verbindung des Moduls*/
            public Connection(string V, Pin F, Pin T)
            {
                Value = V;
                From = F;
                To = T;
            }
            public string Value;
            public Pin From;
            public Pin To;
        }
        public class Submodule
        {
            public string Name; /* Name des Submoduls;*/
            public string Param; /* Parameter des Submoduls;*/
            public int numpins; /* anzahl Pins des Submoduls*/
            public List<Connection>[] To;    /* Verbindungen zu anderen pins */
            public List<Connection>[] From;  /* Verbindungen von anderen pins */
            public string[] PinNames;
            public Submodule(string S)
            {
                if (S == null || S == "")
                {
                    Name = null;
                    Param = null;
                }
                else
                {
                    int i = S.IndexOf('(');
                    int j = S.IndexOf(')');
                    if (i != -1 && j == S.Length - 1)
                    {
                        Name = S.Substring(0, i);
                        Param = S.Substring(i, S.Length - i);
                    }
                    else
                    {
                        Name = S;
                        Param = null;
                    }
                }
            }
            public void SetPinNames(List<string> AllPinNames)
            {
                numpins = AllPinNames.Count; /* Anzahl Pins speichern */
                To = new List<Module.Connection>[numpins];   /* connections anlegen */
                From = new List<Module.Connection>[numpins];   /* connections anlegen */
                for (int i = 0; i < numpins; i++)
                {
                    To[i] = new List<Module.Connection>();
                    From[i] = new List<Module.Connection>();
                }
                PinNames = AllPinNames.ToArray();
            }
        }
    }

    class Loader704 : IDisposable
    {
        StreamReader fi;
        int line;
        string filename;

        public Loader704(string path)
        {
            filename = path;
            fi = new StreamReader(path);
            line = 0;
        }
        void Error(string s)
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
        static string Pin_join(string pin, int num)
        {
            StringBuilder s = new StringBuilder(pin);
            if (Char.IsDigit(pin[pin.Length - 1]))
                s.Append('-');
            s.Append(num.ToString());
            return s.ToString();
        }
        List<string> Create_elem(string from, string to)
        {
            List<string> l = new List<string>();
            if (from.Length == 1 && to.Length == 1 && Char.IsLetter(from[0]) && char.IsLetter(to[0]))
            {
                char f = from[0];
                char t = to[0];
                if (Char.IsLower(f) != Char.IsLower(t))
                    Error("invalid List");
                for (char c = f; c <= t; c++)
                {
                    if (c != 'o' && c != 'i' && c != 'O' && c != 'I')
                        l.Add(new string(c, 1));
                }
            }
            else
            {
                bool test = true;
                foreach (char c in from)
                    if (!Char.IsDigit(c))
                        test = false;
                foreach (char c in to)
                    if (!Char.IsDigit(c))
                        test = false;
                if (!test)
                    Error("invalid list");
                int f = int.Parse(from);
                int t = int.Parse(to);
                int minlen = 0;
                if (from[0] == '0')
                    minlen = from.Length;
                for (int i = f; i <= t; i++)
                    l.Add(Convert.ToString(i).PadLeft(minlen, '0'));
            }
            if (l.Count < 2)
                Error("invalid list");
            return l;
        }
        List<string> Expand(string s)
        {
            List<string> l = new List<string>();
            int op = s.LastIndexOf('[');

            if (op != -1)
            {
                if (op > s.Length - 5)
                    Error("invalid List");
                string start = s.Substring(op + 1);
                s = s.Substring(0, op);
                int m = start.IndexOf('-');
                if (m == -1 || m > start.Length - 3)
                    Error("invalid List");
                string f = start.Substring(0, m);
                start = start.Substring(m + 1);
                int cl = start.IndexOf(']');
                if (cl == -1)
                    Error("invalid List");
                string t = start.Substring(0, cl);
                string end = start.Substring(cl + 1);
                List<string> l2 = Create_elem(f, t);
                List<string> l1 = Expand(s);
                foreach (string s1 in l1)
                    foreach (string s2 in l2)
                        l.Add(s1 + s2 + end);
            }
            else
                l.Add(s);
            return l;
        }
        public Module Load()
        {
            Module M = null;
            int state = 0;
            while (state != 5)
            {
                if (fi.EndOfStream)
                {
                    if (state > 0)
                        Error("unexpected End of File");
                    return null;
                }
                string rl;
                if (state == 2)
                {
                    rl = "0V +220V +150V +10V -100V -30V -250V +15V :";
                    state = 3;
                }
                else
                {
                    rl = fi.ReadLine();
                    line++;
                }
                int ci = rl.IndexOf("//");
                string comm = "";
                if (ci >= 0)
                {
                    comm = rl.Substring(ci + 2).Trim();
                    rl = rl.Substring(0, ci);
                }
                string[] s = rl.Trim().Split(new char[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (s.Length > 0)
                    switch (state)
                    {
                        case 0:
                            if (s.Length == 2 && s[0] == ".Module")
                            {
                                M = new Module(s[1]);
                                state = 2;
                            }
                            else
                                Error(".Module missing");
                            break;
                        case 1:
                            if (s.Length == 1 && s[0] == ".Connect")
                                state = 4;
                            else
                            {
                                if (s.Length >= 2 && (s[0] == "I" || s[0] == "O") && M.Pins.TryGetValue(s[1], out Module.Pin su) && su.SubIndex == M.thismodule)
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
                                    Module.direction d = Module.direction.undef;
                                    if (s[0] == "I")
                                        d = Module.direction.input;
                                    else
                                        d = Module.direction.output;
                                    M.SignalDirections[p] = d;
                                }
                                else
                                {
                                    Error("Invalid Signal entry");
                                }
                            }
                            break;
                        case 3:
                            if (s.Length == 1 && s[0] == ".Connect")
                            {

                                if (M.thismodule == -1)
                                    Error("Pin Definition Missing");
                                state = 4;
                            }
                            else if (s.Length == 1 && s[0] == ".Signals")
                            {
                                if (M.thismodule == -1)
                                    Error("Pin Definition Missing");
                                M.SignalsDefined = true;
                                state = 1;
                            }
                            else
                            {
                                Module.Submodule S = null;
                                List<string> AllPinnames = new List<string>();
                                if ((s.Length >= 3 && s[s.Length - 2] == ":") || (s.Length >= 2 && s[s.Length - 1] == ":"))
                                {
                                    S = new Module.Submodule(s[s.Length - 1] == ":" ? null : s[s.Length - 1]);

                                    int SubIndex = M.Submodules.Count;
                                    int SubPins = 0;
                                    if (S.Name == M.Name)
                                    {
                                        if (M.thismodule == -1)
                                            M.thismodule = SubIndex;
                                        else
                                            Error("Duplicate Module Pin definitions");
                                    }
                                    for (int i = 0; s[i] != ":"; i++)
                                    {
                                        List<string> Pinnames = Expand(s[i]);
                                        foreach (string Pinname in Pinnames)
                                        {
                                            if (M.Pins.ContainsKey(Pinname))
                                                Error(string.Format("Module {0}, has duplicate Pin {1}", M.Name, Pinname));
                                            M.Pins.Add(Pinname, new Module.Pin(Pinname, SubIndex, SubPins));
                                            SubPins++;
                                            AllPinnames.Add(Pinname);
                                        }
                                    }
                                    S.SetPinNames(AllPinnames);
                                    M.Submodules.Add(S);       /* Submodul hinzufügen */
                                    if (S.Name == M.Name)
                                    {
                                        M.Signals = new string[SubPins];
                                        M.SignalDirections = new Module.direction[SubPins];
                                        M.NumPins = SubPins;
                                    }
                                }
                                else
                                {
                                    if (s.Length % 2 == 1) /* ungrade anzahl: Module referenz*/
                                        S = new Module.Submodule(s[s.Length - 1]);
                                    else if (s.Length >= 2) /* greade Anzahl: verbindungs Pins */
                                        S = new Module.Submodule(null);
                                    else
                                        Error("invalid Line");
                                    if (S != null)
                                    {
                                        int SubIndex = M.Submodules.Count;
                                        int SubPins = 0;
                                        if (S.Name == M.Name)
                                        {
                                            if (M.thismodule == -1)
                                                M.thismodule = SubIndex;
                                            else
                                                Error("Duplicate Module Pin definitions");
                                        }
                                        for (int i = 0; i < s.Length - 1; i += 2)
                                        {
                                            string name = s[i];
                                            if (!int.TryParse(s[i + 1], out int num))
                                                Error(String.Format("Invalid Number {0}", s[i + 1]));
                                            int min = 1;
                                            if (name == "U")
                                                min = 0;
                                            for (int j = min; j <= num; j++)
                                            {
                                                string Pinname = Loader704.Pin_join(name, j);
                                                if (M.Pins.ContainsKey(Pinname))
                                                    Error(string.Format("Module {0}, has duplicate Pin {2}", M.Name, Pinname));
                                                M.Pins.Add(Pinname, new Module.Pin(Pinname, SubIndex, SubPins));
                                                SubPins++;
                                                AllPinnames.Add(Pinname);
                                            }
                                        }
                                        S.SetPinNames(AllPinnames);
                                        M.Submodules.Add(S);       /* Submodul hinzufügen */
                                        if (S.Name == M.Name)
                                        {
                                            M.Signals = new string[SubPins];
                                            M.SignalDirections = new Module.direction[SubPins];
                                            M.NumPins = SubPins;
                                        }
                                    }
                                }
                            }
                            break;
                        case 4:
                            if (s.Length == 1 && s[0] == ".End")
                                state = 5;
                            else if (s.Length == 3)
                            {
                                if (!M.Pins.TryGetValue(s[1], out Module.Pin F))
                                    Error(string.Format("Unkown Pin {0}", s[1]));
                                if (!M.Pins.TryGetValue(s[2], out Module.Pin T))
                                    Error(string.Format("Unkown Pin {0}", s[2]));
                                if (T.SubIndex == M.thismodule)
                                {
                                    if ((M.SignalsDefined) && (M.Signals[T.PinIndex] == null || M.Signals[T.PinIndex] != comm))
                                    {

                                        Error("wrong Signal");
                                    }
                                }
                                if (F.SubIndex == M.thismodule)
                                {

                                    if ((M.SignalsDefined) && (M.Signals[F.PinIndex] == null || M.Signals[F.PinIndex] != comm))
                                    {
                                        Error("wrong Signal");
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
    class Program
    {
        static Dictionary<string, Module> Modules;
        static HashSet<string> GetConnectedPins(Module M, int SubIndex, int PinIndex, bool[][] VisitedPins)
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
        static void Check1()
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
        static void Check2()
        {
            /* Prüfe: */
            /* Alle signal pins sind verbunden. */
            /* Bei allen mit W verbundenen Pinguppen gibt es genau ein Write und >=1 Read. */

            foreach (KeyValuePair<string, Module> Mkvp in Modules)
            {
                if (Mkvp.Key.StartsWith("MF") || Mkvp.Key == "SYSTEM") /* vorerst überspringen */
                    continue;
                bool[][] readpin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls aus dem netzwerk liest*/
                bool[][] writepin = new bool[Mkvp.Value.Submodules.Count][]; /* gibt an ob der pin eines submoduls in das netzwerk schreibt*/

                //* fülle readpin und writepin  */
                int j = 0;
                foreach (Module.Submodule S in Mkvp.Value.Submodules)
                {
                    if (S.Name != null)
                    {
                        readpin[j] = new bool[S.numpins];
                        writepin[j] = new bool[S.numpins];
                        if (!Modules.TryGetValue(S.Name, out Module M2))
                        { 
                            Console.WriteLine("Module {0} not found", S.Name);
                            Environment.Exit(-1);
                        }
                        for (int i = 0; i < S.numpins; i++)
                        {
                            if (M2.SignalDirections[i] == Module.direction.undef)
                            {
                                if (S.To[i].Count > 0 || S.From[i].Count > 0)
                                {
                                    Console.WriteLine("Module {0} Submodule{1} Pin{2} is used without Signal Definiton", Mkvp.Key, S.Name, i);
                                }
                            }
                            else
                            {
                                if (S.To[i].Count == 0 && S.From[i].Count == 0 && M2.Signals[i] != "")
                                {
                                    Console.WriteLine("Module {0}: Signal \"{1}\" of Submodule {2} is not used", Mkvp.Key, M2.Signals[i], S.Name);
                                }
                                if (M2.SignalDirections[i] == Module.direction.input)
                                {
                                    if (M2.thismodule == j)
                                        writepin[j][i] = true;
                                    else
                                        readpin[j][i] = true;
                                }
                                else
                                {
                                    if (M2.thismodule == j)
                                        readpin[j][i] = true;
                                    else
                                        writepin[j][i] = true;

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
                                            Console.WriteLine("Module {0}: Pin {1} is not connected", Mkvp.Key, s);
                                        }
                                    }
                                    else
                                    {
                                        String s = "";
                                        foreach (string e in H)
                                            s = e;
                                        Console.WriteLine("Module {0}: Pin {1} of {2} is not connected", Mkvp.Key, s, S.Name);
                                    }
                                }
                            }

                        }
                        else
                        {
                            int numread = 0;
                            int numwrite = 0;
                            StringBuilder s = new StringBuilder();
                            foreach (string pinname in H)
                            {  /* zähle wieoft in dieser Pingruppe gelesen oder geschrieben wird */
                                Module.Pin P = Mkvp.Value.Pins[pinname];
                                if (readpin[P.SubIndex] != null && readpin[P.SubIndex][P.PinIndex])
                                    numread++;
                                if (writepin[P.SubIndex] != null && writepin[P.SubIndex][P.PinIndex])
                                    numwrite++;
                                s.Append(pinname);
                                s.Append(' ');
                            }
                            if (numwrite == 0 && numread > 0)
                            {
                                Console.WriteLine("Module {0}: Connected pins {1}have no signal source", Mkvp.Key, s.ToString());
                            }
                            if (numwrite > 1)
                            {
                                Console.WriteLine("Module {0}: Connected pins {1}have multiple signal sources", Mkvp.Key, s.ToString());
                            }
                            if (numread == 0)
                            {
                                Console.WriteLine("Module {0}: Connected pins {1}have no signal sink", Mkvp.Key, s.ToString());
                            }

                        }
                    }
                    j++;
                }
            }
        }

        static void Main(string[] args)
        {
            Modules = new Dictionary<string, Module>();
            foreach (string n in Directory.GetFiles(@"..\..\", "*.txt"))
            {
                using (Loader704 l = new Loader704(n))
                {
                    while (!l.Eof)
                    {
                        Module M = l.Load();
                        if (M != null)
                        {
                            Console.WriteLine(M.Name);
                            if (Modules.ContainsKey(M.Name))
                            {
                                Console.WriteLine("Duplicate Module {0}", M.Name);
                                Environment.Exit(-1);
                            }
                            Modules.Add(M.Name, M);
                        }
                    }
                }
            }
            Check1();
            Check2();

        }
    }
}


