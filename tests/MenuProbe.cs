using System;
using System.Runtime.InteropServices;

[ComImport,Guid("00000001-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ClassFactory {
    [PreserveSig] int CreateInstance(IntPtr outer,ref Guid iid,out IntPtr result);
    [PreserveSig] int LockServer(bool locked);
}
[ComImport,Guid("A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ExplorerCommand {
    void GetTitle(IntPtr items,out IntPtr title);
    void GetIcon(IntPtr items,out IntPtr icon);
    void GetToolTip(IntPtr items,out IntPtr tip);
    void GetCanonicalName(out Guid value);
    void GetState(IntPtr items,[MarshalAs(UnmanagedType.Bool)]bool slow,out uint state);
    [PreserveSig] int Invoke(IntPtr items,IntPtr bind);
    void GetFlags(out uint flags);
    [PreserveSig] int EnumSubCommands(out IntPtr commands);
}
static class MenuProbe {
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibrary(string path);
    [DllImport("kernel32.dll",CharSet=CharSet.Ansi,SetLastError=true)] static extern IntPtr GetProcAddress(IntPtr library,string name);
    [DllImport("ole32.dll")] static extern int CoCreateInstance(ref Guid clsid,IntPtr outer,uint context,ref Guid iid,out IntPtr result);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern int SHParseDisplayName(string name,IntPtr bind,out IntPtr pidl,uint attrs,out uint found);
    [DllImport("shell32.dll")] static extern int SHCreateShellItemArrayFromIDLists(uint count,IntPtr[] pidls,out IntPtr array);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetFactory(ref Guid clsid,ref Guid iid,out IntPtr result);
    [STAThread] static int Main(string[] args) {
        try {
            Guid clsid=new Guid("ED3017E2-5A5A-4CD5-A714-EC67642842A7"),iid=new Guid("A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9");
            IntPtr pointer;
            if(args[0]=="--registered") {
                Marshal.ThrowExceptionForHR(CoCreateInstance(ref clsid,IntPtr.Zero,4,ref iid,out pointer));
                Console.WriteLine("Activated registered out-of-process COM command.");
            } else {
                IntPtr dll=LoadLibrary(args[0]); if(dll==IntPtr.Zero) throw new Exception("LoadLibrary error "+Marshal.GetLastWin32Error());
                var get=(GetFactory)Marshal.GetDelegateForFunctionPointer(GetProcAddress(dll,"DllGetClassObject"),typeof(GetFactory));
                Guid factoryId=new Guid("00000001-0000-0000-C000-000000000046"); IntPtr factoryPointer;
                Marshal.ThrowExceptionForHR(get(ref clsid,ref factoryId,out factoryPointer));
                var factory=(ClassFactory)Marshal.GetObjectForIUnknown(factoryPointer); Marshal.Release(factoryPointer);
                Marshal.ThrowExceptionForHR(factory.CreateInstance(IntPtr.Zero,ref iid,out pointer)); Marshal.ReleaseComObject(factory);
            }
            var command=(ExplorerCommand)Marshal.GetObjectForIUnknown(pointer); Marshal.Release(pointer);
            IntPtr title,icon; Guid name; uint state,flags;
            command.GetTitle(IntPtr.Zero,out title); string text=Marshal.PtrToStringUni(title); Marshal.FreeCoTaskMem(title);
            command.GetIcon(IntPtr.Zero,out icon); string iconText=Marshal.PtrToStringUni(icon); Marshal.FreeCoTaskMem(icon);
            command.GetCanonicalName(out name); command.GetState(IntPtr.Zero,false,out state); command.GetFlags(out flags);
            if(text!="设置中文显示名…" || name!=clsid || state!=2 || flags!=0) throw new Exception("Unexpected command contract.");
            if(command.Invoke(IntPtr.Zero,IntPtr.Zero)>=0) throw new Exception("Null selection was accepted.");
            Console.WriteLine("Title="+text); Console.WriteLine("Icon="+iconText); Console.WriteLine("PASS: title, icon, CLSID, hidden null selection, flags and null Invoke.");
            if(args.Length>=3) {
                IntPtr folder,file; uint attributes;
                Marshal.ThrowExceptionForHR(SHParseDisplayName(args[1],IntPtr.Zero,out folder,0,out attributes));
                Marshal.ThrowExceptionForHR(SHParseDisplayName(args[2],IntPtr.Zero,out file,0,out attributes));
                try {
                    CheckState(command,new[]{folder},0,"one real folder");
                    CheckState(command,new[]{folder,folder},0,"multiple real folders");
                    CheckState(command,new[]{file},2,"file selection hidden");
                    CheckState(command,new[]{folder,file},2,"mixed selection hidden");
                    var many=new IntPtr[65]; for(int i=0;i<many.Length;i++) many[i]=folder;
                    CheckState(command,many,1,"selection over limit disabled");
                    if(args.Length==5 && args[3]=="--invoke") {
                        IntPtr second,array;
                        Marshal.ThrowExceptionForHR(SHParseDisplayName(args[4],IntPtr.Zero,out second,0,out attributes));
                        try {
                            Marshal.ThrowExceptionForHR(SHCreateShellItemArrayFromIDLists(2,new[]{folder,second},out array));
                            try { Marshal.ThrowExceptionForHR(command.Invoke(array,IntPtr.Zero)); Console.WriteLine("PASS: Invoke accepted two real folder paths."); }
                            finally { Marshal.Release(array); }
                        } finally { Marshal.FreeCoTaskMem(second); }
                    }
                } finally { Marshal.FreeCoTaskMem(folder);Marshal.FreeCoTaskMem(file); }
            }
            Marshal.ReleaseComObject(command); return 0;
        } catch(Exception ex) {Console.Error.WriteLine(ex);return 1;}
    }
    static void CheckState(ExplorerCommand command,IntPtr[] pidls,uint expected,string label) {
        IntPtr array; Marshal.ThrowExceptionForHR(SHCreateShellItemArrayFromIDLists((uint)pidls.Length,pidls,out array));
        try { uint state; command.GetState(array,false,out state); if(state!=expected) throw new Exception(label+": unexpected state "+state); Console.WriteLine("PASS: "+label); }
        finally { Marshal.Release(array); }
    }
}
