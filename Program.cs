using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace DiX
{
    class Program
    {
        private const string DefaultAlias = "@default";
        private const string BackAlias = "@back";
        private const string DoubleHorizontal = "\u2550";
        private const string Vertical = "\u2502";
        private const string DiXVersion = "1.0";

        private static string DataFile { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DIX.DAT"); } }
        private static string GoFile { get { return Path.Combine(Path.GetTempPath(), "DIX_GO.CMD"); } }

        private static Dictionary<string,string> Load()
        {
            Dictionary<string,string> d = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(DataFile)) return d;
            foreach (string line in File.ReadAllLines(DataFile))
            {
                int p = line.IndexOf('=');
                if (p > 0) d[line.Substring(0,p)] = line.Substring(p+1);
            }
            if (d.ContainsKey("default") && !d.ContainsKey(DefaultAlias))
            {
                d[DefaultAlias] = d["default"];
                d.Remove("default");
                Save(d);
            }
            return d;
        }

        private static void Save(Dictionary<string,string> d)
        {
            using (StreamWriter w = new StreamWriter(DataFile, false))
                foreach (KeyValuePair<string,string> item in d) w.WriteLine(item.Key + "=" + item.Value);
        }

        private static string JoinArgs(string[] args, int first)
        {
            if (first >= args.Length) return "";
            string s = args[first];
            for (int i = first + 1; i < args.Length; i++) s += " " + args[i];
            return s;
        }

        private static string Resolve(Dictionary<string,string> d, string value)
        {
            string path;
            return d.TryGetValue(value, out path) ? path : value;
        }

        private static void WriteGo(string path)
        {
            using (StreamWriter w = new StreamWriter(GoFile, false))
            {
                w.WriteLine("@echo off");
                w.WriteLine("CD /D \"" + path + "\"");
            }
        }

        private static bool IsReservedAlias(string name) { return name.StartsWith("@"); }

        private static int DoMark(string argument, Dictionary<string,string> d)
        {
            string current = Environment.CurrentDirectory;
            d[DefaultAlias] = current;
            if (argument.Length > 0)
            {
                if (IsReservedAlias(argument))
                {
                    Console.WriteLine("Alias names beginning with @ are reserved.");
                    return 2;
                }
                d[argument] = current;
            }
            Save(d);
            return 0;
        }

        private static int DoUnmark(string argument, Dictionary<string,string> d)
        {
            if (argument.Length == 0) { Console.WriteLine("UNMARK requires an alias name."); return 2; }
            if (IsReservedAlias(argument)) { Console.WriteLine("Internal aliases cannot be removed."); return 2; }
            if (d.ContainsKey(argument)) { d.Remove(argument); Save(d); }
            return 0;
        }

        private static int DoGo(string argument, Dictionary<string,string> d, bool rememberCurrent)
        {
            if (argument.Length == 0) { Console.WriteLine(Environment.CurrentDirectory); return 0; }
            string path = Resolve(d, argument);
            if (!Directory.Exists(path)) { Console.WriteLine("Directory not found: " + path); return 2; }
            if (rememberCurrent) { d[BackAlias] = Environment.CurrentDirectory; Save(d); }
            WriteGo(Path.GetFullPath(path));
            return 0;
        }

        private static int DoRecall(string argument, Dictionary<string,string> d)
        {
            string target = argument.Length == 0 ? DefaultAlias : argument;
            string path = Resolve(d, target);
            if (!Directory.Exists(path))
            {
                Console.WriteLine(argument.Length == 0 ? "No location has been marked yet." : "Directory not found: " + path);
                return 2;
            }
            WriteGo(Path.GetFullPath(path));
            return 0;
        }

        private static int DoBack(Dictionary<string,string> d)
        {
            string previous;
            if (!d.TryGetValue(BackAlias, out previous)) { Console.WriteLine("No BACK location has been stored yet."); return 2; }
            if (!Directory.Exists(previous)) { Console.WriteLine("BACK directory not found: " + previous); return 2; }
            string current = Environment.CurrentDirectory;
            d[BackAlias] = current;
            Save(d);
            WriteGo(Path.GetFullPath(previous));
            return 0;
        }

        private static int DoAliases(Dictionary<string,string> d)
        {
            List<string> names = new List<string>();
            foreach (string name in d.Keys) if (!IsReservedAlias(name)) names.Add(name);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            if (names.Count == 0) { Console.WriteLine("No aliases have been stored."); return 0; }
            Console.WriteLine("DiX aliases:");
            Console.WriteLine();
            foreach (string name in names) Console.WriteLine(name + " = " + d[name]);
            return 0;
        }

        private const int STD_OUTPUT_HANDLE = -11;

        [StructLayout(LayoutKind.Sequential)]
        private struct COORD { public short X; public short Y; public COORD(short x, short y) { X=x; Y=y; } }

        [StructLayout(LayoutKind.Sequential)]
        private struct SMALL_RECT { public short Left; public short Top; public short Right; public short Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct CONSOLE_SCREEN_BUFFER_INFO
        {
            public COORD dwSize; public COORD dwCursorPosition; public short wAttributes;
            public SMALL_RECT srWindow; public COORD dwMaximumWindowSize;
        }

        [DllImport("kernel32.dll", SetLastError=true)] private static extern IntPtr GetStdHandle(int nStdHandle);
        [DllImport("kernel32.dll", SetLastError=true)] private static extern bool GetConsoleScreenBufferInfo(IntPtr h, out CONSOLE_SCREEN_BUFFER_INFO i);
        [DllImport("kernel32.dll", SetLastError=true)] private static extern bool SetConsoleScreenBufferSize(IntPtr h, COORD s);
        [DllImport("kernel32.dll", SetLastError=true)] private static extern bool SetConsoleWindowInfo(IntPtr h, bool a, ref SMALL_RECT r);
        [DllImport("kernel32.dll", SetLastError=true)] private static extern bool GetConsoleMode(IntPtr h, out int m);

        private const int INVALID_FILE_ATTRIBUTES = -1;
        private static readonly IntPtr INVALID_HANDLE_VALUE =
            new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Auto)]
        private struct WIN32_FIND_DATA
        {
            public FileAttributes dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=260)]
            public string cFileName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=14)]
            public string cAlternateFileName;
        }

        [DllImport("kernel32.dll", CharSet=CharSet.Auto, SetLastError=true)]
        private static extern IntPtr FindFirstFile(
            string lpFileName,
            out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", CharSet=CharSet.Auto, SetLastError=true)]
        private static extern bool FindNextFile(
            IntPtr hFindFile,
            out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", SetLastError=true)]
        private static extern bool FindClose(
            IntPtr hFindFile);

        private static bool HasConsoleOutput()
        {
            try
            {
                IntPtr h = GetStdHandle(STD_OUTPUT_HANDLE); int mode;
                return h != IntPtr.Zero && h.ToInt64() != -1 && GetConsoleMode(h, out mode);
            }
            catch { return false; }
        }

        private static int ConsoleWidth()
        {
            try { if (HasConsoleOutput() && Console.WindowWidth > 10) return Console.WindowWidth; }
            catch { }
            return 80;
        }

        private class DOptions
        {
            public string Sort="n"; public bool ShowAll=false; public bool ShowTime=false;
            public bool Redirectable=false; public bool Monochrome=false;
            public bool GroupDirectories=true; public bool Help=false; public bool ForceFourColumns=false;
        }

        private class DEntry
        {
            public string Name; public string BaseName; public string Extension;
            public bool IsDirectory; public long Size; public int DirectorySizeState; public DateTime Modified; public FileAttributes Attributes;
        }

        private static bool IsDOptionChar(char c)
        {
            return "nNeEsSdDalhrwmkf?gG".IndexOf(c) >= 0;
        }

        private static bool IsDHelpToken(string t)
        {
            return String.Equals(t,"help",StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(t,"/help",StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(t,"-help",StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(t,"--help",StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDOptionToken(string t)
        {
            if(IsDHelpToken(t))
                return true;

            if(t.Length==1)
                return IsDOptionChar(t[0]);

            if(t.Length==2 &&
               (t[0]=='/' || t[0]=='-'))
            {
                return IsDOptionChar(t[1]);
            }

            if(t.Length==3 &&
               t[0]=='-' &&
               t[1]=='-')
            {
                return IsDOptionChar(t[2]);
            }

            return false;
        }

        private static char DOptionChar(string t)
        {
            if(IsDHelpToken(t))
                return '?';

            if(t.Length==1)
                return t[0];

            return t[t.Length-1];
        }

        private static void ApplyDOption(DOptions o, char c)
        {
            if ("nNeEsSdD".IndexOf(c)>=0) o.Sort=c.ToString();
            else if (c=='a') o.ShowAll=true;
            else if (c=='l') o.ShowTime=true;
            else if (c=='r') o.Redirectable=true;
            else if (c=='w') o.ForceFourColumns=true;
            else if (c=='m') o.Monochrome=true;
            else if (c=='g') o.GroupDirectories=true;
            else if (c=='G') o.GroupDirectories=false;
            else if (c=='?') o.Help=true;
        }

        private static void ParseDArguments(string[] args, Dictionary<string,string> aliases, out string target, out DOptions options)
        {
            options=new DOptions();
            List<string> tokens=new List<string>();
            for(int i=1;i<args.Length;i++) tokens.Add(args[i]);
            int optionStart=tokens.Count;
            while(optionStart>0 && IsDOptionToken(tokens[optionStart-1])) optionStart--;
            for(int i=optionStart;i<tokens.Count;i++) ApplyDOption(options,DOptionChar(tokens[i]));
            if(optionStart==0) { target=Environment.CurrentDirectory; return; }
            string s=tokens[0];
            for(int i=1;i<optionStart;i++) s+=" "+tokens[i];
            target=Resolve(aliases,s);
        }

        private static void SplitTarget(string rawTarget, out string directory, out string mask)
        {
            string target=rawTarget;
            if(target=="\\") { directory=Path.GetPathRoot(Environment.CurrentDirectory); mask="*"; return; }
            bool wild=target.IndexOf('*')>=0 || target.IndexOf('?')>=0;
            if(wild)
            {
                string dir=Path.GetDirectoryName(target); string file=Path.GetFileName(target);
                if(String.IsNullOrEmpty(dir)) dir=Environment.CurrentDirectory;
                directory=Path.GetFullPath(dir); mask=String.IsNullOrEmpty(file)?"*":file; return;
            }
            if(Directory.Exists(target)) { directory=Path.GetFullPath(target); mask="*"; return; }
            string parent=Path.GetDirectoryName(target); string leaf=Path.GetFileName(target);
            if(!String.IsNullOrEmpty(parent) && Directory.Exists(parent)) { directory=Path.GetFullPath(parent); mask=leaf; return; }
            directory=target; mask="*";
        }

        private static bool IsHidden(FileAttributes a) { return (a&FileAttributes.Hidden)!=0 || (a&FileAttributes.System)!=0; }

        private const int DirectorySizeUnavailable = 0;
        private const int DirectorySizeExact = 1;
        private const int DirectorySizePartial = 2;

        private static bool IsLocalNtfs(
            string directory)
        {
            try
            {
                string root=
                    Path.GetPathRoot(
                        Path.GetFullPath(directory));

                if(String.IsNullOrEmpty(root))
                    return false;

                DriveInfo di=new DriveInfo(root);

                return di.DriveType==DriveType.Fixed &&
                       String.Equals(
                           di.DriveFormat,
                           "NTFS",
                           StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static int TryGetDirectorySizeWin32(
            string root,
            int milliseconds,
            out long size)
        {
            size=0;

            if(milliseconds<=0)
                return DirectorySizeUnavailable;

            Stopwatch sw=Stopwatch.StartNew();
            Stack<string> pending=new Stack<string>();
            bool sawAnything=false;
            bool incomplete=false;

            pending.Push(root);

            while(pending.Count>0)
            {
                if(sw.ElapsedMilliseconds>=milliseconds)
                    return (sawAnything || size>0)
                        ? DirectorySizePartial
                        : DirectorySizeUnavailable;

                string dir=pending.Pop();
                string pattern=
                    Path.Combine(dir,"*");

                WIN32_FIND_DATA data;
                IntPtr h=FindFirstFile(
                    pattern,
                    out data);

                if(h==INVALID_HANDLE_VALUE)
                {
                    incomplete=true;
                    continue;
                }

                try
                {
                    bool more=true;

                    while(more)
                    {
                        if(sw.ElapsedMilliseconds>=milliseconds)
                            return DirectorySizePartial;

                        string name=data.cFileName;

                        if(name!="." && name!="..")
                        {
                            sawAnything=true;

                            bool isDirectory=
                                (data.dwFileAttributes&
                                 FileAttributes.Directory)!=0;

                            bool isReparse=
                                (data.dwFileAttributes&
                                 FileAttributes.ReparsePoint)!=0;

                            if(isDirectory)
                            {
                                if(!isReparse)
                                {
                                    pending.Push(
                                        Path.Combine(
                                            dir,
                                            name));
                                }
                                else
                                {
                                    incomplete=true;
                                }
                            }
                            else
                            {
                                ulong value=
                                    ((ulong)data.nFileSizeHigh<<32) |
                                    (ulong)data.nFileSizeLow;

                                if(value<=Int64.MaxValue)
                                    size+=(long)value;
                                else
                                    incomplete=true;
                            }
                        }

                        more=FindNextFile(
                            h,
                            out data);
                    }
                }
                finally
                {
                    FindClose(h);
                }
            }

            if(!sawAnything && size==0)
                return DirectorySizeExact;

            return incomplete
                ? DirectorySizePartial
                : DirectorySizeExact;
        }

        private static int TryGetDirectorySizeBounded(
            string root,
            int milliseconds,
            out long size)
        {
            size=0;

            if(milliseconds<=0)
                return DirectorySizeUnavailable;

            Stopwatch sw=Stopwatch.StartNew();
            Stack<string> pending=new Stack<string>();
            bool sawAnything=false;
            bool incomplete=false;

            pending.Push(root);

            while(pending.Count>0)
            {
                if(sw.ElapsedMilliseconds>=milliseconds)
                    return (sawAnything || size>0)
                        ? DirectorySizePartial
                        : DirectorySizeUnavailable;

                string dir=pending.Pop();

                string[] files=null;
                try
                {
                    files=Directory.GetFiles(dir);
                    sawAnything=true;
                }
                catch
                {
                    incomplete=true;
                }

                if(files!=null)
                {
                    for(int i=0;i<files.Length;i++)
                    {
                        if(sw.ElapsedMilliseconds>=milliseconds)
                            return DirectorySizePartial;

                        try
                        {
                            size+=new FileInfo(files[i]).Length;
                            sawAnything=true;
                        }
                        catch
                        {
                            incomplete=true;
                        }
                    }
                }

                string[] dirs=null;
                try
                {
                    dirs=Directory.GetDirectories(dir);
                    sawAnything=true;
                }
                catch
                {
                    incomplete=true;
                }

                if(dirs!=null)
                {
                    for(int i=0;i<dirs.Length;i++)
                    {
                        if(sw.ElapsedMilliseconds>=milliseconds)
                            return DirectorySizePartial;

                        try
                        {
                            FileAttributes a=File.GetAttributes(dirs[i]);

                            // Never follow junctions/reparse points.
                            if((a&FileAttributes.ReparsePoint)==0)
                                pending.Push(dirs[i]);
                            else
                                incomplete=true;
                        }
                        catch
                        {
                            incomplete=true;
                        }
                    }
                }
            }

            if(!sawAnything && size==0)
                return incomplete
                    ? DirectorySizeUnavailable
                    : DirectorySizeExact;

            return incomplete
                ? DirectorySizePartial
                : DirectorySizeExact;
        }

        private static string FormatDirectorySize(DEntry e)
        {
            if(!e.IsDirectory)
                return FormatSize(e.Size);

            if(e.DirectorySizeState==DirectorySizeUnavailable)
                return "----";

            string value=FormatSize(e.Size);

            if(e.DirectorySizeState==DirectorySizePartial)
                return ">" + value;

            return value;
        }

        private static List<DEntry> ReadEntries(string directory,string mask,DOptions options)
        {
            List<DEntry> list=new List<DEntry>();

            const int TotalSizingBudgetMs=2000;

            string[] allPaths=
                Directory.GetFileSystemEntries(directory,mask);

            int directoryCount=0;

            for(int countIndex=0;
                countIndex<allPaths.Length;
                countIndex++)
            {
                try
                {
                    FileAttributes countAttributes=
                        File.GetAttributes(allPaths[countIndex]);

                    if((countAttributes&FileAttributes.Directory)!=0)
                        directoryCount++;
                }
                catch
                {
                }
            }

            int PerDirectoryBudgetMs=
                directoryCount>0
                ? TotalSizingBudgetMs/directoryCount
                : 0;

            if(directoryCount>0 &&
               PerDirectoryBudgetMs<1)
            {
                PerDirectoryBudgetMs=1;
            }

            Stopwatch sizingClock=Stopwatch.StartNew();

            foreach(string p in allPaths)
            {
                FileAttributes a; try { a=File.GetAttributes(p); } catch { continue; }
                if(!options.ShowAll && IsHidden(a)) continue;

                DEntry e=new DEntry();
                e.Name=Path.GetFileName(p);
                e.IsDirectory=(a&FileAttributes.Directory)!=0;
                e.Attributes=a;

                if(e.IsDirectory)
                {
                    e.BaseName=e.Name;
                    e.Extension="";
                    e.Size=0;
                    e.DirectorySizeState=DirectorySizeUnavailable;
                    e.Modified=Directory.GetLastWriteTime(p);

                    int remaining=
                        TotalSizingBudgetMs-
                        (int)sizingClock.ElapsedMilliseconds;

                    if(remaining>0 &&
                       PerDirectoryBudgetMs>0)
                    {
                        int budget=PerDirectoryBudgetMs;

                        if(budget>remaining)
                            budget=remaining;

                        long directorySize;

                        if(IsLocalNtfs(p))
                        {
                            e.DirectorySizeState=
                                TryGetDirectorySizeWin32(
                                    p,
                                    budget,
                                    out directorySize);
                        }
                        else
                        {
                            e.DirectorySizeState=
                                TryGetDirectorySizeBounded(
                                    p,
                                    budget,
                                    out directorySize);
                        }

                        e.Size=directorySize;
                    }
                }
                else
                {
                    e.BaseName=Path.GetFileNameWithoutExtension(e.Name);
                    string ext=Path.GetExtension(e.Name);
                    if(ext.StartsWith(".")) ext=ext.Substring(1);
                    e.Extension=ext;
                    e.DirectorySizeState=DirectorySizeExact;

                    try
                    {
                        FileInfo fi=new FileInfo(p);
                        e.Size=fi.Length;
                        e.Modified=fi.LastWriteTime;
                    }
                    catch
                    {
                        e.Size=0;
                        e.Modified=DateTime.MinValue;
                    }
                }

                list.Add(e);
            }

            return list;
        }

        private static int NaturalCompare(
            string left,
            string right)
        {
            if(left==null) left="";
            if(right==null) right="";

            int i=0;
            int j=0;

            while(i<left.Length &&
                  j<right.Length)
            {
                char a=left[i];
                char b=right[j];

                if(Char.IsDigit(a) &&
                   Char.IsDigit(b))
                {
                    int iStart=i;
                    int jStart=j;

                    while(i<left.Length &&
                          left[i]=='0')
                        i++;

                    while(j<right.Length &&
                          right[j]=='0')
                        j++;

                    int iDigits=i;
                    int jDigits=j;

                    while(iDigits<left.Length &&
                          Char.IsDigit(left[iDigits]))
                        iDigits++;

                    while(jDigits<right.Length &&
                          Char.IsDigit(right[jDigits]))
                        jDigits++;

                    int iLen=iDigits-i;
                    int jLen=jDigits-j;

                    if(iLen!=jLen)
                        return iLen<jLen ? -1 : 1;

                    for(int k=0;k<iLen;k++)
                    {
                        if(left[i+k]!=right[j+k])
                            return left[i+k]<right[j+k] ? -1 : 1;
                    }

                    int iRaw=iDigits-iStart;
                    int jRaw=jDigits-jStart;

                    if(iRaw!=jRaw)
                        return iRaw<jRaw ? -1 : 1;

                    i=iDigits;
                    j=jDigits;
                    continue;
                }

                char ua=Char.ToUpperInvariant(a);
                char ub=Char.ToUpperInvariant(b);

                if(ua!=ub)
                    return ua<ub ? -1 : 1;

                i++;
                j++;
            }

            if(i<left.Length)
                return 1;

            if(j<right.Length)
                return -1;

            return 0;
        }

        private class DEntryComparer : IComparer<DEntry>
        {
            private string sort; private bool group;
            public DEntryComparer(string s,bool g) { sort=s; group=g; }
            public int Compare(DEntry x,DEntry y)
            {
                if(group && x.IsDirectory!=y.IsDirectory) return x.IsDirectory?-1:1;
                char c=sort[0]; bool desc=Char.IsUpper(c); char lower=Char.ToLower(c); int r=0;
                if(lower=='n') r=NaturalCompare(x.Name,y.Name);
                else if(lower=='e') r=StringComparer.OrdinalIgnoreCase.Compare(x.Extension,y.Extension);
                else if(lower=='s')
                {
                    bool xKnown=!x.IsDirectory ||
                        x.DirectorySizeState!=DirectorySizeUnavailable;
                    bool yKnown=!y.IsDirectory ||
                        y.DirectorySizeState!=DirectorySizeUnavailable;

                    if(xKnown!=yKnown)
                        r=xKnown?-1:1;
                    else
                        r=x.Size.CompareTo(y.Size);
                }
                else if(lower=='d') r=x.Modified.CompareTo(y.Modified);
                if(r==0) r=NaturalCompare(x.Name,y.Name);
                return desc?-r:r;
            }
        }

        private static string FormatSize(long size)
        {
            if(size==0)
                return "0B";

            if(size<1024)
                return size.ToString()+"B";

            double value=(double)size/1024.0;

            if(value<1024.0)
                return value>=10.0
                    ? value.ToString("0")+"K"
                    : value.ToString("0.0")+"K";

            value=value/1024.0;

            if(value<1024.0)
                return value>=10.0
                    ? value.ToString("0")+"M"
                    : value.ToString("0.0")+"M";

            value=value/1024.0;

            return value>=10.0
                ? value.ToString("0")+"G"
                : value.ToString("0.0")+"G";
        }

        private static string AttributeText(FileAttributes a)
        {
            string s="";
            s+=(a&FileAttributes.ReadOnly)!=0?"R":"-"; s+=(a&FileAttributes.Hidden)!=0?"H":"-";
            s+=(a&FileAttributes.System)!=0?"S":"-"; s+=(a&FileAttributes.Archive)!=0?"A":"-";
            return s;
        }

        private static void SetColor(ConsoleColor c,DOptions o) { if(!o.Monochrome && !o.Redirectable && HasConsoleOutput()) Console.ForegroundColor=c; }
        private static void ResetColor(DOptions o) { if(!o.Monochrome && !o.Redirectable && HasConsoleOutput()) Console.ResetColor(); }

        private static bool ExtIn(
            string ext,
            string values)
        {
            string[] parts=
                values.Split(',');

            for(int i=0;i<parts.Length;i++)
            {
                if(String.Equals(
                       ext,
                       parts[i],
                       StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static ConsoleColor EntryColor(DEntry e)
        {
            if(e.IsDirectory)
                return ConsoleColor.Red;

            string ext=e.Extension.ToUpperInvariant();

            // Programming languages and interpreted scripts.
            if(ExtIn(
                ext,
                "JS,JSX,TS,TSX,AS,REXX,REX,PY,PYW,RB,PL,PM,PHP,LUA,TCL,"+
                "C,H,CPP,CXX,CC,HPP,HXX,CS,JAVA,KT,KTS,GO,RS,SWIFT,"+
                "ASM,S,INC,PAS,PP,BAS,VB,VBS,FS,FSX,F90,F95,FOR,COB,CLJ,"+
                "SCALA,DART,R,SQL,PS1,PSM1,SH,BASH,ZSH,CLASS"))
            {
                return ConsoleColor.Yellow;
            }

            // Command/control scripts.
            if(ExtIn(ext,"BAT,CMD,WSF,REG"))
                return ConsoleColor.Yellow;

            // Executables / binaries / libraries.
            if(ExtIn(
                ext,
                "EXE,COM,DLL,SYS,DRV,OCX,CPL,BIN,LIB,A,SO,DYLIB,JAR"))
            {
                return ConsoleColor.Green;
            }

            // Office documents and common project/creative-work files.
            if(ExtIn(
                ext,
                "RTF,DOC,DOCX,DOT,DOTX,DOCM,DOTM,"+
                "WPD,WPT,WPF,WP,WP4,WP5,WP6,WP7,WP8,WP9,WPG,"+
                "XLS,XLSX,XLSM,XLSB,XLT,XLTX,XLTM,CSV,"+
                "PPT,PPTX,PPTM,POT,POTX,POTM,PPS,PPSX,PPSM,"+
                "MDB,ACCDB,ACCDE,ACCDT,ACCDA,"+
                "PUB,ONE,ONETOC2,"+
                "ODT,OTT,ODS,OTS,ODP,OTP,ODG,OTG,ODF,"+
                "PDF,XPS,"+
                "PSD,PSB,AI,INDD,INDT,IDML,AFDESIGN,AFPHOTO,AFPUB,"+
                "BLEND,BLEND1,MAX,3DS,C4D,MA,MB,FBX,OBJ,STL,DAE,GLTF,GLB,"+
                "DWG,DXF,DGN,SKP,RVT,RFA,FCSTD,"+
                "PRPROJ,AEP,AET,AEPX,VEG,VEGAS,KDENLIVE,MLT,"+
                "FLA,XFL,ASE,ASEPRITE,KRA,XCF,CLIP,SAI,SAI2,"+
                "MMPZ,FLP,ALS,LOGICX,PTX,SESX,RPP,"+
                "SLN,CSPROJ,VBPROJ,VCPROJ,VCXPROJ,FSproj,PROJ"))
            {
                return ConsoleColor.Magenta;
            }

            // Pictures.
            if(ExtIn(
                ext,
                "BMP,JPG,JPEG,PNG,GIF,TIF,TIFF,ICO,PCX,TGA,WEBP,HEIC,HEIF,RAW,CR2,CR3,NEF,ARW,DNG"))
            {
                return ConsoleColor.DarkMagenta;
            }

            // Video.
            if(ExtIn(
                ext,
                "AVI,MPG,MPEG,MP4,M4V,MKV,MOV,WMV,FLV,VOB,WEBM,M2TS,MTS,TS,3GP,3GPP,SWF"))
            {
                return ConsoleColor.DarkCyan;
            }

            // Music / audio.
            if(ExtIn(
                ext,
                "WAV,MP3,WMA,OGG,FLAC,AAC,M4A,MID,MIDI,AIFF,AIF,APE,OPUS,AC3,DTS"))
            {
                return ConsoleColor.Cyan;
            }

            // Archives / compressed files / disk images.
            if(ExtIn(
                ext,
                "ZIP,RAR,7Z,ARJ,LZH,CAB,TAR,GZ,TGZ,BZ2,TBZ2,XZ,TXZ,Z,ISO,IMG,DMG,VHD,VHDX,WIM"))
            {
                return ConsoleColor.DarkYellow;
            }

            // Plain text, markup and configuration.
            if(ExtIn(
                ext,
                "TXT,INI,CFG,CONF,CONFIG,LOG,NFO,MD,MARKDOWN,XML,YML,YAML,JSON,"+
                "HTM,HTML,CSS,SCSS,LESS,TOML,INF,MANIFEST"))
            {
                return ConsoleColor.White;
            }

            return ConsoleColor.Gray;
        }

        private static void PrintLeftRight(
            string left,
            string right)
        {
            int width=ConsoleWidth()-1;
            if(width<20) width=20;

            if(right.Length>=width)
            {
                Console.WriteLine(Crop(right,width));
                return;
            }

            int leftWidth=width-right.Length-1;

            if(leftWidth<1)
            {
                Console.WriteLine(Crop(right,width));
                return;
            }

            left=Crop(left,leftWidth);

            Console.Write(left);

            int spaces=width-left.Length-right.Length;
            if(spaces<1) spaces=1;

            Console.Write(new string(' ',spaces));
            Console.WriteLine(right);
        }

        private static void PrintDoubleRule(DOptions o)
        {
            if(o.Redirectable) return;
            int width=ConsoleWidth()-1; if(width<20) width=20;
            SetColor(ConsoleColor.Cyan,o);
            Console.WriteLine(new string(DoubleHorizontal[0],width));
            ResetColor(o);
        }

        private static void PrintHeader(string directory,string mask,DOptions o)
        {
            if(o.Redirectable) return;
            string label=""; string root=Path.GetPathRoot(directory);
            try { label=new DriveInfo(root).VolumeLabel; } catch { }
            string left=
                "Volume Label: " +
                (label.Length==0?"(none)":label) +
                ", Catalog: " +
                Path.Combine(directory,mask);

            string right="DiX version "+DiXVersion;

            PrintLeftRight(left,right);
            PrintDoubleRule(o);
        }

        private static string Crop(string s,int width)
        {
            if(width<=0) return "";
            if(s.Length<=width) return s;
            if(width<=3) return s.Substring(0,width);
            return s.Substring(0,width-3)+"...";
        }

        private static int CompactNameWidth(DEntry e)
        {
            int width=e.BaseName.Length;

            if(width<8)
                width=8;

            return width;
        }

        private static int CompactExtensionWidth(DEntry e)
        {
            string ext=
                e.IsDirectory
                ? "DIR"
                : e.Extension;

            int width=ext.Length;

            if(width<3)
                width=3;

            return width;
        }

        private static int CompactSizeWidth(DEntry e)
        {
            string size=
                FormatDirectorySize(e);

            int width=size.Length;

            if(width<4)
                width=4;

            return width;
        }

        private static string CompactText(
            DEntry e,
            int nameWidth,
            int extWidth,
            int sizeWidth)
        {
            string ext=
                e.IsDirectory
                ? "DIR"
                : e.Extension;

            string size=
                FormatDirectorySize(e);

            return e.BaseName.PadRight(nameWidth)+
                   "    "+
                   ext.PadRight(extWidth)+
                   "    "+
                   size.PadLeft(sizeWidth);
        }

        private static void CalculateColumnMetrics(
            List<DEntry> entries,
            int columns,
            out int rows,
            out int[] nameWidths,
            out int[] extWidths,
            out int[] sizeWidths,
            out int[] requiredCellWidths)
        {
            rows=
                (entries.Count+columns-1)/
                columns;

            nameWidths=new int[columns];
            extWidths=new int[columns];
            sizeWidths=new int[columns];
            requiredCellWidths=new int[columns];

            for(int col=0;
                col<columns;
                col++)
            {
                int nameWidth=8;
                int extWidth=3;
                int sizeWidth=4;

                for(int row=0;
                    row<rows;
                    row++)
                {
                    int index=
                        col*rows+row;

                    if(index>=entries.Count)
                        continue;

                    DEntry e=
                        entries[index];

                    int n=
                        CompactNameWidth(e);

                    int x=
                        CompactExtensionWidth(e);

                    int z=
                        CompactSizeWidth(e);

                    if(n>nameWidth)
                        nameWidth=n;

                    if(x>extWidth)
                        extWidth=x;

                    if(z>sizeWidth)
                        sizeWidth=z;
                }

                nameWidths[col]=nameWidth;
                extWidths[col]=extWidth;
                sizeWidths[col]=sizeWidth;

                requiredCellWidths[col]=
                    2+
                    nameWidth+
                    4+
                    extWidth+
                    4+
                    sizeWidth;
            }
        }

        private static int RequiredCompactWidth(
            int[] cellWidths)
        {
            int total=0;

            for(int i=0;
                i<cellWidths.Length;
                i++)
            {
                total+=cellWidths[i];
            }

            if(cellWidths.Length>1)
                total+=cellWidths.Length-1;

            return total;
        }

        private static int FindBestColumnCount(
            List<DEntry> entries,
            int consoleWidth)
        {
            int max=entries.Count;

            if(max>12)
                max=12;

            for(int columns=max;
                columns>=1;
                columns--)
            {
                int rows;
                int[] nameWidths;
                int[] extWidths;
                int[] sizeWidths;
                int[] required;

                CalculateColumnMetrics(
                    entries,
                    columns,
                    out rows,
                    out nameWidths,
                    out extWidths,
                    out sizeWidths,
                    out required);

                if(RequiredCompactWidth(required)<=
                   consoleWidth)
                {
                    return columns;
                }
            }

            return 1;
        }

        private static int[] ExpandColumnsToFill(
            int[] required,
            int consoleWidth)
        {
            int columns=required.Length;
            int separators=
                columns>1
                ? columns-1
                : 0;

            int available=
                consoleWidth-separators;

            int requiredTotal=0;

            for(int i=0;
                i<columns;
                i++)
            {
                requiredTotal+=required[i];
            }

            int[] result=
                new int[columns];

            if(requiredTotal>=available)
            {
                for(int i=0;
                    i<columns;
                    i++)
                {
                    result[i]=required[i];
                }

                return result;
            }

            int used=0;

            for(int i=0;
                i<columns;
                i++)
            {
                if(i==columns-1)
                {
                    result[i]=
                        available-used;
                }
                else
                {
                    double share=
                        ((double)required[i]/
                         (double)requiredTotal)*
                        (double)available;

                    int width=
                        (int)Math.Floor(share);

                    if(width<required[i])
                        width=required[i];

                    result[i]=width;
                    used+=width;
                }
            }

            return result;
        }

        private static void PrintCompact(
            List<DEntry> entries,
            DOptions o)
        {
            if(entries.Count==0)
                return;

            int width=
                ConsoleWidth()-1;

            if(width<20)
                width=20;

            int columns=
                o.ForceFourColumns
                ? 4
                : FindBestColumnCount(
                      entries,
                      width);

            if(columns>entries.Count)
                columns=entries.Count;

            if(columns<1)
                columns=1;

            int rows;
            int[] nameWidths;
            int[] extWidths;
            int[] sizeWidths;
            int[] required;

            CalculateColumnMetrics(
                entries,
                columns,
                out rows,
                out nameWidths,
                out extWidths,
                out sizeWidths,
                out required);

            while(columns>1 &&
                  RequiredCompactWidth(required)>
                  width)
            {
                columns--;

                CalculateColumnMetrics(
                    entries,
                    columns,
                    out rows,
                    out nameWidths,
                    out extWidths,
                    out sizeWidths,
                    out required);
            }

            int[] cellWidths=
                ExpandColumnsToFill(
                    required,
                    width);

            for(int row=0;
                row<rows;
                row++)
            {
                for(int col=0;
                    col<columns;
                    col++)
                {
                    int index=
                        col*rows+row;

                    int cellWidth=
                        cellWidths[col];

                    if(index<entries.Count)
                    {
                        DEntry e=
                            entries[index];

                        int extra=
                            cellWidth-
                            required[col];

                        int displayNameWidth=
                            nameWidths[col]+
                            extra;

                        string text=
                            CompactText(
                                e,
                                displayNameWidth,
                                extWidths[col],
                                sizeWidths[col]);

                        SetColor(
                            EntryColor(e),
                            o);

                        Console.Write(
                            " "+text+" ");

                        ResetColor(o);
                    }
                    else
                    {
                        Console.Write(
                            new string(
                                ' ',
                                cellWidth));
                    }

                    if(col<columns-1)
                    {
                        SetColor(
                            ConsoleColor.Cyan,
                            o);

                        Console.Write(
                            Vertical);

                        ResetColor(o);
                    }
                }

                Console.WriteLine();
            }
        }

        private static void PrintDetailed(List<DEntry> entries,DOptions o)
        {
            int totalWidth=ConsoleWidth()-1;
            int extWidth=6, attrWidth=o.ShowAll?5:0, sizeWidth=10, timeWidth=o.ShowTime?16:0;
            int nameWidth=8;

            foreach(DEntry scan in entries)
            {
                if(scan.BaseName.Length>nameWidth)
                    nameWidth=scan.BaseName.Length;
            }

            foreach(DEntry e in entries)
            {
                SetColor(ConsoleColor.Cyan,o); Console.Write(Vertical); ResetColor(o);
                SetColor(EntryColor(e),o);
                string name=e.BaseName, ext=e.IsDirectory?"DIR":e.Extension, size=FormatDirectorySize(e);
                Console.Write(" "+name.PadRight(nameWidth)+" "+ext.PadRight(extWidth));
                if(o.ShowAll) Console.Write(AttributeText(e.Attributes).PadRight(attrWidth));
                Console.Write(size.PadLeft(sizeWidth));
                if(o.ShowTime) Console.Write(" "+e.Modified.ToString("yy-MM-dd HH:mm"));
                int used=1+nameWidth+1+extWidth+attrWidth+sizeWidth+(o.ShowTime?16:0);
                int remain=(totalWidth-2)-used;
                if(remain>0) Console.Write(new string(' ',remain));
                ResetColor(o);
                SetColor(ConsoleColor.Cyan,o); Console.Write(Vertical); ResetColor(o);
                Console.WriteLine();
            }
        }

        private static string FormatSummaryBytes(
            long size)
        {
            if(size<1024)
            {
                return size.ToString()+
                       " Bytes";
            }

            return FormatSize(size)+
                   " Bytes";
        }

        private static void PrintSummary(List<DEntry> entries,string directory,DOptions o)
        {
            if(o.Redirectable) return;
            long bytes=0; int files=0, dirs=0;
            foreach(DEntry e in entries) { if(e.IsDirectory) dirs++; else { files++; bytes+=e.Size; } }
            long free=0; try { free=new DriveInfo(Path.GetPathRoot(directory)).AvailableFreeSpace; } catch { }
            PrintDoubleRule(o);

            string dirText=
                dirs==1
                ? "1 Directory"
                : dirs+" Directories";

            string fileText=
                files==1
                ? "1 File"
                : files+" Files";

            string left=
                dirText+
                ",  "+fileText+
                ",  "+FormatSummaryBytes(bytes)+" Displayed"+
                ",  "+FormatSummaryBytes(free)+" Free.";

            string right=
                DateTime.Now.ToString(
                    "yyyy/MM/dd hh:mm:ss tt");

            PrintLeftRight(left,right);
        }

        private static void PrintHelpOption(
            string option,
            string description)
        {
            Console.Write("  ");
            Console.Write(option.PadRight(20));
            Console.WriteLine(description);
        }

        private static void PrintHelpSection(
            string title)
        {
            Console.WriteLine();
            Console.WriteLine(title);
            Console.WriteLine(new string('-',title.Length));
        }

        private static void PrintDHelp()
        {
            int width=ConsoleWidth()-1;
            if(width<60) width=60;

            PrintLeftRight(
                "Directory Interaction eXtensions Help",
                "DiX version "+DiXVersion);

            Console.WriteLine(
                new string('=',width));

            Console.WriteLine();
            Console.WriteLine(
                "Usage: D [path | alias | filespec] [options]");

            Console.WriteLine();
            Console.WriteLine(
                "Options may be written without a prefix, or with /, - or -- prefixes.");

            Console.WriteLine();
            Console.WriteLine(
                "Examples:  D *.BAT N    D *.BAT /N    D *.BAT -N    D *.BAT --N");

            PrintHelpSection("Sorting");

            PrintHelpOption(
                "n  N",
                "Name, ascending / descending");

            PrintHelpOption(
                "e  E",
                "Extension, ascending / descending");

            PrintHelpOption(
                "s  S",
                "Size, ascending / descending");

            PrintHelpOption(
                "d  D",
                "Date/time, ascending / descending");

            PrintHelpSection("Display");

            PrintHelpOption(
                "a",
                "Show hidden/system files and attributes");

            PrintHelpOption(
                "l",
                "Show last-changed date/time");

            PrintHelpOption(
                "r",
                "Plain output suitable for redirection");

            PrintHelpOption(
                "w",
                "Enables/Disables forced four columns compact view");

            PrintHelpOption(
                "m",
                "Monochrome output");

            PrintHelpOption(
                "g",
                "Group directories first (default)");

            PrintHelpOption(
                "G",
                "Mix directories and files according to sort options");

            PrintHelpOption(
                "? / help",
                "Display this help");

            PrintHelpSection("Directory sizes");

            Console.WriteLine(
                "  D allocates limited time for directory size calculation. Depending on amount of directories and system settings, the available information might not be complete.");

            Console.WriteLine(
                "  84M = exact size, 0B = empty, >5.3G = partial size calculation.");

            Console.WriteLine(
                "  ---- = no useful size could be obtained.");

            PrintHelpSection("Colors");

            PrintHelpOption(
                "Red",
                "Directories");

            PrintHelpOption(
                "Yellow",
                "Programming languages and scripts");

            PrintHelpOption(
                "Green",
                "Executables / binaries / libraries");

            PrintHelpOption(
                "Magenta",
                "Office documents and project/creative files");

            PrintHelpOption(
                "Dark magenta",
                "Pictures");

            PrintHelpOption(
                "Dark cyan",
                "Video");

            PrintHelpOption(
                "Cyan",
                "Music / audio");

            PrintHelpOption(
                "Dark yellow",
                "Archives / disk images");

            PrintHelpOption(
                "White",
                "Text / markup / configuration");

            PrintHelpOption(
                "Gray",
                "Other files");

            Console.WriteLine();
            Console.WriteLine(
                new string('=',width));
        }

        private static int DoDirectory(string[] args,Dictionary<string,string> aliases)
        {
            string target; DOptions options;
            ParseDArguments(args,aliases,out target,out options);
            if(options.Help) { PrintDHelp(); return 0; }

            string directory,mask;
            try { SplitTarget(target,out directory,out mask); }
            catch(Exception ex) { Console.WriteLine("Invalid path: "+ex.Message); return 2; }
            if(!Directory.Exists(directory)) { Console.WriteLine("Directory not found: "+directory); return 2; }

            List<DEntry> entries;
            try { entries=ReadEntries(directory,mask,options); }
            catch(Exception ex) { Console.WriteLine("Unable to list directory: "+ex.Message); return 2; }

            entries.Sort(new DEntryComparer(options.Sort,options.GroupDirectories));
            PrintHeader(directory,mask,options);
            if(options.ShowAll || options.ShowTime) PrintDetailed(entries,options); else PrintCompact(entries,options);
            PrintSummary(entries,directory,options);
            ResetColor(options);
            return 0;
        }

        static int Main(string[] args)
        {
            if(args.Length==0) { Console.WriteLine("DiX: GO, BACK, MARK, RECALL, UNMARK, ALIASES, D"); return 1; }
            string command=args[0].ToUpperInvariant();
            string argument=JoinArgs(args,1);
            Dictionary<string,string> d=Load();

            if(command=="MARK") return DoMark(argument,d);
            if(command=="UNMARK") return DoUnmark(argument,d);
            if(command=="GO") return DoGo(argument,d,true);
            if(command=="RECALL") return DoRecall(argument,d);
            if(command=="BACK") return DoBack(d);
            if(command=="ALIASES") return DoAliases(d);
            if(command=="D") return DoDirectory(args,d);

            Console.WriteLine("Unknown DiX command: "+command);
            return 3;
        }
    }
}
