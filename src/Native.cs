using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace FolderAlias
{
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        struct FileInfoNative
        {
            public uint Attributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
            public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct StreamData
        {
            public long Size;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)] public string Name;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInfoNative info);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr FindFirstStreamW(string path, int level, out StreamData data, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool FindNextStreamW(IntPtr handle, out StreamData data);
        [DllImport("kernel32.dll")] static extern bool FindClose(IntPtr handle);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern void SHChangeNotify(uint eventId, uint flags, string first, string second);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHParseDisplayName(string name, IntPtr binding, out IntPtr pidl, uint mask, out uint attributes);
        [DllImport("shell32.dll")]
        static extern int SHGetNameFromIDList(IntPtr pidl, uint type, out IntPtr name);

        public static string Hash(byte[] data)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "");
        }
        public static string Identity(string path, bool directory)
        {
            using (var handle = CreateFile(path, 0, 7, IntPtr.Zero, 3, directory ? 0x02000000u : 0u, IntPtr.Zero))
            {
                FileInfoNative info;
                if (handle.IsInvalid || !GetFileInformationByHandle(handle, out info))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!directory && info.Links != 1) throw new IOException("desktop.ini 或备份是硬链接，已停止修改。");
                return info.Volume.ToString("X8") + ":" + info.IndexHigh.ToString("X8") + info.IndexLow.ToString("X8");
            }
        }
        public static void CheckStreams(string path)
        {
            StreamData data;
            var handle = FindFirstStreamW(path, 0, out data, 0);
            if (handle == new IntPtr(-1))
            {
                int error = Marshal.GetLastWin32Error();
                if (error == 38) return;
                throw new Win32Exception(error, "无法检查文件的附加数据流。");
            }
            try
            {
                do
                {
                    if (!String.Equals(data.Name, "::$DATA", StringComparison.OrdinalIgnoreCase))
                        throw new IOException("desktop.ini 或备份带有附加数据流（可能是下载来源标记），为保留安全信息已停止修改。");
                } while (FindNextStreamW(handle, out data));
                if (Marshal.GetLastWin32Error() != 38) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally { FindClose(handle); }
        }
        public static void Refresh(string path)
        {
            const uint flags = 0x0005 | 0x3000; // SHCNF_PATHW | SHCNF_FLUSHNOWAIT
            SHChangeNotify(0x00000800, flags, path, null); // attributes
            SHChangeNotify(0x00002000, flags, path, null); // update item
            string parent = Path.GetDirectoryName(path);
            if (!String.IsNullOrEmpty(parent)) SHChangeNotify(0x00001000, flags, parent, null);
        }
        public static void RefreshMenu() { SHChangeNotify(0x08000000, 0, null, null); }
        public static string DisplayName(string path)
        {
            IntPtr pidl = IntPtr.Zero, name = IntPtr.Zero;
            try
            {
                uint attributes;
                Marshal.ThrowExceptionForHR(SHParseDisplayName(path, IntPtr.Zero, out pidl, 0, out attributes));
                Marshal.ThrowExceptionForHR(SHGetNameFromIDList(pidl, 0, out name));
                return Marshal.PtrToStringUni(name);
            }
            finally
            {
                if (name != IntPtr.Zero) Marshal.FreeCoTaskMem(name);
                if (pidl != IntPtr.Zero) Marshal.FreeCoTaskMem(pidl);
            }
        }
    }

    internal static class SafePath
    {
        public const string BackupName = ".FolderAlias.Backup";
        public static string Folder(string input)
        {
            if (String.IsNullOrWhiteSpace(input) || input.Length < 3 || !Char.IsLetter(input[0]) || input[1] != ':' || input[2] != '\\')
                throw new IOException("请选择本机盘符下的绝对路径，例如 E:\\Tools\\EcoPaste。");
            if (input.Substring(2).Contains(":")) throw new IOException("不支持数据流路径。");
            string path = Path.GetFullPath(input).TrimEnd('\\');
            if (path.Length < 4 || path.Length > 180) throw new IOException("不处理磁盘根目录；当前版本要求文件夹完整路径不超过 180 个字符。");
            var drive = new DriveInfo(Path.GetPathRoot(path));
            if (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable)
                throw new IOException("当前版本仅处理本机固定磁盘或可移动磁盘，不访问网络路径。");
            if (!String.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
                throw new IOException("当前版本只支持 NTFS，以可靠保存配置、属性和备份。");
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException("文件夹不存在：" + path);
            CheckAncestors(path);
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
            if (path.Equals(windows, StringComparison.OrdinalIgnoreCase) || path.StartsWith(windows + "\\", StringComparison.OrdinalIgnoreCase))
                throw new IOException("不修改 Windows 系统目录。");
            foreach (string part in path.Split('\\'))
            {
                if (part.EndsWith(".") || part.EndsWith(" ")) throw new IOException("不处理末尾带空格或句点的路径。");
                if (part.Equals(BackupName, StringComparison.OrdinalIgnoreCase)) throw new IOException("不能把本工具的备份目录设为目标。");
            }
            return path;
        }
        public static void CheckAncestors(string path)
        {
            for (var dir = new DirectoryInfo(path); dir != null; dir = dir.Parent)
                if ((dir.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("路径包含链接、联接或重解析点，已停止修改：" + dir.FullName);
        }
        public static void RegularFile(string path)
        {
            if (Directory.Exists(path)) throw new IOException("预期文件的位置存在同名目录：" + path);
            if (!File.Exists(path)) return;
            var attrs = File.GetAttributes(path);
            if ((attrs & (FileAttributes.ReparsePoint | FileAttributes.Encrypted | FileAttributes.Offline)) != 0)
                throw new IOException("不处理链接、加密或脱机配置文件。");
            Native.Identity(path, false);
            Native.CheckStreams(path);
        }
    }
}
