using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace FolderAlias.Setup
{
    public sealed class InstalledFile { public string Name; public string Hash; }
    public sealed class InstallState
    {
        public string Product = SetupEngine.Product;
        public string Version = "1.2.0";
        public string Directory;
        public string Mode;
        public string PreviousMenuCommand;
        public bool CreatedDirectory;
        public List<InstalledFile> Files = new List<InstalledFile>();
    }
    internal static class SetupEngine
    {
        internal const string Product = "FolderAlias.Local.Setup.v1";
        internal const string Settings = @"Software\FolderAlias.Local\Installer";
        internal const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\FolderAlias.Local";
        internal const string PackageName = "FolderAlias.Local.Menu";
        internal const string Publisher = "CN=FolderAlias Local";
        internal const string StateName = "install-state.xml";
        internal const string SetupName = "FolderAlias.Setup.exe";
        internal static string Self { get { return Assembly.GetExecutingAssembly().Location; } }
        internal static byte[] Resource(string name)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (stream == null) throw new IOException("安装包缺少内嵌资源：" + name);
                using (var output = new MemoryStream()) { stream.CopyTo(output); return output.ToArray(); }
            }
        }
        internal static string Hash(string path)
        { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""); }
        internal static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        internal static string Quote(string value)
        { if (value.IndexOf('"') >= 0 || value.EndsWith("\\")) throw new ArgumentException("路径参数无效。"); return "\"" + value + "\""; }
        internal static string PSLiteral(string value) { return "'" + value.Replace("'", "''") + "'"; }
        internal static string PowerShell(string script)
        {
            string prelude = "$ErrorActionPreference='Stop'; [Console]::OutputEncoding=[Text.UTF8Encoding]::new($false); try { ";
            string command = prelude + script + " } catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }";
            var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\WindowsPowerShell\v1.0\powershell.exe"),
                "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(command)))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
            using (var process = Process.Start(info))
            {
                var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                process.WaitForExit(); string text = output.Result.Trim(), errors = error.Result.Trim();
                if (process.ExitCode != 0) throw new IOException("Windows 未完成配置：" + errors + "\r\n" + text);
                return text;
            }
        }
        internal static string PackageQuery
        { get { return "@(Get-AppxPackage -Name " + PSLiteral(PackageName) + " | Where-Object { $_.Publisher -eq " + PSLiteral(Publisher) + " })"; } }
        internal static bool PackagePresent() { return PowerShell(PackageQuery + ".Count").Trim() != "0"; }
        internal static void RegisterPackage(string path)
        {
            PowerShell("Add-AppxPackage -Path " + PSLiteral(Path.Combine(path, "FolderAlias.Identity.msix")) + " -ExternalLocation " + PSLiteral(path) + "; if (" + PackageQuery + ".Count -ne 1) { throw '无法确认菜单包已注册。' }");
            VerifyCommand.Activate();
        }
        internal static void RemovePackage()
        { PowerShell(PackageQuery + " | ForEach-Object { Remove-AppxPackage -Package $_.PackageFullName }; if (" + PackageQuery + ".Count -ne 0) { throw '菜单包仍存在，文件已保留。' }"); }
        internal static void AssertNoReparse(string path)
        {
            for (string current = Path.GetFullPath(path); !String.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("为避免写入链接目标，请选择不含链接或联接的普通目录：" + current);
        }
        internal static string ValidateDirectory(string input)
        {
            if (String.IsNullOrWhiteSpace(input) || input.Length < 4 || input[1] != ':' || input[2] != '\\' || input.IndexOf('"') >= 0)
                throw new IOException("请选择本地磁盘上的完整安装路径。");
            string path = Path.GetFullPath(input).TrimEnd('\\');
            if (path.Length > 170 || path.Length < 4 || path.IndexOf(':', 2) >= 0) throw new IOException("安装路径过长或无效（最多 170 个字符）。");
            foreach (string part in path.Substring(3).Split('\\'))
                if (part.EndsWith(".") || part.EndsWith(" ")) throw new IOException("路径不能以空格或句点结尾。");
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (path.Equals(windows, StringComparison.OrdinalIgnoreCase) || path.StartsWith(windows + "\\", StringComparison.OrdinalIgnoreCase)) throw new IOException("不能安装到 Windows 系统目录。");
            var drive = new DriveInfo(Path.GetPathRoot(path));
            if (drive.DriveType != DriveType.Fixed || drive.DriveFormat != "NTFS") throw new IOException("请选择本地 NTFS 固定磁盘上的目录。");
            AssertNoReparse(path); return path;
        }
        internal static string EntryPath(string root, string name)
        {
            if (String.IsNullOrEmpty(name) || name.IndexOfAny(new[] { ':', '\\' }) >= 0 || name.StartsWith("/") || name.EndsWith("/")) throw new IOException("安装包文件名无效。");
            foreach (string part in name.Split('/'))
                if (part == "" || part == "." || part == ".." || part.EndsWith(".") || part.EndsWith(" ")) throw new IOException("安装包路径无效。");
            string path = Path.GetFullPath(Path.Combine(root, name.Replace('/', '\\')));
            if (!path.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) throw new IOException("安装包试图越出安装目录。");
            AssertNoReparse(path); return path;
        }
        internal static string InstalledDirectory()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(Settings))
            {
                if (key == null) return null;
                if ((string)key.GetValue("Owner", "") != Product) throw new IOException("同名安装记录不属于本工具。");
                return (string)key.GetValue("Path", "");
            }
        }
        static void Save(string root, InstallState state)
        {
            string path = Path.Combine(root, StateName), temporary = path + ".new";
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { new XmlSerializer(typeof(InstallState)).Serialize(stream, state); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
        internal static InstallState ReadState(string root)
        {
            root = ValidateDirectory(root); string path = Path.Combine(root, StateName); AssertNoReparse(path);
            if (new FileInfo(path).Length > 65536) throw new IOException("安装记录过大。");
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                var state = (InstallState)new XmlSerializer(typeof(InstallState)).Deserialize(reader);
                if (state.Product != Product || !String.Equals(state.Directory, root, StringComparison.OrdinalIgnoreCase) || (state.Mode != "modern" && state.Mode != "classic") || state.Files.Count > 64)
                    throw new IOException("安装记录与目录不匹配，已停止。");
                var allowed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                using (var archive = new ZipArchive(new MemoryStream(Resource("Payload.zip")), ZipArchiveMode.Read))
                    foreach (var entry in archive.Entries)
                        using (var source = entry.Open()) using (var bytes = new MemoryStream()) { source.CopyTo(bytes); allowed.Add(entry.FullName, Hash(bytes.ToArray())); }
                allowed.Add(SetupName, Hash(Self));
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in state.Files)
                { EntryPath(root, file.Name); if (!names.Add(file.Name) || !allowed.ContainsKey(file.Name) || file.Hash != allowed[file.Name]) throw new IOException("安装记录与本版本内置文件不匹配，已停止。"); }
                return state;
            }
        }
        static void RecordFile(InstallState state, string root, string name)
        { state.Files.Add(new InstalledFile { Name = name, Hash = Hash(EntryPath(root, name)) }); Save(root, state); }
        internal static void Extract(byte[] payload, string root, InstallState state)
        {
            using (var zip = new ZipArchive(new MemoryStream(payload), ZipArchiveMode.Read))
            {
                if (zip.Entries.Count > 64) throw new IOException("安装包文件数量无效。");
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long size = 0;
                foreach (var entry in zip.Entries)
                {
                    string path = EntryPath(root, entry.FullName); size += entry.Length;
                    if (!seen.Add(entry.FullName) || size > 16 * 1024 * 1024 || entry.Length > 8 * 1024 * 1024 || entry.FullName == StateName || entry.FullName == SetupName)
                        throw new IOException("安装包结构无效。");
                    if (File.Exists(path) || Directory.Exists(path)) throw new IOException("目标已存在，未覆盖：" + path);
                }
                foreach (var entry in zip.Entries)
                {
                    string path = EntryPath(root, entry.FullName); Directory.CreateDirectory(Path.GetDirectoryName(path));
                    bool created = false;
                    try
                    {
                        using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        { created = true; using (var input = entry.Open()) input.CopyTo(output); output.Flush(true); }
                        RecordFile(state, root, entry.FullName);
                    }
                    catch { if (created && !state.Files.Any(f => f.Name == entry.FullName)) File.Delete(path); throw; }
                }
            }
        }
        static void RegisterUninstall(string root)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                key.SetValue("Owner", Product); key.SetValue("DisplayName", "文件夹中文别名 · FolderAlias"); key.SetValue("DisplayVersion", "1.2.0");
                key.SetValue("Publisher", "FolderAlias Local（个人分享版）"); key.SetValue("InstallLocation", root);
                key.SetValue("UninstallString", Quote(Path.Combine(root, SetupName)) + " --uninstall");
                key.SetValue("DisplayIcon", Path.Combine(root, "FolderAlias.exe")); key.SetValue("NoModify", 1); key.SetValue("NoRepair", 1);
            }
            using (var key = Registry.CurrentUser.CreateSubKey(Settings)) { key.SetValue("Owner", Product); key.SetValue("Path", root); }
        }
        internal static string Install(string input, bool modern, bool consent, Action<string> progress)
        {
            if (InstalledDirectory() != null) throw new IOException("当前用户已经安装；请先卸载旧版，再选择新位置安装。文件夹别名与备份会保留。");
            string root = ValidateDirectory(input);
            if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()) throw new IOException("请选择新的或空的安装目录，避免覆盖已有文件。");
            if (File.Exists(root)) throw new IOException("安装路径已经是一个文件。");
            using (var key = Registry.CurrentUser.OpenSubKey(UninstallKey)) if (key != null) throw new IOException("同名卸载记录已存在，请先处理原安装。");
            string previous = MenuIntegration.Status();
            if (modern && PackagePresent()) throw new IOException("当前用户已有本工具的现代菜单包，请先从原安装位置卸载。");
            if (modern && !Trust.IsPresent() && !consent) throw new IOException("第一层菜单需要先同意安装本工具的专用签名证书。");
            var state = new InstallState { Directory = root, Mode = modern ? "modern" : "classic", CreatedDirectory = !Directory.Exists(root), PreviousMenuCommand = previous == "未安装" ? null : previous };
            bool registered = false, menuTouched = false, trustAdded = false;
            Directory.CreateDirectory(root); Save(root, state);
            try
            {
                progress("正在解压内置文件…"); Extract(Resource("Payload.zip"), root, state);
                File.Copy(Self, Path.Combine(root, SetupName), false); RecordFile(state, root, SetupName);
                if (modern)
                {
                    if (!Trust.IsPresent())
                    {
                        progress("请在 Windows 管理员提示中确认专用证书安装…");
                        if (Trust.RunElevated(false) != 0 || !Trust.IsPresent()) throw new IOException("未完成证书信任；没有降低 Windows 安全策略。");
                        trustAdded = true;
                    }
                    progress("正在为当前用户注册第一层右键菜单…"); RegisterPackage(root); registered = true;
                    menuTouched = true; MenuIntegration.Uninstall();
                }
                else { progress("正在配置当前用户普通右键菜单…"); menuTouched = true; MenuIntegration.Install(Path.Combine(root, "FolderAlias.exe")); }
                RegisterUninstall(root); Native.RefreshMenu();
                return modern ? "安装完成。右键普通文件夹 → 设置中文显示名…\r\n若菜单尚未刷新，请关闭并重开资源管理器窗口；必要时注销再登录。菜单位置由 Windows 决定。" : "安装完成。右键文件夹 → 显示更多选项 → 设置中文显示名…";
            }
            catch (Exception error)
            {
                var notes = new List<string>(); bool clean = true;
                try { if (registered || (modern && PackagePresent())) RemovePackage(); } catch (Exception ex) { clean = false; notes.Add(ex.Message); }
                if (clean)
                {
                    try { if (menuTouched) RestorePreviousMenu(state, root); } catch (Exception ex) { clean = false; notes.Add(ex.Message); }
                    if (clean)
                    {
                        RemoveRegistryIfOwned(root); notes.AddRange(DeleteOwnedFiles(root, state));
                        if (File.Exists(Path.Combine(root, StateName))) notes.Add("部分文件未移除，安装记录已保留：" + Path.Combine(root, StateName));
                    }
                }
                if (trustAdded) notes.Add("专用证书已加入；可使用安装窗口的“证书说明 / 清理”移除。未自动触发第二次管理员提示。");
                throw new IOException(error.Message + "\r\n" + String.Join("\r\n", notes));
            }
        }
        static void RestorePreviousMenu(InstallState state, string root)
        {
            string current = MenuIntegration.Status();
            if (current == MenuIntegration.BuildCommand(Path.Combine(root, "FolderAlias.exe"))) { MenuIntegration.Uninstall(); current = "未安装"; }
            if (current != "未安装" || String.IsNullOrEmpty(state.PreviousMenuCommand)) return;
            string old = state.PreviousMenuCommand;
            const string suffix = "\" --open \"%1\"";
            if (old.StartsWith("\"") && old.EndsWith(suffix))
            { string exe = old.Substring(1, old.Length - suffix.Length - 1); if (File.Exists(exe) && MenuIntegration.BuildCommand(exe) == old) MenuIntegration.Install(exe); }
        }
        static void RemoveRegistryIfOwned(string root)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(UninstallKey))
                if (key != null && (string)key.GetValue("Owner", "") == Product && (string)key.GetValue("InstallLocation", "") == root) Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
            using (var key = Registry.CurrentUser.OpenSubKey(Settings))
                if (key != null && (string)key.GetValue("Owner", "") == Product && (string)key.GetValue("Path", "") == root) Registry.CurrentUser.DeleteSubKeyTree(Settings, false);
        }
        internal static List<string> DeleteOwnedFiles(string root, InstallState state)
        {
            var notes = new List<string>(); var parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in state.Files.OrderBy(f => f.Name == SetupName ? 1 : 0))
            {
                try
                {
                    if (file.Name == SetupName && notes.Count != 0) { notes.Add("卸载工具和安装记录已保留，便于稍后重试。"); continue; }
                    string path = EntryPath(root, file.Name); parents.Add(Path.GetDirectoryName(path));
                    if (!File.Exists(path)) continue;
                    if (Hash(path) != file.Hash) { notes.Add("保留已被修改的文件：" + file.Name); continue; }
                    File.Delete(path);
                }
                catch (Exception ex) { notes.Add("保留文件 " + file.Name + "：" + ex.Message); }
            }
            // Never recurse: foreign files, user backups and changed files always survive.
            foreach (string parent in parents.OrderByDescending(p => p.Length))
                if (parent != root && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent, false);
            if (notes.Count == 0)
            {
                File.Delete(Path.Combine(root, StateName));
                if (state.CreatedDirectory && Directory.Exists(root) && !Directory.EnumerateFileSystemEntries(root).Any()) Directory.Delete(root, false);
            }
            return notes;
        }
        internal static string Uninstall(string input, bool removeTrust, Action<string> progress)
        {
            string root = ValidateDirectory(input); var state = ReadState(root);
            // Do not remove another installation's registration if this directory was copied.
            string registeredPath = InstalledDirectory();
            if (registeredPath != null && !String.Equals(root, registeredPath, StringComparison.OrdinalIgnoreCase)) throw new IOException("另一个目录是当前注册位置；未修改菜单。");
            progress("正在移除当前用户的菜单…");
            if (state.Mode == "modern") RemovePackage();
            RestorePreviousMenu(state, root); RemoveRegistryIfOwned(root); Native.RefreshMenu();
            progress("正在删除本工具安装的文件…"); var notes = DeleteOwnedFiles(root, state);
            if (removeTrust)
            {
                progress("请确认移除不再使用的专用证书…");
                try { int code = Trust.RunElevated(true); if (code != 0) notes.Add(code == 12 ? "其他账户仍使用此菜单包，证书已保留。" : "证书未移除（可能不是本工具导入，或管理员操作被取消）。"); }
                catch (Exception ex) { notes.Add("证书已保留：" + ex.Message); }
            }
            return "已卸载菜单和可移除的程序文件。已有中文别名与备份保留。\r\n" + String.Join("\r\n", notes);
        }
    }

    internal static class Trust
    {
        internal static X509Certificate2 Certificate() { return new X509Certificate2(SetupEngine.Resource("Publisher.cer")); }
        internal static string Thumbprint { get { using (var cert = Certificate()) return cert.Thumbprint; } }
        internal static bool IsPresent()
        {
            using (var cert = Certificate()) using (var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine))
            { store.Open(OpenFlags.ReadOnly); return store.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, false).Count != 0; }
        }
        internal static bool IsAdmin() { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
        internal static int RunElevated(bool remove)
        {
            var info = new ProcessStartInfo(SetupEngine.Self, remove ? "--trust-remove" : "--trust-add") { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
            using (var child = Process.Start(info)) { child.WaitForExit(); return child.ExitCode; }
        }
        internal static int Elevated(bool remove)
        {
            if (!IsAdmin()) return 5;
            using (var cert = Certificate())
            {
                if (cert.HasPrivateKey || cert.Subject != SetupEngine.Publisher) throw new IOException("内嵌证书身份无效。");
                string keyPath = @"Software\FolderAlias.Local\Certificates\" + cert.Thumbprint;
                using (var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine))
                {
                    store.Open(OpenFlags.ReadWrite);
                    var found = store.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, false);
                    if (!remove)
                    {
                        if (DateTime.Now < cert.NotBefore || DateTime.Now > cert.NotAfter) throw new IOException("安装包签名证书不在有效期内，请使用新版本。");
                        if (found.Count != 0) return 0;
                        using (var marker = Registry.LocalMachine.CreateSubKey(keyPath)) marker.SetValue("Owner", SetupEngine.Product);
                        try { store.Add(cert); }
                        catch { Registry.LocalMachine.DeleteSubKeyTree(keyPath, false); throw; }
                        return 0;
                    }
                    using (var marker = Registry.LocalMachine.OpenSubKey(keyPath))
                        if (marker == null || (string)marker.GetValue("Owner", "") != SetupEngine.Product) return found.Count == 0 ? 0 : 13;
                    string count = SetupEngine.PowerShell("@(Get-AppxPackage -AllUsers -Name " + SetupEngine.PSLiteral(SetupEngine.PackageName) + " | Where-Object { $_.Publisher -eq " + SetupEngine.PSLiteral(SetupEngine.Publisher) + " }).Count");
                    if (count.Trim() != "0") return 12;
                    foreach (var item in found) store.Remove(item);
                    Registry.LocalMachine.DeleteSubKeyTree(keyPath, false); return 0;
                }
            }
        }
    }
}
