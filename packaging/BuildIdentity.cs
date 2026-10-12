using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

[StructLayout(LayoutKind.Sequential)]
struct PackageSettings { public int ForceZip32; public IntPtr HashMethod; }
[ComImport, Guid("beb94909-e451-438b-b5a7-d79e767b75d8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAppxFactory {
    void CreatePackageWriter(IStream output, ref PackageSettings settings, out IAppxPackageWriter writer);
}
[ComImport, Guid("9099e33b-246f-41e4-881a-008eb613f858"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAppxPackageWriter {
    void AddPayloadFile([MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string type, int compression, IStream input);
    void Close(IStream manifest);
}
internal static class BuildIdentity {
    [DllImport("shlwapi.dll",CharSet=CharSet.Unicode,PreserveSig=false)]
    static extern void SHCreateStreamOnFileEx(string path,uint mode,uint attrs,bool create,IntPtr template,out IStream stream);
    [DllImport("urlmon.dll",CharSet=CharSet.Unicode,PreserveSig=false)]
    static extern void CreateUri(string value,uint flags,IntPtr reserved,out IntPtr uri);
    [STAThread]
    static int Main(string[] args) {
        if (args.Length != 2) { Console.Error.WriteLine("BuildIdentity manifest.xml output.msix"); return 2; }
        IStream output=null,manifest=null; IAppxFactory factory=null; IAppxPackageWriter writer=null; IntPtr uri=IntPtr.Zero;
        try {
            SHCreateStreamOnFileEx(Path.GetFullPath(args[1]),0x1011,0,true,IntPtr.Zero,out output);
            SHCreateStreamOnFileEx(Path.GetFullPath(args[0]),0x10,0,false,IntPtr.Zero,out manifest);
            CreateUri("http://www.w3.org/2001/04/xmlenc#sha256",0,IntPtr.Zero,out uri);
            var settings=new PackageSettings { ForceZip32=1,HashMethod=uri };
            factory=(IAppxFactory)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("5842a140-ff9f-4166-8f5c-62f5b7b0c781")));
            factory.CreatePackageWriter(output,ref settings,out writer);
            writer.Close(manifest);
            Console.WriteLine("Created identity-only package: " + args[1]); return 0;
        } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally {
            if(writer!=null) Marshal.ReleaseComObject(writer); if(factory!=null) Marshal.ReleaseComObject(factory);
            if(manifest!=null) Marshal.ReleaseComObject(manifest); if(output!=null) Marshal.ReleaseComObject(output);
            if(uri!=IntPtr.Zero) Marshal.Release(uri);
        }
    }
}
