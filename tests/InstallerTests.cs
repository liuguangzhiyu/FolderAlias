using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using FolderAlias.Setup;

static class InstallerTests
{
    static int count;
    static string root;
    static void Check(bool result, string message) { if (!result) throw new Exception(message); count++; Console.WriteLine("PASS " + message); }
    static void Reject(Action action, string message) { try { action(); } catch (IOException) { Check(true, message); return; } throw new Exception("Expected rejection: " + message); }
    static byte[] Zip(params string[] names)
    {
        using (var output = new MemoryStream())
        {
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
                foreach (string name in names) using (var entry = archive.CreateEntry(name).Open()) { byte[] bytes = Encoding.UTF8.GetBytes("数据 " + name); entry.Write(bytes, 0, bytes.Length); }
            return output.ToArray();
        }
    }
    static int Main(string[] args)
    {
        try
        {
            root = Path.GetFullPath(args[0]); if (Directory.Exists(root)) throw new IOException("Choose a new test directory."); Directory.CreateDirectory(root);
            Check(SetupEngine.PSLiteral("O'Brien $()") == "'O''Brien $()'", "PowerShell data quoting");
            foreach (string name in new[] { "../outside.txt", "/absolute.txt", "C:/absolute.txt", "foo\\bar", "a/../b", "a//b", "a./b", "x:stream", "a /b" })
            { string capture = name; Reject(() => SetupEngine.EntryPath(root, capture), "reject archive path " + name); }
            Check(SetupEngine.EntryPath(root, "Assets/中文.png") == Path.Combine(root, @"Assets\中文.png"), "Unicode archive path remains inside target");
            Reject(() => SetupEngine.ValidateDirectory(@"C:\"), "reject drive root");
            Reject(() => SetupEngine.ValidateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "reject Windows directory");
            Reject(() => SetupEngine.ValidateDirectory(@"\\server\share\FolderAlias"), "reject network install");
            var state = new InstallState { Directory = root, Mode = "classic" };
            Reject(() => SetupEngine.Extract(Zip("good.txt", "../outside.txt"), root, state), "validate every ZIP path before any extraction");
            Check(!File.Exists(Path.Combine(root, "good.txt")), "invalid ZIP leaves no partial good file");
            Reject(() => SetupEngine.Extract(Zip("dup.txt", "DUP.txt"), root, state), "reject case-insensitive duplicate entries");
            string sentinel = Path.Combine(root, "sentinel.txt"); File.WriteAllText(sentinel, "user file");
            Reject(() => SetupEngine.Extract(Zip("sentinel.txt"), root, state), "never overwrite an existing file");
            Check(File.ReadAllText(sentinel) == "user file", "existing file byte content preserved");
            SetupEngine.Extract(Zip("owned.txt", "Assets/中文.txt"), root, state);
            Check(state.Files.Count == 2 && state.Files.All(f => f.Hash.Length == 64), "extraction records per-file SHA-256");
            Check(File.ReadAllText(Path.Combine(root, @"Assets\中文.txt")).Contains("数据"), "Unicode extraction preserves contents");
            File.AppendAllText(Path.Combine(root, "owned.txt"), "changed externally");
            var notes = SetupEngine.DeleteOwnedFiles(root, state);
            Check(notes.Count == 1 && File.Exists(Path.Combine(root, "owned.txt")), "uninstall preserves externally changed installed file");
            Check(File.Exists(sentinel) && !File.Exists(Path.Combine(root, @"Assets\中文.txt")), "uninstall removes unchanged owned file and preserves foreign file");
            Check(!Directory.Exists(Path.Combine(root, "Assets")), "uninstall removes only empty owned subdirectory");
            string round = Path.Combine(root, "roundtrip"); Directory.CreateDirectory(round);
            var valid = new InstallState { Directory = round, Mode = "classic" };
            SetupEngine.Extract(SetupEngine.Resource("Payload.zip"), round, valid);
            Check(SetupEngine.ReadState(round).Files.Count == valid.Files.Count, "state can be verified against embedded payload");
            string record = Path.Combine(round, SetupEngine.StateName); string xml = File.ReadAllText(record);
            File.WriteAllText(record, xml.Replace("FolderAlias.exe", "sentinel.txt"));
            Reject(() => SetupEngine.ReadState(round), "tampered manifest cannot designate an unrelated file for removal");
            using (var cert = Trust.Certificate())
            {
                Check(!cert.HasPrivateKey, "only public certificate embedded");
                var basic = cert.Extensions.OfType<System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension>().Single();
                Check(!basic.CertificateAuthority, "signing certificate is not a certificate authority");
            }
            Console.WriteLine("Passed " + count + " installer checks. No registry or trust changes made."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
