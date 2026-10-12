using System;
using System.Runtime.InteropServices;

namespace FolderAlias.Setup
{
    [ComImport, Guid("A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface CommandTitle { void GetTitle(IntPtr items, out IntPtr text); }
    internal static class VerifyCommand
    {
        [DllImport("ole32.dll")] static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint context, ref Guid iid, out IntPtr value);
        internal static void Activate()
        {
            Guid clsid = new Guid("ED3017E2-5A5A-4CD5-A714-EC67642842A7"), iid = typeof(CommandTitle).GUID;
            IntPtr pointer; Marshal.ThrowExceptionForHR(CoCreateInstance(ref clsid, IntPtr.Zero, 4, ref iid, out pointer));
            var command = (CommandTitle)Marshal.GetObjectForIUnknown(pointer); Marshal.Release(pointer);
            try
            {
                IntPtr title; command.GetTitle(IntPtr.Zero, out title);
                try { if (Marshal.PtrToStringUni(title) != "设置中文显示名…") throw new InvalidOperationException("原生菜单组件验证未通过。"); }
                finally { Marshal.FreeCoTaskMem(title); }
            }
            finally { Marshal.ReleaseComObject(command); }
        }
    }
}
