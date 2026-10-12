using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
using System.Runtime.Versioning;

[assembly: AssemblyTitle("文件夹中文别名")]
[assembly: AssemblyDescription("Offline folder display aliases with backups and conflict detection")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]
[assembly: TargetFramework(".NETFramework,Version=v4.8")]

namespace FolderAlias
{
    internal static class Program
    {
        static string report;
        static StreamWriter reportWriter;
        [STAThread]
        static int Main(string[] originalArgs)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                var args = originalArgs.ToList();
                int reportIndex = args.IndexOf("--report");
                if (reportIndex >= 0)
                {
                    if (reportIndex + 1 >= args.Count) throw new ArgumentException("--report 缺少输出文件路径。");
                    report = args[reportIndex + 1]; args.RemoveRange(reportIndex, 2);
                    if (!String.Equals(Path.GetExtension(report), ".txt", StringComparison.OrdinalIgnoreCase) &&
                        !String.Equals(Path.GetExtension(report), ".log", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("报告请使用新的 .txt 或 .log 文件。");
                    // Reserve the new report before any mutation; an existing/unwritable report must not cause a partial command.
                    reportWriter = new StreamWriter(new FileStream(report, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(true));
                }
                if (args.Count == 0 || args[0] == "--open")
                { Application.Run(new MainForm(args.Skip(args.Count == 0 ? 0 : 1))); return 0; }
                var engine = new AliasEngine();
                string message;
                switch (args[0])
                {
                    case "--inspect":
                        Require(args.Count == 2); message = engine.Inspect(args[1]).ToString(); break;
                    case "--apply":
                        Require(args.Count == 4 && args[3] == "--yes"); message = engine.Apply(args[1], args[2]); break;
                    case "--restore":
                        Require(args.Count == 3 && args[2] == "--yes"); message = engine.Restore(args[1]); break;
                    case "--recover":
                        Require(args.Count == 3 && args[2] == "--yes"); message = engine.Recover(args[1]); break;
                    case "--refresh":
                        Require(args.Count == 2); Native.Refresh(SafePath.Folder(args[1])); message = engine.Inspect(args[1]).ToString(); break;
                    case "--install-menu":
                        Require(args.Count == 2 && args[1] == "--yes"); MenuIntegration.Install(Application.ExecutablePath); message = "已安装当前用户右键菜单。"; break;
                    case "--uninstall-menu":
                        Require(args.Count == 2 && args[1] == "--yes"); MenuIntegration.Uninstall(); message = "已卸载右键菜单。别名和备份保留。"; break;
                    case "--menu-status":
                        Require(args.Count == 1); message = MenuIntegration.Status(); break;
                    case "--preview":
                        Require(args.Count >= 4 && (args.Count - 2) % 2 == 0);
                        using (var form = new MainForm(new string[0]))
                        {
                            for (int index = 2; index < args.Count; index += 2) form.AddFolder(args[index], args[index + 1]);
                            form.RenderPreview(args[1]);
                        }
                        message = "预览已保存。"; break;
                    case "--help": message = Help; break;
                    default: throw new ArgumentException("未知参数。\r\n" + Help);
                }
                Output(message, false); return 0;
            }
            catch (Exception ex)
            {
                try { Output("未完成：" + ex.Message, true); }
                catch { /* Preserve failure exit code if the requested report cannot be written. */ }
                return 1;
            }
            finally { if (reportWriter != null) reportWriter.Dispose(); }
        }
        static void Require(bool valid) { if (!valid) throw new ArgumentException("参数不正确；修改命令需要明确的 --yes。\r\n" + Help); }
        static void Output(string value, bool error)
        {
            if (report != null)
            {
                if (reportWriter != null) { reportWriter.WriteLine(value); reportWriter.Flush(); }
                else Console.Error.WriteLine(value);
            }
            else MessageBox.Show(value, error ? "FolderAlias · 未完成" : "FolderAlias", MessageBoxButtons.OK, error ? MessageBoxIcon.Information : MessageBoxIcon.None);
        }
        const string Help = "FolderAlias.exe [--open 文件夹 ...]\r\n" +
            "--inspect 文件夹\r\n--apply 文件夹 显示名 --yes\r\n--restore 文件夹 --yes\r\n--recover 文件夹 --yes\r\n--refresh 文件夹\r\n" +
            "--install-menu --yes\r\n--uninstall-menu --yes\r\n--menu-status\r\n" +
            "自动化命令可附加 --report 输出文件，写入结果并返回退出码；0 成功，1 失败。";
    }
}
