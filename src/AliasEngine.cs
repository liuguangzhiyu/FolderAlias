using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

namespace FolderAlias
{
    public sealed class Snapshot
    {
        public bool Exists;
        public byte[] Bytes;
        public string Sha256;
        public int FileAttributes;
        public long WriteUtcTicks;
        public bool FolderReadOnly;
        public Snapshot Copy()
        {
            return new Snapshot { Exists = Exists, Bytes = Bytes == null ? null : (byte[])Bytes.Clone(), Sha256 = Sha256,
                FileAttributes = FileAttributes, WriteUtcTicks = WriteUtcTicks, FolderReadOnly = FolderReadOnly };
        }
        public void Validate()
        {
            if (Bytes == null || Bytes.Length > 1024 * 1024 || Sha256 != (Exists ? Native.Hash(Bytes) : "ABSENT"))
                throw new IOException("备份内容校验失败，已停止操作。");
            if (!Exists && Bytes.Length != 0) throw new IOException("备份状态不一致。");
            if (WriteUtcTicks < 0 || WriteUtcTicks > DateTime.MaxValue.Ticks) throw new IOException("备份时间字段无效。");
        }
    }
    public sealed class BackupState
    {
        public string Product = "FolderAlias.Local/1";
        public string FolderIdentity;
        public string OriginalPath;
        public string Phase;
        public string PreviousPhase;
        public string Action;
        public string Alias;
        public Snapshot Original;
        public Snapshot Last;
        public Snapshot Before;
        public Snapshot After;
    }
    internal sealed class Inspection
    {
        public string Folder, Alias, Encoding, ShellName, BackupStatus, Warning;
        public bool IniExists;
        public override string ToString()
        {
            return "实际路径：" + Folder + "\r\n当前配置显示名：" + (Alias ?? "（未设置）") +
                "\r\n编码：" + Encoding + "\r\nWindows Shell 返回：" + ShellName + "\r\n备份状态：" + BackupStatus +
                (String.IsNullOrEmpty(Warning) ? "" : "\r\n注意：" + Warning);
        }
    }
    internal sealed class AliasEngine
    {
        // Only tests assign this hook. No environment variables or remote controls affect it.
        internal static Action<string> TestFault = null;
        const FileAttributes FileBits = FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System | FileAttributes.Archive | FileAttributes.NotContentIndexed;
        const FileAttributes CompareBits = FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System | FileAttributes.NotContentIndexed;
        static void Fault(string point) { if (TestFault != null) TestFault(point); }
        static string Ini(string folder) { return Path.Combine(folder, "desktop.ini"); }
        internal static string BackupFolder(string folder) { return Path.Combine(folder, SafePath.BackupName); }
        internal static string StatePath(string folder) { return Path.Combine(BackupFolder(folder), "state.xml"); }

        internal static Snapshot Capture(string folder)
        {
            string path = Ini(folder);
            SafePath.RegularFile(path);
            bool exists = File.Exists(path);
            byte[] bytes = new byte[0];
            if (exists)
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length > 1024 * 1024) throw new IOException("desktop.ini 超过 1 MB，未修改。");
                    bytes = new byte[(int)stream.Length];
                    int position = 0, count;
                    while (position < bytes.Length && (count = stream.Read(bytes, position, bytes.Length - position)) > 0) position += count;
                    if (position != bytes.Length) throw new IOException("配置读取不完整。");
                }
            }
            return new Snapshot { Exists = exists, Bytes = bytes, Sha256 = exists ? Native.Hash(bytes) : "ABSENT",
                FileAttributes = exists ? (int)(File.GetAttributes(path) & FileBits) : 0,
                WriteUtcTicks = exists ? File.GetLastWriteTimeUtc(path).Ticks : 0,
                FolderReadOnly = (File.GetAttributes(folder) & FileAttributes.ReadOnly) != 0 };
        }
        static bool ContentEqual(Snapshot a, Snapshot b) { return a.Exists == b.Exists && a.Sha256 == b.Sha256; }
        internal static bool Equivalent(Snapshot a, Snapshot b)
        {
            return ContentEqual(a, b) && a.FolderReadOnly == b.FolderReadOnly &&
                (!a.Exists || (((FileAttributes)a.FileAttributes & CompareBits) == ((FileAttributes)b.FileAttributes & CompareBits)));
        }
        static void RequireUnchanged(string folder, Snapshot expected)
        {
            if (!Equivalent(Capture(folder), expected))
                throw new IOException("配置或相关属性已被其他程序更改。为保留新配置，本次未覆盖；请检查 desktop.ini 与备份。");
        }
        internal static BackupState Load(string folder)
        {
            string directory = BackupFolder(folder), path = StatePath(folder);
            if (File.Exists(directory)) throw new IOException("备份目录名称已被文件占用。");
            if (!Directory.Exists(directory)) return null;
            SafePath.CheckAncestors(directory);
            SafePath.RegularFile(path);
            if (!File.Exists(path)) throw new IOException("发现没有有效状态文件的备份目录，已停止；请检查：" + directory);
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 6 * 1024 * 1024 };
            BackupState state;
            using (var reader = XmlReader.Create(path, settings)) state = (BackupState)new XmlSerializer(typeof(BackupState)).Deserialize(reader);
            if (state.Product != "FolderAlias.Local/1" || state.FolderIdentity != Native.Identity(folder, true))
                throw new IOException("备份不属于当前文件夹（可能复制或替换过文件夹），未自动还原。");
            if (state.Phase != "Active" && state.Phase != "Restored" && state.Phase != "Pending") throw new IOException("未知备份状态。");
            if (state.Original == null || state.Last == null) throw new IOException("备份缺少必要内容。");
            state.Original.Validate(); state.Last.Validate();
            if (state.Phase == "Pending")
            {
                if (state.Before == null || state.After == null || (state.PreviousPhase != "Active" && state.PreviousPhase != "Restored"))
                    throw new IOException("未完成事务的备份不完整。");
                state.Before.Validate(); state.After.Validate();
            }
            return state;
        }
        internal static void Save(string folder, BackupState state)
        {
            string directory = BackupFolder(folder), path = StatePath(folder);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                File.SetAttributes(directory, File.GetAttributes(directory) | FileAttributes.Hidden);
            }
            SafePath.CheckAncestors(directory);
            SafePath.RegularFile(path);
            string temporary = Path.Combine(directory, "state-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    new XmlSerializer(typeof(BackupState)).Serialize(stream, state);
                    stream.Flush(true);
                }
                if (File.Exists(path))
                {
                    string history = Path.Combine(directory, "history-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".xml");
                    File.Replace(temporary, path, history); // Each prior state, including original bytes, remains available.
                }
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        static void SetReadOnly(string folder, bool value)
        {
            var attributes = File.GetAttributes(folder);
            File.SetAttributes(folder, value ? attributes | FileAttributes.ReadOnly : attributes & ~FileAttributes.ReadOnly);
        }
        static void WriteSnapshot(string folder, Snapshot target)
        {
            target.Validate();
            SafePath.Folder(folder);
            string path = Ini(folder);
            SafePath.RegularFile(path);
            if (target.Exists)
            {
                string temporary = Path.Combine(folder, ".folderalias-" + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { stream.Write(target.Bytes, 0, target.Bytes.Length); stream.Flush(true); }
                    if (File.Exists(path))
                    {
                        var attributes = File.GetAttributes(path);
                        if ((attributes & FileAttributes.ReadOnly) != 0) File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
                        try { File.Replace(temporary, path, null); }
                        catch { if (File.Exists(path)) File.SetAttributes(path, attributes); throw; }
                    }
                    else File.Move(temporary, path);
                    Fault("after-file-write");
                    var current = File.GetAttributes(path);
                    // Set the timestamp before restoring read-only, otherwise Windows denies the write.
                    File.SetAttributes(path, current & ~FileAttributes.ReadOnly);
                    File.SetLastWriteTimeUtc(path, new DateTime(target.WriteUtcTicks, DateTimeKind.Utc));
                    File.SetAttributes(path, (current & ~FileBits) | (FileAttributes)target.FileAttributes);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            else if (File.Exists(path))
            {
                File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
                File.Delete(path);
                Fault("after-file-write");
            }
            SetReadOnly(folder, target.FolderReadOnly);
            Fault("after-folder-attributes");
        }
        static bool Recoverable(Snapshot current, BackupState state)
        {
            // An interrupted atomic file replacement leaves either old or planned content.
            return ContentEqual(current, state.Before) || ContentEqual(current, state.After);
        }
        static void RecoverCore(string folder, BackupState state)
        {
            if (!Recoverable(Capture(folder), state)) throw new IOException("中断后配置又被其他程序更改，无法安全自动恢复。备份仍保留。");
            WriteSnapshot(folder, state.Before);
            RequireUnchanged(folder, state.Before);
            state.Last = Capture(folder); state.Phase = state.PreviousPhase;
            state.Before = null; state.After = null; state.Action = null;
            Save(folder, state);
            Native.Refresh(folder);
        }
        static void Commit(string folder, BackupState state, Snapshot before, Snapshot after, string action)
        {
            RequireUnchanged(folder, before);
            state.PreviousPhase = state.Phase; state.Phase = "Pending";
            state.Before = before; state.After = after; state.Action = action;
            Save(folder, state); // Durable journal is committed before touching desktop.ini.
            try
            {
                Fault("after-journal");
                RequireUnchanged(folder, before);
                WriteSnapshot(folder, after);
                RequireUnchanged(folder, after);
                Fault("before-commit");
                state.Last = Capture(folder); state.Phase = action == "restore" ? "Restored" : "Active";
                Save(folder, state);
            }
            catch (Exception error)
            {
                // Read the durable pending journal, rather than a partly advanced in-memory state.
                try
                {
                    BackupState pending = Load(folder);
                    if (pending == null || pending.Phase != "Pending") throw new IOException("请检查备份状态。");
                    RecoverCore(folder, pending);
                }
                catch (Exception recovery)
                { throw new IOException("操作未完成，已保留备份。请使用“恢复中断操作”或检查备份。原因：" + error.Message + "；恢复：" + recovery.Message, error); }
                throw new IOException("操作未完成，已回滚到操作前。原因：" + error.Message, error);
            }
            Native.Refresh(folder);
        }
        static void WithLock(string folder, Action action)
        {
            string name = "Local\\FolderAlias-" + Native.Hash(Encoding.UTF8.GetBytes(Native.Identity(folder, true)));
            using (var mutex = new Mutex(false, name))
            {
                bool acquired;
                try { acquired = mutex.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
                if (!acquired) throw new IOException("另一个本工具进程正在处理这个文件夹，请稍后重试。");
                try { action(); } finally { mutex.ReleaseMutex(); }
            }
        }
        public Inspection Inspect(string input)
        {
            string folder = SafePath.Folder(input);
            Snapshot snapshot = Capture(folder);
            var document = new IniDocument(snapshot.Bytes);
            BackupState state = Load(folder);
            string shell;
            try { shell = Native.DisplayName(folder); } catch (Exception ex) { shell = "无法查询：" + ex.Message; }
            return new Inspection { Folder = folder, Alias = document.Alias, Encoding = snapshot.Exists ? document.EncodingName : "尚无 desktop.ini",
                IniExists = snapshot.Exists, ShellName = shell,
                BackupStatus = state == null ? "无（首次应用时创建）" : state.Phase == "Pending" ? "有中断操作，需恢复" : state.Phase == "Active" ? "可还原" : "已还原，备份保留",
                Warning = state != null && state.Phase != "Pending" && !Equivalent(snapshot, state.Last) ? "配置已发生外部变化，自动覆盖/还原将被阻止。" : "" };
        }
        public string Apply(string input, string alias)
        {
            string folder = SafePath.Folder(input);
            IniDocument.ValidateAlias(alias);
            WithLock(folder, delegate
            {
                Snapshot before = Capture(folder);
                var document = new IniDocument(before.Bytes);
                BackupState state = Load(folder);
                if (state != null && state.Phase == "Pending") throw new IOException("存在中断操作，请先点击“恢复中断操作”。");
                if (state != null && state.Phase == "Active") RequireUnchanged(folder, state.Last);
                else state = new BackupState { FolderIdentity = Native.Identity(folder, true), OriginalPath = folder, Original = before.Copy(), Last = before.Copy(), Phase = "Restored" };
                Snapshot after = before.Copy();
                after.Exists = true; after.Bytes = document.SetAlias(alias); after.Sha256 = Native.Hash(after.Bytes);
                after.FileAttributes = (int)(((FileAttributes)before.FileAttributes) | FileAttributes.Hidden | FileAttributes.System);
                after.FolderReadOnly = true; after.WriteUtcTicks = DateTime.UtcNow.Ticks; state.Alias = alias;
                if (Equivalent(before, after)) return;
                Commit(folder, state, before, after, "apply");
            });
            return "已保存；请检测 Windows 显示结果。";
        }
        public string Restore(string input)
        {
            string folder = SafePath.Folder(input);
            string result = "已还原原配置；备份保留。";
            WithLock(folder, delegate
            {
                BackupState state = Load(folder);
                if (state == null) throw new IOException("没有本工具的备份，不能推测原设置。");
                if (state.Phase == "Pending") throw new IOException("请先恢复中断操作，再还原原设置。");
                if (state.Phase == "Restored") { result = "此前已还原，未改动。"; return; }
                RequireUnchanged(folder, state.Last);
                Commit(folder, state, Capture(folder), state.Original.Copy(), "restore");
            });
            return result;
        }
        public string Recover(string input)
        {
            string folder = SafePath.Folder(input);
            WithLock(folder, delegate
            {
                BackupState state = Load(folder);
                if (state == null || state.Phase != "Pending") throw new IOException("没有待恢复的中断操作。");
                RecoverCore(folder, state);
            });
            return "已恢复到中断操作开始前；备份保留。";
        }
    }
}
