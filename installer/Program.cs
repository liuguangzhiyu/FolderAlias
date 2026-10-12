using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("文件夹中文别名安装程序")]
[assembly: AssemblyDescription("Offline self-extracting FolderAlias installer")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]
[assembly: TargetFramework(".NETFramework,Version=v4.8")]

namespace FolderAlias.Setup
{
    internal static class Program
    {
        internal static void StartDetachedUninstaller(string root)
        {
            SetupEngine.ReadState(root);
            string temp = Path.Combine(Path.GetTempPath(), "FolderAlias-Uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(SetupEngine.Self, temp, false);
            Process.Start(new ProcessStartInfo(temp, "--uninstall-from " + SetupEngine.Quote(root) + " --wait-pid " + Process.GetCurrentProcess().Id) { UseShellExecute = true });
        }
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                if (args.Length == 1 && (args[0] == "--trust-add" || args[0] == "--trust-remove")) return Trust.Elevated(args[0] == "--trust-remove");
                if (args.Length == 4 && args[0] == "--uninstall-from" && args[2] == "--wait-pid")
                {
                    try { using (var parent = Process.GetProcessById(Int32.Parse(args[3]))) { if (!parent.WaitForExit(15000)) throw new IOException("原安装窗口未关闭。"); } }
                    catch (ArgumentException) { }
                    args = new[] { args[0], args[1] };
                }
                if (!Trust.IsAdmin() && File.Exists(Path.Combine(Path.GetDirectoryName(SetupEngine.Self), SetupEngine.StateName)) &&
                    (args.Length == 0 || (args.Length == 1 && args[0] == "--uninstall")))
                { StartDetachedUninstaller(Path.GetDirectoryName(SetupEngine.Self)); return 0; }
                bool acquired;
                using (var mutex = new Mutex(true, "Local\\FolderAlias.Setup." + System.Security.Principal.WindowsIdentity.GetCurrent().User.Value, out acquired))
                {
                    if (!acquired) throw new IOException("已有安装或卸载窗口正在运行，请先关闭它。");
                    try
                    {
                        if (Environment.OSVersion.Version.Build < 22000 || !Environment.Is64BitOperatingSystem) throw new IOException("此版本面向 Windows 11 x64。");
                        using (var hardware = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment"))
                            if (hardware == null || !String.Equals((string)hardware.GetValue("PROCESSOR_ARCHITECTURE", ""), "AMD64", StringComparison.OrdinalIgnoreCase)) throw new IOException("此安装包仅支持 x64 系统，不支持 ARM64。");
                        if (Trust.IsAdmin()) throw new IOException("请普通双击安装包，不要对整个安装程序选择“以管理员身份运行”。仅证书步骤会单独请求管理员权限，菜单注册给当前用户。");
                        if (args.Length == 1 && args[0] == "--uninstall")
                        { StartDetachedUninstaller(Path.GetDirectoryName(SetupEngine.Self)); return 0; }
                        if (args.Length == 2 && args[0] == "--preview") { using (var form = new SetupForm(null)) form.Preview(args[1]); return 0; }
                        // Explicit test automation; certificate changes are intentionally unavailable here.
                        if (args.Length == 4 && args[0] == "--test-install-classic" && args[2] == "--report")
                        {
                            using (var report = new StreamWriter(new FileStream(args[3], FileMode.CreateNew, FileAccess.Write), new UTF8Encoding(true)))
                            { try { report.WriteLine(SetupEngine.Install(args[1], false, false, report.WriteLine)); return 0; } catch (Exception ex) { report.WriteLine(ex); return 1; } }
                        }
                        if (args.Length == 4 && args[0] == "--test-uninstall" && args[2] == "--report")
                        {
                            using (var report = new StreamWriter(new FileStream(args[3], FileMode.CreateNew, FileAccess.Write), new UTF8Encoding(true)))
                            { try { report.WriteLine(SetupEngine.Uninstall(args[1], false, report.WriteLine)); return 0; } catch (Exception ex) { report.WriteLine(ex); return 1; } }
                        }
                        if (args.Length != 0 && !(args.Length == 2 && args[0] == "--uninstall-from")) throw new ArgumentException("未知参数。");
                        Application.Run(new SetupForm(args.Length == 2 ? args[1] : null)); return 0;
                    }
                    finally { mutex.ReleaseMutex(); }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "FolderAlias · 未完成", MessageBoxButtons.OK, MessageBoxIcon.Information); return 1; }
            finally
            {
                // One short-lived helper removes only this exact temporary uninstaller after it exits.
                // No service, task, Run key or reboot-time deletion is registered.
                if (String.Equals(Path.GetDirectoryName(SetupEngine.Self).TrimEnd('\\'), Path.GetTempPath().TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) &&
                    System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(SetupEngine.Self), "^FolderAlias-Uninstall-[a-f0-9]{32}\\.exe$"))
                {
                    try
                    {
                        string script = "Wait-Process -Id " + Process.GetCurrentProcess().Id + " -ErrorAction SilentlyContinue; Remove-Item -LiteralPath " + SetupEngine.PSLiteral(SetupEngine.Self) + " -ErrorAction SilentlyContinue";
                        Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\WindowsPowerShell\v1.0\powershell.exe"), "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script))) { UseShellExecute = false, CreateNoWindow = true });
                    }
                    catch { /* A locked temporary copy may be removed later through Windows temporary-file cleanup. */ }
                }
            }
        }
    }
}
