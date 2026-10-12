using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FolderAlias
{
    internal sealed class IniDocument
    {
        readonly List<string> lines;
        int section = -1, sectionEnd = -1, key = -1;
        public string Alias { get; private set; }
        public string EncodingName { get; private set; }

        public IniDocument(byte[] bytes)
        {
            if (bytes.Length > 1024 * 1024) throw new IOException("desktop.ini 超过 1 MB，已停止处理。");
            string text;
            if (bytes.Length >= 4 && ((bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0 && bytes[3] == 0) ||
                (bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF)))
                throw new IOException("不支持 UTF-32 配置，请先检查原文件。");
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            { text = new UnicodeEncoding(false, true, true).GetString(bytes, 2, bytes.Length - 2); EncodingName = "UTF-16 LE"; }
            else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            { text = new UnicodeEncoding(true, true, true).GetString(bytes, 2, bytes.Length - 2); EncodingName = "UTF-16 BE"; }
            else if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            { text = new UTF8Encoding(false, true).GetString(bytes, 3, bytes.Length - 3); EncodingName = "UTF-8 BOM"; }
            else
            {
                if (bytes.Any(b => b >= 128)) throw new IOException("原配置没有编码标记且含非 ASCII 字符，无法安全判断编码，未修改。请先确认原文件编码。");
                text = Encoding.ASCII.GetString(bytes); EncodingName = "ASCII";
            }
            if (text.IndexOf('\0') >= 0) throw new IOException("配置含 NUL 字符，未修改。");
            lines = Regex.Matches(text, @"[^\r\n]*(?:\r\n|\r|\n|$)").Cast<Match>().Select(m => m.Value).Where(s => s.Length > 0).ToList();
            bool inSection = false;
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith(";") || line.StartsWith("#") || line.Length == 0) continue;
                if (line.StartsWith("["))
                {
                    var match = Regex.Match(line, @"^\[([^\]]+)\]\s*(?:;.*)?$");
                    if (!match.Success) throw new IOException("配置中有无法可靠解析的节标题，未修改。");
                    if (inSection) sectionEnd = i;
                    inSection = match.Groups[1].Value.Trim().Equals(".ShellClassInfo", StringComparison.OrdinalIgnoreCase);
                    if (inSection)
                    {
                        if (section >= 0) throw new IOException("配置有重复的 [.ShellClassInfo] 节，未修改。");
                        section = i;
                    }
                }
                else if (inSection)
                {
                    int equals = line.IndexOf('=');
                    if (equals >= 0 && line.Substring(0, equals).Trim().Equals("LocalizedResourceName", StringComparison.OrdinalIgnoreCase))
                    {
                        if (key >= 0) throw new IOException("配置有重复的显示名字段，未修改。");
                        key = i; Alias = line.Substring(equals + 1).Trim();
                    }
                }
            }
            if (sectionEnd < 0) sectionEnd = lines.Count;
        }
        public static string ValidateAlias(string value)
        {
            if (String.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Length > 120)
                throw new ArgumentException("显示名须为 1–120 个字符，首尾不能是空格。");
            if (value.StartsWith("@")) throw new ArgumentException("显示名不能以 @ 开头，以免被解释成外部资源引用。");
            if (value.Any(c => Char.IsControl(c) || c == '"' || c == '\u2028' || c == '\u2029' || (c >= '\u202A' && c <= '\u202E') || (c >= '\u2066' && c <= '\u2069')))
                throw new ArgumentException("显示名不能包含控制字符、双引号、换行或双向文本控制符。");
            new UnicodeEncoding(false, true, true).GetBytes(value); // Reject isolated surrogates; allow emoji.
            return value;
        }
        static string Ending(string value)
        {
            if (value.EndsWith("\r\n")) return "\r\n";
            if (value.EndsWith("\n")) return "\n";
            if (value.EndsWith("\r")) return "\r";
            return "";
        }
        public byte[] SetAlias(string alias)
        {
            ValidateAlias(alias);
            var output = new List<string>(lines);
            string newline = lines.Select(Ending).FirstOrDefault(s => s.Length > 0) ?? "\r\n";
            if (key >= 0)
            {
                string line = output[key];
                output[key] = line.Substring(0, line.IndexOf('=') + 1) + alias + Ending(line);
            }
            else if (section >= 0)
            {
                if (sectionEnd > 0 && Ending(output[sectionEnd - 1]).Length == 0) output[sectionEnd - 1] += newline;
                output.Insert(sectionEnd, "LocalizedResourceName=" + alias + newline);
            }
            else
            {
                if (output.Count > 0 && Ending(output[output.Count - 1]).Length == 0) output[output.Count - 1] += newline;
                output.Add("[.ShellClassInfo]" + newline);
                output.Add("LocalizedResourceName=" + alias + newline);
            }
            var encoding = new UnicodeEncoding(false, true, true);
            return encoding.GetPreamble().Concat(encoding.GetBytes(String.Concat(output))).ToArray();
        }
    }
}
