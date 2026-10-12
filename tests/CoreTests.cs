using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using FolderAlias;
using Microsoft.Win32.SafeHandles;

internal static class CoreTests
{
    static string root;
    static int passed, failed, counter;
    static string filter;
    static readonly AliasEngine engine = new AliasEngine();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateHardLink(string name, string existing, IntPtr security);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--crash")
        {
            AliasEngine.TestFault = p => { if (p == args[2]) Environment.Exit(77); };
            engine.Apply(args[1], "中断测试（Crash）"); return 2;
        }
        if (args.Length < 1 || args.Length > 2) { Console.WriteLine("Pass a writable test root and optional test-name filter. Existing files are not removed."); return 2; }
        filter = args.Length == 2 ? args[1] : null;
        root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
        Test("new folder / Unicode / original path / restore", delegate
        {
            string dir = Folder(); string payload = Path.Combine(dir, "app.bin"); File.WriteAllBytes(payload, new byte[] { 1, 3, 5, 7 });
            var original = AliasEngine.Capture(dir);
            engine.Apply(dir, "剪贴板工具（EcoPaste）😀");
            var current = AliasEngine.Capture(dir);
            Check(current.Bytes[0] == 255 && current.Bytes[1] == 254, "UTF-16 LE BOM");
            Check(new IniDocument(current.Bytes).Alias == "剪贴板工具（EcoPaste）😀", "Unicode alias");
            Check(current.FolderReadOnly && ((FileAttributes)current.FileAttributes & (FileAttributes.Hidden | FileAttributes.System)) == (FileAttributes.Hidden | FileAttributes.System), "attributes");
            Check(Directory.Exists(dir) && File.ReadAllBytes(payload).SequenceEqual(new byte[] { 1, 3, 5, 7 }), "real files untouched");
            Console.WriteLine("  SHELL=" + Native.DisplayName(dir));
            engine.Restore(dir); Check(AliasEngine.Equivalent(original, AliasEngine.Capture(dir)), "restore original");
            Check(File.Exists(AliasEngine.StatePath(dir)), "retained backup");
        });
        Test("preserve icon, comments, other sections; byte exact restore", delegate
        {
            string dir = Folder(); string ini = Path.Combine(dir, "desktop.ini");
            byte[] original = Encoding.ASCII.GetBytes("; comment\r\n[.ShellClassInfo]\r\nIconResource=C:\\Windows\\system32\\shell32.dll,3\r\n  LocalizedResourceName =Old Name\r\nInfoTip=tip\r\n[ViewState]\r\nMode=\r\nVid=old\r\n");
            File.WriteAllBytes(ini, original);
            File.SetLastWriteTimeUtc(ini, new DateTime(2023, 1, 2, 3, 4, 5, DateTimeKind.Utc));
            File.SetAttributes(ini, FileAttributes.Hidden | FileAttributes.ReadOnly);
            var snapshot = AliasEngine.Capture(dir);
            engine.Apply(dir, "剪贴板工具（EcoPaste）");
            string text = Encoding.Unicode.GetString(File.ReadAllBytes(ini)).TrimStart('\uFEFF');
            Check(text == Encoding.ASCII.GetString(original).Replace("Old Name", "剪贴板工具（EcoPaste）"), "non-target content preserved");
            engine.Restore(dir);
            Check(File.ReadAllBytes(ini).SequenceEqual(original), "original bytes restored");
            Check(AliasEngine.Equivalent(snapshot, AliasEngine.Capture(dir)), "attributes restored");
            Check(File.GetLastWriteTimeUtc(ini).Ticks == snapshot.WriteUtcTicks, "write timestamp restored");
        });
        Test("repeat apply keeps first baseline", delegate
        {
            string dir = Folder(); File.WriteAllText(Path.Combine(dir, "desktop.ini"), "[.ShellClassInfo]\nInfoTip=original", Encoding.ASCII);
            var before = AliasEngine.Capture(dir); engine.Apply(dir, "第一次"); engine.Apply(dir, "第二次"); engine.Restore(dir);
            Check(AliasEngine.Equivalent(before, AliasEngine.Capture(dir)), "first baseline retained");
        });
        Test("reapply after restore creates a new baseline", delegate
        {
            string dir = Folder(); engine.Apply(dir, "第一次"); engine.Restore(dir);
            File.WriteAllText(Path.Combine(dir, "desktop.ini"), "[.ShellClassInfo]\r\nInfoTip=new", Encoding.ASCII);
            var before = AliasEngine.Capture(dir); engine.Apply(dir, "第二次"); engine.Restore(dir);
            Check(AliasEngine.Equivalent(before, AliasEngine.Capture(dir)), "new baseline preserved");
        });
        Test("external configuration edit blocks apply and restore", delegate
        {
            string dir = Folder(); engine.Apply(dir, "别名"); string ini = Path.Combine(dir, "desktop.ini");
            File.AppendAllText(ini, "InfoTip=new by app\r\n", Encoding.Unicode); byte[] changed = File.ReadAllBytes(ini);
            Reject(() => engine.Restore(dir)); Reject(() => engine.Apply(dir, "新别名"));
            Check(File.ReadAllBytes(ini).SequenceEqual(changed), "external bytes retained");
        });
        Test("external attributes change blocks restore", delegate
        {
            string dir = Folder(); engine.Apply(dir, "别名");
            File.SetAttributes(dir, File.GetAttributes(dir) & ~FileAttributes.ReadOnly);
            Reject(() => engine.Restore(dir));
        });
        Test("missing backup never deletes another application's config", delegate
        {
            string dir = Folder(); string ini = Path.Combine(dir, "desktop.ini"); File.WriteAllText(ini, "[.ShellClassInfo]\r\nIconIndex=4", Encoding.ASCII);
            byte[] before = File.ReadAllBytes(ini); Reject(() => engine.Restore(dir)); Check(before.SequenceEqual(File.ReadAllBytes(ini)), "kept config");
        });
        Test("UTF-8 BOM and UTF-16 BE existing configs", delegate
        {
            foreach (Encoding encoding in new Encoding[] { new UTF8Encoding(true), new UnicodeEncoding(true, true) })
            {
                string dir = Folder(); string ini = Path.Combine(dir, "desktop.ini");
                File.WriteAllText(ini, "; 中文注释\n[.ShellClassInfo]\nInfoTip=已有说明\n", encoding);
                byte[] original = File.ReadAllBytes(ini); engine.Apply(dir, "中文（保留）");
                Check(Encoding.Unicode.GetString(File.ReadAllBytes(ini)).Contains("InfoTip=已有说明"), "content preserved");
                engine.Restore(dir); Check(original.SequenceEqual(File.ReadAllBytes(ini)), "encoding restored");
            }
        });
        Test("insertion respects sections and missing final newline", delegate
        {
            foreach (string original in new[] { "[.ShellClassInfo]\nIconIndex=2\n[Other]\nX=4", "[Other]\nX=4", "[.ShellClassInfo]", "[.ShellClassInfo]\rIconIndex=2\r" })
            {
                var doc = new IniDocument(Encoding.ASCII.GetBytes(original)); byte[] result = doc.SetAlias("中文");
                Check(new IniDocument(result).Alias == "中文", "alias in correct section");
                Check(Encoding.Unicode.GetString(result).Contains(original.StartsWith("[Other]") ? "X=4" : "[.ShellClassInfo]"), "old content");
            }
        });
        Test("ambiguous encoding and malformed config rejected without backup", delegate
        {
            foreach (byte[] bytes in new[] { new byte[] { 0xD6, 0xD0 }, Encoding.ASCII.GetBytes("[.ShellClassInfo]\nLocalizedResourceName=A\nLocalizedResourceName=B"), Encoding.ASCII.GetBytes("[.ShellClassInfo]\n[.ShellClassInfo]"), Encoding.ASCII.GetBytes("[broken"), new byte[] { 0, 1, 2 }, new byte[] { 255, 254, 65 } })
            {
                string dir = Folder(); string ini = Path.Combine(dir, "desktop.ini"); File.WriteAllBytes(ini, bytes);
                Reject(() => engine.Apply(dir, "中文")); Check(bytes.SequenceEqual(File.ReadAllBytes(ini)), "no change");
                Check(!Directory.Exists(AliasEngine.BackupFolder(dir)), "no partial backup");
            }
        });
        Test("unsafe aliases rejected", delegate
        {
            string dir = Folder();
            foreach (string alias in new[] { "", " ", " text", "@remote.dll,-1", "name\r\nIconResource=bad", "\0", "a\"b", "a\u202Eb", new string('a', 121), "\uD800" })
                Reject(() => engine.Apply(dir, alias));
            Check(!File.Exists(Path.Combine(dir, "desktop.ini")), "no config written");
        });
        Test("network, root, relative and stream paths rejected", delegate
        {
            foreach (string path in new[] { @"\\server\share\dir", @"C:\", "relative", @"C:\name:stream", @"\\?\C:\test" }) Reject(() => SafePath.Folder(path));
        });
        Test("hardlinked desktop.ini rejected", delegate
        {
            string dir = Folder(); string real = Path.Combine(dir, "original.ini"); File.WriteAllText(real, "[.ShellClassInfo]", Encoding.ASCII);
            Check(CreateHardLink(Path.Combine(dir, "desktop.ini"), real, IntPtr.Zero), "create hard link: " + Marshal.GetLastWin32Error());
            Reject(() => engine.Apply(dir, "中文")); Check(File.ReadAllText(real) == "[.ShellClassInfo]", "hardlink target unchanged");
        });
        Test("MOTW or named stream is not removed", delegate
        {
            string dir = Folder(); string ini = Path.Combine(dir, "desktop.ini"); File.WriteAllText(ini, "[.ShellClassInfo]", Encoding.ASCII);
            using (var handle = CreateFile(ini + ":Zone.Identifier", 0x40000000, 0, IntPtr.Zero, 2, 0, IntPtr.Zero))
            {
                Check(!handle.IsInvalid, "create stream");
                using (var stream = new FileStream(handle, FileAccess.Write)) { byte[] bytes = Encoding.ASCII.GetBytes("[ZoneTransfer]\r\nZoneId=3\r\n"); stream.Write(bytes, 0, bytes.Length); }
            }
            Reject(() => engine.Apply(dir, "中文")); Check(File.ReadAllText(ini) == "[.ShellClassInfo]", "stream-protected file unchanged");
        });
        Test("foreign backup folder is not overwritten", delegate
        {
            string dir = Folder(); Directory.CreateDirectory(AliasEngine.BackupFolder(dir));
            File.WriteAllText(Path.Combine(AliasEngine.BackupFolder(dir), "mine.txt"), "keep");
            Reject(() => engine.Apply(dir, "中文")); Check(!File.Exists(Path.Combine(dir, "desktop.ini")), "no config change");
        });
        Test("corrupt backup refused", delegate
        {
            string dir = Folder(); engine.Apply(dir, "中文"); var before = AliasEngine.Capture(dir);
            string state = File.ReadAllText(AliasEngine.StatePath(dir));
            File.WriteAllText(AliasEngine.StatePath(dir), state.Replace("ABSENT", "CORRUPT"));
            Reject(() => engine.Restore(dir)); Check(AliasEngine.Equivalent(before, AliasEngine.Capture(dir)), "config retained");
        });
        Test("copied backup cannot restore another folder", delegate
        {
            string first = Folder(), second = Folder(); engine.Apply(first, "中文");
            Directory.CreateDirectory(AliasEngine.BackupFolder(second)); File.Copy(AliasEngine.StatePath(first), AliasEngine.StatePath(second));
            Reject(() => engine.Restore(second));
        });
        foreach (string stage in new[] { "after-journal", "after-file-write", "after-folder-attributes", "before-commit" })
        {
            string capturedStage = stage;
            Test("rollback after fault: " + stage, delegate
            {
                string dir = Folder(); var original = AliasEngine.Capture(dir); bool once = false;
                AliasEngine.TestFault = p => { if (!once && p == capturedStage) { once = true; throw new IOException("injected failure"); } };
                try { Reject(() => engine.Apply(dir, "中文")); } finally { AliasEngine.TestFault = null; }
                Check(once, "fault reached"); Check(AliasEngine.Equivalent(original, AliasEngine.Capture(dir)), "rolled back");
                Check(AliasEngine.Load(dir).Phase == "Restored", "journal resolved");
            });
            Test("process crash recovery: " + stage, delegate
            {
                string dir = Folder(); var original = AliasEngine.Capture(dir);
                var start = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName, "--crash \"" + dir + "\" " + capturedStage) { UseShellExecute = false, CreateNoWindow = true };
                using (var process = Process.Start(start)) { Check(process.WaitForExit(15000), "child exited"); Check(process.ExitCode == 77, "simulated crash"); }
                Check(AliasEngine.Load(dir).Phase == "Pending", "pending state persisted");
                Reject(() => engine.Apply(dir, "other")); engine.Recover(dir);
                Check(AliasEngine.Equivalent(original, AliasEngine.Capture(dir)), "recovered baseline");
            });
        }
        Test("pending recovery protects third-party changes", delegate
        {
            string dir = Folder(); AliasEngine.TestFault = p => { if (p == "after-file-write") throw new IOException("persistent fault"); };
            try { Reject(() => engine.Apply(dir, "中文")); } finally { AliasEngine.TestFault = null; }
            // Force a real durable pending journal using a crash, independent of partial rollback behavior.
            var state = AliasEngine.Load(dir);
            if (state.Phase != "Pending") throw new Exception("expected pending");
            File.WriteAllText(Path.Combine(dir, "desktop.ini"), "[.ShellClassInfo]\r\nInfoTip=third party", Encoding.ASCII);
            byte[] changed = File.ReadAllBytes(Path.Combine(dir, "desktop.ini")); Reject(() => engine.Recover(dir));
            Check(changed.SequenceEqual(File.ReadAllBytes(Path.Combine(dir, "desktop.ini"))), "third-party bytes retained");
        });
        Test("existing read-only config survives failed update", delegate
        {
            string dir = Folder(); string ini = Path.Combine(dir, "desktop.ini");
            File.WriteAllText(ini, "; original\r\n[.ShellClassInfo]\r\nIconIndex=4\r\nLocalizedResourceName=Original\r\n", Encoding.ASCII);
            File.SetAttributes(ini, FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System);
            var original = AliasEngine.Capture(dir); engine.Apply(dir, "第一次"); var first = AliasEngine.Capture(dir); bool once = false;
            AliasEngine.TestFault = p => { if (p == "after-file-write" && !once) { once = true; throw new IOException("injected"); } };
            try { Reject(() => engine.Apply(dir, "第二次")); } finally { AliasEngine.TestFault = null; }
            Check(AliasEngine.Equivalent(first, AliasEngine.Capture(dir)), "first applied state restored");
            engine.Restore(dir); Check(AliasEngine.Equivalent(original, AliasEngine.Capture(dir)), "original restored");
        });
        Test("locked config rejected without modifying original", delegate
        {
            string dir = Folder(); string ini = Path.Combine(dir, "desktop.ini"); File.WriteAllText(ini, "[.ShellClassInfo]\nInfoTip=keep", Encoding.ASCII);
            byte[] original = File.ReadAllBytes(ini);
            using (var held = new FileStream(ini, FileMode.Open, FileAccess.Read, FileShare.None)) Reject(() => engine.Apply(dir, "中文"));
            Check(File.ReadAllBytes(ini).SequenceEqual(original), "locked original retained");
        });
        Test("batch continues after per-folder conflict", delegate
        {
            string first = Folder(), second = Folder(), third = Folder(); engine.Apply(second, "old");
            File.AppendAllText(Path.Combine(second, "desktop.ini"), "InfoTip=external\r\n", Encoding.Unicode);
            int ok = 0, errors = 0;
            foreach (string folder in new[] { first, second, third }) { try { engine.Apply(folder, "批量（测试）"); ok++; } catch (IOException) { errors++; } }
            Check(ok == 2 && errors == 1, "isolated outcomes");
            engine.Restore(first); engine.Restore(third);
            Check(!File.Exists(Path.Combine(first, "desktop.ini")) && !File.Exists(Path.Combine(third, "desktop.ini")), "successful rows restore");
        });
        Test("menu command quoted without invoking a command shell", delegate
        {
            Check(MenuIntegration.BuildCommand(@"C:\中文 空格 & tools\FolderAlias.exe") == "\"C:\\中文 空格 & tools\\FolderAlias.exe\" --open \"%1\"", "quoted command");
            Reject(() => MenuIntegration.BuildCommand("relative.exe"));
        });
        Console.WriteLine("RESULT: " + passed + " passed, " + failed + " failed");
        Console.WriteLine("FIXTURES: " + root);
        return failed == 0 ? 0 : 1;
    }
    static string Folder() { string value = Path.Combine(root, "case" + (++counter).ToString("D3")); Directory.CreateDirectory(value); return value; }
    static void Test(string name, Action body)
    {
        if (filter != null && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) return;
        try { body(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + "\n" + ex); }
        finally { AliasEngine.TestFault = null; }
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Reject(Action action)
    {
        try { action(); } catch (Exception) { return; }
        throw new Exception("Expected rejection but operation succeeded.");
    }
}
