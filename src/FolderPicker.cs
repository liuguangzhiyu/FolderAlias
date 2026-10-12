using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FolderAlias
{
    // Windows common item dialog, folder mode. No extra library or Explorer hooks.
    internal static class FolderPicker
    {
        internal const uint FolderOptions = 0x20 | 0x40 | 0x200 | 0x800;
        internal static IOpenDialog Create()
        {
            var dialog = (IOpenDialog)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")));
            try
            {
                uint options; dialog.GetOptions(out options); dialog.SetOptions(options | FolderOptions);
                dialog.SetTitle("添加文件夹 · 按住 Ctrl 或 Shift 可一次多选");
                dialog.SetOkButtonLabel("添加选中的文件夹");
                return dialog;
            }
            catch { Marshal.FinalReleaseComObject(dialog); throw; }
        }
        internal static string[] Show(IntPtr owner)
        {
            var dialog = Create();
            try
            {
                int result = dialog.Show(owner);
                if (result == unchecked((int)0x800704C7)) return new string[0];
                Marshal.ThrowExceptionForHR(result);
                IShellItems items; dialog.GetResults(out items);
                try { return ReadPaths(items); }
                finally { Marshal.FinalReleaseComObject(items); }
            }
            finally { Marshal.FinalReleaseComObject(dialog); }
        }
        internal static string[] ReadPaths(IShellItems items)
        {
            uint count; items.GetCount(out count); var paths = new List<string>();
            for (uint index = 0; index < count; index++)
            {
                IShellItem item; items.GetItemAt(index, out item);
                try
                {
                    IntPtr name; item.GetDisplayName(0x80058000, out name); // SIGDN_FILESYSPATH
                    try { paths.Add(Marshal.PtrToStringUni(name)); }
                    finally { Marshal.FreeCoTaskMem(name); }
                }
                finally { Marshal.FinalReleaseComObject(item); }
            }
            return paths.ToArray();
        }
    }

    [ComImport, Guid("D57C7288-D4AD-4768-BE02-9D969532D960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IOpenDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint count, IntPtr filters);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(IntPtr events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(uint options);
        void GetOptions(out uint options);
        void SetDefaultFolder(IShellItem item);
        void SetFolder(IShellItem item);
        void GetFolder(out IShellItem item);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName(out IntPtr name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void GetResult(out IShellItem item);
        void AddPlace(IShellItem item, uint placement);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
        void Close(int result);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr filter);
        void GetResults(out IShellItems items);
        void GetSelectedItems(out IShellItems items);
    }
    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItem
    {
        void BindToHandler(IntPtr bind, ref Guid handler, ref Guid iid, out IntPtr value);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint type, out IntPtr name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }
    [ComImport, Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItems
    {
        void BindToHandler(IntPtr bind, ref Guid handler, ref Guid iid, out IntPtr value);
        void GetPropertyStore(uint flags, ref Guid iid, out IntPtr store);
        void GetPropertyDescriptionList(IntPtr key, ref Guid iid, out IntPtr descriptions);
        void GetAttributes(uint flags, uint mask, out uint attributes);
        void GetCount(out uint count);
        void GetItemAt(uint index, out IShellItem item);
        void EnumItems(out IntPtr enumeration);
    }
}
