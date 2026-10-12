using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace FolderAlias
{
    internal static class MenuIntegration
    {
        internal const string KeyPath = @"Software\Classes\Directory\shell\FolderAlias.Local";
        const string Owner = "FolderAlias.Local.v1";
        internal static string BuildCommand(string executable)
        {
            if (String.IsNullOrEmpty(executable) || executable.IndexOf('"') >= 0 || !Path.IsPathRooted(executable))
                throw new ArgumentException("EXE 路径无效。");
            return "\"" + executable + "\" --open \"%1\"";
        }
        static void CheckOwned(RegistryKey key)
        {
            if ((string)key.GetValue("FolderAliasOwner", "") != Owner)
                throw new IOException("同名右键菜单已存在且不属于本工具，未覆盖或删除。");
            string[] allowed = { "", "Icon", "MultiSelectModel", "FolderAliasOwner" };
            if (key.GetValueNames().Any(v => !allowed.Contains(v, StringComparer.OrdinalIgnoreCase)) ||
                key.GetSubKeyNames().Any(v => !v.Equals("command", StringComparison.OrdinalIgnoreCase)))
                throw new IOException("本工具菜单项包含额外配置，未覆盖或删除。");
            using (var command = key.OpenSubKey("command"))
                if (command != null && (command.GetSubKeyNames().Length != 0 || command.GetValueNames().Any(v => v != "")))
                    throw new IOException("菜单命令包含额外配置，未覆盖或删除。");
        }
        public static void Install(string executable)
        {
            if (!File.Exists(executable)) throw new FileNotFoundException("可执行文件不存在。", executable);
            string command = BuildCommand(executable);
            using (var existing = Registry.CurrentUser.OpenSubKey(KeyPath)) if (existing != null) CheckOwned(existing);
            using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                key.SetValue("FolderAliasOwner", Owner, RegistryValueKind.String);
                key.SetValue("", "设置中文显示名…", RegistryValueKind.String);
                key.SetValue("Icon", "\"" + executable + "\",0", RegistryValueKind.String);
                key.SetValue("MultiSelectModel", "Single", RegistryValueKind.String);
                using (var cmd = key.CreateSubKey("command")) cmd.SetValue("", command, RegistryValueKind.String);
                key.Flush();
            }
            Native.RefreshMenu();
        }
        public static void Uninstall()
        {
            using (var existing = Registry.CurrentUser.OpenSubKey(KeyPath))
            { if (existing == null) return; CheckOwned(existing); }
            Registry.CurrentUser.DeleteSubKeyTree(KeyPath, false);
            Native.RefreshMenu();
        }
        public static string Status()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(KeyPath))
            {
                if (key == null) return "未安装";
                CheckOwned(key);
                using (var command = key.OpenSubKey("command")) return command == null ? "安装不完整" : (string)command.GetValue("", "");
            }
        }
    }
}
