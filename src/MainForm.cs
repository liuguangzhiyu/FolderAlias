using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FolderAlias
{
    internal sealed class MainForm : Form
    {
        readonly DataGridView grid = new DataGridView();
        readonly TextBox details = new TextBox();
        readonly Label summary = new Label();
        readonly AliasEngine engine = new AliasEngine();
        readonly FlowLayoutPanel tools = new FlowLayoutPanel();
        readonly FlowLayoutPanel actions = new FlowLayoutPanel();
        bool busy;
        public MainForm(IEnumerable<string> folders)
        {
            SuspendLayout();
            Text = "文件夹中文别名 · FolderAlias";
            Font = new Font("Microsoft YaHei UI", 10F);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1080, 690); MinimumSize = new Size(920, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(245, 247, 250);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 7 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 53));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 145));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            Controls.Add(layout);
            var header = new Panel { Dock = DockStyle.Fill };
            header.Controls.Add(new Label { Text = "中文显示名，英文路径照旧", Font = new Font(Font.FontFamily, 17, FontStyle.Bold), AutoSize = true, Location = new Point(0, 0), ForeColor = Color.FromArgb(27, 47, 66) });
            header.Controls.Add(new Label { Text = "一次添加多个文件夹，填写中文名，再应用勾选项。双击中文名可编辑。", AutoSize = true, Location = new Point(1, 33), ForeColor = Color.DimGray });
            layout.Controls.Add(header, 0, 0);
            tools.Dock = DockStyle.Fill; tools.WrapContents = false;
            AddButton(tools, "添加文件夹（多选）", delegate { foreach (string folder in FolderPicker.Show(Handle)) AddFolder(folder, null); });
            AddButton(tools, "全选", delegate { SetChecked(true); }, 72);
            AddButton(tools, "全不选", delegate { SetChecked(false); }, 80);
            AddButton(tools, "移除选中行", delegate { foreach (DataGridViewRow row in grid.SelectedRows.Cast<DataGridViewRow>().ToArray()) grid.Rows.Remove(row); UpdateSummary(); });
            AddButton(tools, "右键菜单…", MenuDialog);
            layout.Controls.Add(tools, 0, 1);
            grid.Dock = DockStyle.Fill; grid.BackgroundColor = Color.White; grid.BorderStyle = BorderStyle.None;
            grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.RowHeadersVisible = false;
            grid.AllowUserToResizeRows = false; grid.AllowUserToResizeColumns = false;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.MultiSelect = true;
            grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; grid.RowTemplate.Height = 36;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
            grid.EnableHeadersVisualStyles = false; grid.ColumnHeadersHeight = 38;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(231, 237, 242);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(27, 47, 66);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(221, 237, 249);
            grid.DefaultCellStyle.SelectionForeColor = Color.Black;
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "use", HeaderText = "应用", AutoSizeMode = DataGridViewAutoSizeColumnMode.None, Width = 55, MinimumWidth = 48 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "path", HeaderText = "实际路径（不改名）", ReadOnly = true, FillWeight = 37 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "alias", HeaderText = "中文显示名（可编辑）", FillWeight = 26 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "result", HeaderText = "状态", ReadOnly = true, FillWeight = 28 });
            grid.Columns["alias"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.Columns["result"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.DefaultCellStyle.Padding = new Padding(4, 5, 4, 5);
            foreach (DataGridViewColumn column in grid.Columns)
            { column.Resizable = DataGridViewTriState.False; column.SortMode = DataGridViewColumnSortMode.NotSortable; }
            grid.SelectionChanged += delegate { ShowDetails(); };
            grid.CellContentClick += delegate(object sender, DataGridViewCellEventArgs e) { if (e.ColumnIndex == 0) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            grid.CellValueChanged += delegate { UpdateSummary(); };
            grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
            layout.Controls.Add(grid, 0, 2);
            actions.Dock = DockStyle.Fill; actions.Padding = new Padding(0, 9, 0, 0); actions.WrapContents = false;
            Button apply = AddButton(actions, "应用勾选项", delegate { Run("apply"); });
            apply.BackColor = Color.FromArgb(29, 96, 158); apply.ForeColor = Color.White;
            AddButton(actions, "还原勾选项", delegate { Run("restore"); });
            AddButton(actions, "刷新并检测", delegate { Run("inspect"); });
            AddButton(actions, "恢复中断操作", delegate { Run("recover"); });
            layout.Controls.Add(actions, 0, 3);
            summary.AutoSize = true; summary.ForeColor = Color.FromArgb(65, 83, 99); summary.Padding = new Padding(0, 4, 0, 0);
            layout.Controls.Add(summary, 0, 4);
            details.Dock = DockStyle.Fill; details.Multiline = true; details.ReadOnly = true; details.ScrollBars = ScrollBars.Both;
            details.BackColor = Color.White; details.BorderStyle = BorderStyle.FixedSingle; details.WordWrap = false;
            layout.Controls.Add(details, 0, 5);
            layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "原配置和备份会保留。Windows 可能缓存或忽略别名；可按 F5 或重新打开父文件夹查看。", ForeColor = Color.DimGray, Padding = new Padding(0, 8, 0, 0) }, 0, 6);
            AllowDrop = true;
            DragEnter += delegate(object sender, DragEventArgs e) { if (!busy && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += delegate(object sender, DragEventArgs e) { if (!busy) foreach (string path in (string[])e.Data.GetData(DataFormats.FileDrop)) AddFolder(path, null); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (busy) e.Cancel = true; };
            foreach (string folder in folders) AddFolder(folder, null);
            UpdateSummary();
            Shown += delegate
            {
                grid.Columns["use"].Width = TextRenderer.MeasureText("应用", grid.Font).Width + 28;
                grid.ColumnHeadersHeight = grid.Font.Height + 16;
                grid.AutoResizeRows(DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders);
                if (grid.Rows.Count == 1) { grid.CurrentCell = grid.Rows[0].Cells["alias"]; grid.BeginEdit(true); }
            };
            ResumeLayout(true);
        }
        Button AddButton(FlowLayoutPanel panel, string text, Action click, int width = 118)
        {
            var button = new Button { Text = text, AutoSize = true, Height = 34, MinimumSize = new Size(width, 34), FlatStyle = FlatStyle.Flat, BackColor = Color.White, Margin = new Padding(0, 0, 9, 0), Padding = new Padding(7, 0, 7, 0) };
            button.FlatAppearance.BorderColor = Color.FromArgb(201, 211, 221);
            button.Click += delegate { try { click(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "未完成", MessageBoxButtons.OK, MessageBoxIcon.Information); } };
            panel.Controls.Add(button); return button;
        }
        internal void AddFolder(string input, string suggested)
        {
            string folder;
            try { folder = SafePath.Folder(input); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "无法添加", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            foreach (DataGridViewRow existing in grid.Rows)
                if (String.Equals((string)existing.Cells["path"].Value, folder, StringComparison.OrdinalIgnoreCase))
                { if (suggested != null) existing.Cells["alias"].Value = suggested; return; }
            string alias = suggested ?? "", status;
            try
            {
                Inspection info = engine.Inspect(folder);
                if (suggested == null && info.Alias != null && !info.Alias.StartsWith("@")) alias = info.Alias;
                status = info.IniExists ? "已有配置；应用前备份" : "尚无配置；等待填写";
                if (!String.IsNullOrEmpty(info.Warning)) status = info.Warning;
            }
            catch (Exception ex) { status = "检查未通过：" + ex.Message; }
            int index = grid.Rows.Add(true, folder, alias, status);
            grid.Rows[index].Cells["path"].ToolTipText = folder;
            UpdateSummary();
        }
        void UpdateSummary()
        {
            int selected = grid.Rows.Cast<DataGridViewRow>().Count(r => Convert.ToBoolean(r.Cells["use"].Value));
            summary.Text = "已勾选 " + selected + " / " + grid.Rows.Count + " 个文件夹 · 仅处理勾选项，不递归修改子文件夹";
        }
        void SetChecked(bool value)
        {
            grid.EndEdit();
            foreach (DataGridViewRow row in grid.Rows) row.Cells["use"].Value = value;
            UpdateSummary();
        }
        void ShowDetails()
        {
            if (busy || grid.CurrentRow == null) return;
            try { details.Text = engine.Inspect((string)grid.CurrentRow.Cells["path"].Value).ToString(); }
            catch (Exception ex) { details.Text = ex.Message; }
        }
        void Run(string action)
        {
            grid.EndEdit();
            var rows = grid.Rows.Cast<DataGridViewRow>().Where(r => Convert.ToBoolean(r.Cells["use"].Value)).ToArray();
            if (rows.Length == 0) throw new InvalidOperationException("请先勾选至少一个文件夹。");
            if (action == "restore" && MessageBox.Show(this, "还原 " + rows.Length + " 个文件夹在本轮首次设置前的配置？\r\n备份将保留；发现外部改动时会跳过。", "还原原设置", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            busy = true; tools.Enabled = false; actions.Enabled = false; grid.Enabled = false; UseWaitCursor = true;
            int success = 0, failed = 0; var log = new List<string>();
            try
            {
                foreach (DataGridViewRow row in rows)
                {
                    string path = (string)row.Cells["path"].Value;
                    try
                    {
                        string result;
                        if (action == "apply") result = engine.Apply(path, Convert.ToString(row.Cells["alias"].Value));
                        else if (action == "restore") result = engine.Restore(path);
                        else if (action == "recover") result = engine.Recover(path);
                        else
                        {
                            Native.Refresh(path);
                            Inspection info = engine.Inspect(path);
                            result = info.Alias != null && info.ShellName == info.Alias ? "Shell 已返回中文显示名" : "Shell 当前返回：" + info.ShellName;
                        }
                        row.Cells["result"].Value = result; success++;
                        log.Add(path + "\r\n  " + result);
                    }
                    catch (Exception ex)
                    {
                        failed++; row.Cells["result"].Value = "未完成：" + ex.Message;
                        log.Add(path + "\r\n  未完成：" + ex.Message);
                    }
                    row.Cells["result"].ToolTipText = Convert.ToString(row.Cells["result"].Value);
                    grid.Refresh();
                }
            }
            finally { busy = false; tools.Enabled = true; actions.Enabled = true; grid.Enabled = true; UseWaitCursor = false; }
            details.Text = String.Join("\r\n\r\n", log);
            summary.Text = "已完成 " + success + " 项，未完成 " + failed + " 项。各文件夹分别处理，失败项不覆盖其他项。";
        }
        void MenuDialog()
        {
            string setup = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FolderAlias.Setup.exe");
            if (File.Exists(setup) && File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "install-state.xml")))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(setup) { UseShellExecute = true });
                return;
            }
            using (var dialog = new Form { Text = "右键菜单", ClientSize = new Size(540, 255), StartPosition = FormStartPosition.CenterParent, Font = Font, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false })
            {
                var label = new Label { Location = new Point(18, 18), Size = new Size(502, 135), Text = "仅添加当前用户的文件夹菜单，无需管理员权限。\r\nWindows 11 中通常在“显示更多选项”里。\r\n安装前请把 EXE 放在准备长期保存的位置。\r\n卸载菜单不会还原别名，也不会删除备份。\r\n\r\n状态：" + (MenuIntegration.Status() == "未安装" ? "未安装" : "已注册（可重新安装更新位置）") };
                dialog.Controls.Add(label);
                var panel = new FlowLayoutPanel { Location = new Point(18, 181), Size = new Size(510, 50) };
                AddButton(panel, "安装 / 更新菜单", delegate { MenuIntegration.Install(Application.ExecutablePath); label.Text = "已安装到当前用户。\r\n右键文件夹 → 显示更多选项 → 设置中文显示名…"; });
                AddButton(panel, "卸载菜单", delegate { MenuIntegration.Uninstall(); label.Text = "已卸载菜单；现有别名及备份均保留。"; });
                dialog.Controls.Add(panel); dialog.ShowDialog(this);
            }
        }
        internal void RenderPreview(string path)
        {
            Show(); Application.DoEvents(); grid.EndEdit(); PerformLayout();
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
            Close();
        }
    }
}
