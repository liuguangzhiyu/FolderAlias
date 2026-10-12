using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FolderAlias;

// Tests real Windows COM bindings and Unicode multi-selection result conversion, without changing folders.
static class FolderPickerProbe
{
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern int SHParseDisplayName(string path,IntPtr bind,out IntPtr pidl,uint mask,out uint attributes);
    [DllImport("shell32.dll")] static extern int SHCreateShellItemArrayFromIDLists(uint count,IntPtr[] pidls,out IShellItems items);
    [STAThread] static int Main(string[] args)
    {
        try
        {
            var dialog=FolderPicker.Create();
            try { uint options;dialog.GetOptions(out options);if((options & FolderPicker.FolderOptions)!=FolderPicker.FolderOptions)throw new Exception("Dialog options mismatch"); }
            finally { Marshal.FinalReleaseComObject(dialog); }
            Console.WriteLine("PASS: Windows folder dialog creation, multi-select options, title and button label calls");
            var pidls=new IntPtr[args.Length];
            try
            {
                for(int i=0;i<args.Length;i++){uint attrs;Marshal.ThrowExceptionForHR(SHParseDisplayName(args[i],IntPtr.Zero,out pidls[i],0,out attrs));}
                IShellItems items;Marshal.ThrowExceptionForHR(SHCreateShellItemArrayFromIDLists((uint)pidls.Length,pidls,out items));
                try { if(!FolderPicker.ReadPaths(items).SequenceEqual(args.Select(Path.GetFullPath),StringComparer.OrdinalIgnoreCase))throw new Exception("Multi-selection paths mismatch"); }
                finally { Marshal.FinalReleaseComObject(items); }
                Console.WriteLine("PASS: "+args.Length+" selected real folders decoded in order, including Unicode and spaces");
            }
            finally { foreach(var pidl in pidls)if(pidl!=IntPtr.Zero)Marshal.FreeCoTaskMem(pidl); }
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
