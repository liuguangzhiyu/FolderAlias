using System;
using System.Reflection;

// Uses the same installer engine as the UI. --install-modern requires explicit human authorization.
static class SetupProbe
{
    [STAThread] static int Main(string[] args)
    {
        try
        {
            var assembly = Assembly.LoadFrom(System.IO.Path.GetFullPath(args[0]));
            var type = assembly.GetType("FolderAlias.Setup.SetupEngine", true);
            if (args.Length != 3) throw new ArgumentException("SetupProbe setup.exe --install-modern|--uninstall target-directory");
            object result;
            if (args[1] == "--install-modern")
                result = type.GetMethod("Install", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { args[2], true, true, new Action<string>(Console.WriteLine) });
            else if (args[1] == "--uninstall")
                result = type.GetMethod("Uninstall", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { args[2], false, new Action<string>(Console.WriteLine) });
            else throw new ArgumentException("Unknown action.");
            Console.WriteLine(result); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
