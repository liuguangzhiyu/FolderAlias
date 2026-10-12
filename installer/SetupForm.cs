using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FolderAlias.Setup
{
    internal sealed class SetupForm : Form
    {
        readonly TextBox destination = new TextBox();
        readonly RadioButton modern = new RadioButton(), classic = new RadioButton();
        readonly CheckBox consent = new CheckBox(), removeTrust = new CheckBox();
        readonly Button action = new Button(), browse = new Button(), launch = new Button(), certificate = new Button();
        readonly TextBox status = new TextBox();
        readonly Label heading = new Label(), introduction = new Label();
        bool busy, maintenance;
        string installed;
        internal SetupForm(string uninstallFrom)
        {
            SuspendLayout();
            Text = "文件夹中文别名 · 安装与卸载"; Font = new Font("Microsoft YaHei UI", 10F);
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(740, 655); MinimumSize = new Size(740, 655);
            StartPosition = FormStartPosition.CenterScreen; BackColor = Color.FromArgb(246, 248, 251); Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(25), ColumnCount = 1, RowCount = 9 };
            foreach (int height in new[] { 44, 62, 29, 43, 89, 78, 52 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));
            Controls.Add(layout);
            heading.Text = "装好一次，右键就能用"; heading.Font = new Font(Font.FontFamily, 20, FontStyle.Bold); heading.Dock = DockStyle.Fill; heading.ForeColor = Color.FromArgb(29, 51, 72);
            introduction.Text = "文件夹显示中文名，实际英文路径保持不变。\r\n离线安装 · 无自动启动项 · 支持批量设置和还原"; introduction.Dock = DockStyle.Fill; introduction.ForeColor = Color.FromArgb(77, 92, 107); introduction.Padding = new Padding(0, 4, 0, 0);
            layout.Controls.Add(heading, 0, 0); layout.Controls.Add(introduction, 0, 1);
            layout.Controls.Add(new Label { Text = "安装位置", Dock = DockStyle.Fill }, 0, 2);
            var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 97));
            destination.Dock = DockStyle.Fill; destination.Margin = new Padding(0, 2, 9, 0); browse.Text = "选择…"; browse.Dock = DockStyle.Top; browse.Height = 31;
            browse.Click += delegate { using (var picker = new FolderBrowserDialog { Description = "选择父目录；将自动放入其下的 FolderAlias 文件夹", ShowNewFolderButton = true }) if (picker.ShowDialog(this) == DialogResult.OK) destination.Text = Path.Combine(picker.SelectedPath, "FolderAlias"); };
            pathRow.Controls.Add(destination, 0, 0); pathRow.Controls.Add(browse, 1, 0); layout.Controls.Add(pathRow, 0, 3);
            var choices = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            modern.Text = "第一层右键菜单（Windows 11，需要管理员确认一次）"; modern.AutoSize = true; modern.Checked = true;
            classic.Text = "普通右键菜单（在“显示更多选项”里，无需管理员权限）"; classic.AutoSize = true;
            modern.Margin = classic.Margin = new Padding(0, 5, 0, 7); choices.Controls.Add(modern); choices.Controls.Add(classic); layout.Controls.Add(choices, 0, 4);
            var consentPanel = new Panel { Dock = DockStyle.Fill };
            consent.Text = "我同意信任本工具内置的专用签名证书。\r\n它将加入本机“受信任的人”，不加入根证书、不关闭安全检查。";
            consent.Dock = DockStyle.Fill; consent.AutoSize = false;
            removeTrust.Text = "同时移除本工具导入的专用证书（需要管理员确认）。\r\n若其他账户仍使用此菜单，证书会保留。"; removeTrust.Dock = DockStyle.Fill; removeTrust.Visible = false;
            consentPanel.Controls.Add(consent); consentPanel.Controls.Add(removeTrust); layout.Controls.Add(consentPanel, 0, 5);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            action.Text = "安装"; action.Width = 134; action.Height = 36; action.BackColor = Color.FromArgb(29, 96, 158); action.ForeColor = Color.White; action.FlatStyle = FlatStyle.Flat;
            launch.Text = "打开工具"; launch.Width = 125; launch.Height = 36; launch.Enabled = false;
            certificate.Text = "证书说明 / 清理"; certificate.Width = 163; certificate.Height = 36;
            buttons.Controls.Add(action); buttons.Controls.Add(launch); buttons.Controls.Add(certificate); layout.Controls.Add(buttons, 0, 6);
            status.Multiline = true; status.ReadOnly = true; status.ScrollBars = ScrollBars.Vertical; status.Dock = DockStyle.Fill; status.BackColor = Color.White; status.BorderStyle = BorderStyle.FixedSingle;
            status.Text = "此为个人分享版，自签名证书没有公开发行商信誉。Windows 可能提示未知发布者；请确认来源后安装。\r\n选择一个新的或空的目录，再点击“安装”。";
            layout.Controls.Add(status, 0, 7);
            layout.Controls.Add(new Label { Text = "FolderAlias 1.2.0 · Windows 11 x64 · 安装程序不会强制重启资源管理器", Dock = DockStyle.Fill, ForeColor = Color.DimGray, Padding = new Padding(0, 10, 0, 0) }, 0, 8);
            modern.CheckedChanged += delegate { consent.Visible = modern.Checked && !maintenance; };
            action.Click += async delegate { await Execute(); };
            launch.Click += delegate { try { Process.Start(new ProcessStartInfo(Path.Combine(installed, "FolderAlias.exe")) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(this, ex.Message); } };
            certificate.Click += delegate { CertificateDialog(); };
            FormClosing += delegate(object sender, FormClosingEventArgs args) { if (busy) args.Cancel = true; };
            installed = uninstallFrom ?? SetupEngine.InstalledDirectory();
            if (!String.IsNullOrEmpty(installed)) SetMaintenance();
            else destination.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\FolderAlias");
            ResumeLayout(true);
        }
        void SetMaintenance()
        {
            var state = SetupEngine.ReadState(installed); maintenance = true;
            heading.Text = "管理已安装的工具"; introduction.Text = "卸载会移除菜单和程序文件，保留中文别名与备份。\r\n需要还原文件夹时，请先打开工具进行还原。";
            destination.Text = installed; destination.ReadOnly = true; browse.Enabled = false;
            modern.Checked = state.Mode == "modern"; classic.Checked = !modern.Checked; modern.Enabled = classic.Enabled = false;
            consent.Visible = false; removeTrust.Visible = true; action.Text = "卸载"; launch.Enabled = File.Exists(Path.Combine(installed, "FolderAlias.exe"));
            status.Text = "已安装版本：" + state.Version + "\r\n如需更换安装位置或菜单类型，请先卸载，再重新运行安装包。";
        }
        async Task Execute()
        {
            if (busy) return;
            if (maintenance && MessageBox.Show(this, "卸载此目录中的 FolderAlias 和当前用户菜单？\r\n已有中文别名、备份及后来添加的文件会保留。", "卸载", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            if (!maintenance && modern.Checked && !Trust.IsPresent() && !consent.Checked) { status.Text = "第一层右键菜单需要专用证书信任。请阅读并勾选同意，或选择无需管理员权限的普通菜单。"; return; }
            bool useModern = modern.Checked, agreed = consent.Checked, cleanup = removeTrust.Checked; string target = destination.Text;
            if (maintenance && Path.GetDirectoryName(SetupEngine.Self).Equals(installed, StringComparison.OrdinalIgnoreCase))
            {
                try { Program.StartDetachedUninstaller(installed); Close(); } catch (Exception ex) { status.Text = ex.Message; }
                return;
            }
            busy = true; action.Enabled = browse.Enabled = modern.Enabled = classic.Enabled = consent.Enabled = removeTrust.Enabled = certificate.Enabled = launch.Enabled = false; destination.ReadOnly = true; UseWaitCursor = true;
            try
            {
                Action<string> progress = message => { if (!IsDisposed) BeginInvoke((Action)(() => status.Text = message)); };
                string result = await Task.Run(() => maintenance ? SetupEngine.Uninstall(target, cleanup, progress) : SetupEngine.Install(target, useModern, agreed, progress));
                if (!maintenance) { installed = target; SetMaintenance(); status.Text = result; }
                else { status.Text = result; action.Visible = false; launch.Visible = false; }
            }
            catch (Exception ex) { status.Text = "未完成：" + ex.Message; }
            finally
            {
                busy = false; UseWaitCursor = false; action.Enabled = certificate.Enabled = true;
                browse.Enabled = modern.Enabled = classic.Enabled = consent.Enabled = !maintenance; destination.ReadOnly = maintenance; removeTrust.Enabled = true;
                launch.Enabled = installed != null && File.Exists(Path.Combine(installed, "FolderAlias.exe"));
            }
        }
        void CertificateDialog()
        {
            using (var cert = Trust.Certificate())
            using (var dialog = new Form { Text = "签名证书说明", ClientSize = new Size(670, 370), Font = Font, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false })
            {
                var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.White, Text =
                    "本包使用工具专用的自签名代码签名证书，适合知情的个人分享。它不等同于受公众信任的发行商证书。\r\n\r\n" + cert.Subject + "\r\nSHA-256：" + SetupEngine.Hash(cert.RawData) + "\r\n指纹：" + cert.Thumbprint + "\r\n有效期至：" + cert.NotAfter.ToString("yyyy-MM-dd") +
                    "\r\n\r\n第一层菜单：仅将此证书加入 LocalMachine\\TrustedPeople，需要一次 UAC；不改 Root、开发者模式或脚本执行策略。安装程序不联网，Windows 自身可能按系统策略检查签名。\r\n\r\n清理只处理本安装器导入的证书，并检查其他用户是否仍安装此菜单包。" };
                var clean = new Button { Text = "移除未使用的专用证书…", Dock = DockStyle.Bottom, Height = 42 };
                clean.Click += delegate
                {
                    if (MessageBox.Show(dialog, "让 Windows 请求管理员确认，并在无用户使用时移除此工具导入的专用证书？", "清理证书", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
                    try { int code = Trust.RunElevated(true); MessageBox.Show(dialog, code == 0 ? "已清理（或证书原本不存在）。" : code == 12 ? "至少一个账户仍安装本工具菜单，证书已保留。" : "证书已保留：它不是本工具导入，或操作未获管理员批准。"); }
                    catch (Exception ex) { MessageBox.Show(dialog, ex.Message); }
                };
                dialog.Controls.Add(text); dialog.Controls.Add(clean); dialog.ShowDialog(this);
            }
        }
        internal void Preview(string path)
        {
            Show(); Application.DoEvents(); PerformLayout();
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
            Close();
        }
    }
}
