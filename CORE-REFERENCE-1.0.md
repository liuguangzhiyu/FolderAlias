# 文件夹中文别名 · FolderAlias 1.0

给资源管理器中的文件夹设置中文显示名，保留实际英文目录名和路径。使用原生 `desktop.ini`，不注入 Explorer，不安装服务、计划任务或开机启动项，没有网络请求、更新检查或遥测。关闭窗口后退出。

## 开始使用

1. 将 `FolderAlias.exe` 放到准备长期保存的位置，双击运行。
2. 拖入一个或多个文件夹，或点击“添加文件夹”。
3. 在“中文显示名”列填写名字，例如 `剪贴板工具（EcoPaste）`。
4. 勾选需要处理的行，点击“应用勾选项”。每个文件夹分别备份、分别处理，某一项失败不覆盖其他项。
5. 点击“刷新并检测”，再到父文件夹按 F5，必要时关闭并重新打开父文件夹。

“已保存”表示配置写入成功；“Shell 已返回中文显示名”表示 Windows Shell 的显示名接口返回了该名称，并不代表每个已经打开的资源管理器窗口都立即刷新。

批量清单每行一个绝对路径，也可以用 Tab 分隔“路径”和“显示名”。不递归处理子文件夹。表格中的“勾选项”决定应用范围，“选中行”只用于从表格移除。

## 安装、卸载右键菜单

在程序中点击“右键菜单…” → “安装 / 更新菜单”，或双击 `Install-ContextMenu.cmd`。

随后右键一个文件夹 → **显示更多选项 → 设置中文显示名…**。Windows 11 的菜单入口位置会因系统版本和设置而异。本工具注册的是传统普通菜单，不实现 COM / Explorer 注入扩展，不保证出现在新菜单第一层。右键入口一次处理一个文件夹，批量处理通过程序窗口完成。

菜单仅写入当前用户：

```
HKEY_CURRENT_USER\Software\Classes\Directory\shell\FolderAlias.Local
```

默认菜单文字为“设置中文显示名…”。命令直接启动双引号包裹的 EXE 路径并传入 `--open "%1"`，不通过命令解释器处理文件夹名。

无需管理员权限，不修改 `HKEY_LOCAL_MACHINE`。发现不属于本工具的同名键或额外配置会拒绝覆盖/删除。

卸载：程序中点击“卸载菜单”，或双击 `Uninstall-ContextMenu.cmd`。**卸载菜单不会还原文件夹别名，也不会删除备份。** 如果移动 EXE，先卸载旧菜单，到新位置重新安装。整个工具卸载顺序：还原需要还原的文件夹 → 卸载菜单 → 删除 EXE 和源码目录。已完成还原的备份是否继续保留由你决定。

## 已有 desktop.ini 如何处理

- 仅新增或修改 `[.ShellClassInfo]` 下的 `LocalizedResourceName`，其他字段、注释和节保留。原有显示名也包含在备份中。
- 写入格式统一为带 BOM 的 UTF-16 LE，支持中文和有效 Unicode 字符。原编码和原始字节留在备份中，还原时原样恢复。
- 可安全识别 ASCII、带 BOM 的 UTF-8、UTF-16 LE/BE。不猜测无 BOM 的非 ASCII 文件：包括部分 ANSI 中文和无 BOM UTF-8。这类文件会拒绝修改，请先确认编码。重复的目标节/字段和异常格式也会拒绝修改。
- `desktop.ini` 设为 Hidden + System；目标文件夹只增加 ReadOnly 标记，用来启用 Shell 文件夹定制。这不是 NTFS 访问权限，不会把软件内的文件递归设为只读。
- 本工具不设置 `ConfirmFileOp=0`，不清除 Windows 安全来源标记，不改系统信任策略。

## 备份、还原与中断恢复

每个修改过的文件夹内有隐藏目录 `.FolderAlias.Backup`。请保留它。`state.xml` 包含原始配置字节（Base64）、SHA-256、配置文件属性、修改时间，以及原文件夹的 ReadOnly 状态。历史状态以 `history-*.xml` 保留，不自动清理。

“还原勾选项”恢复**本轮首次设置前**的配置。重复换别名不会覆盖这一基线。原来没有 `desktop.ini` 的文件夹，还原时删除本工具创建的该文件，恢复原 ReadOnly 标记。原来有配置则恢复原始字节、原编码、记录的属性和修改时间。文件夹其他属性不作回退，文件夹的时间戳不承诺完全不变。

每次改动前先持久化事务备份，配置文件通过同目录临时文件和原生文件替换提交。正常异常会尝试回滚。遇到进程被结束或回滚无法完成等中断，若备份中有 Pending 状态，可在窗口中点击“恢复中断操作”，恢复到**那次操作开始前**。这与恢复最初设置是两个不同操作。恢复后可继续点击“还原勾选项”。真实断电及硬件存储故障没有经过实测，不能承诺在备份本身损坏时仍可自动恢复。

如果软件更新或其他程序改过配置、配置消失，或相关属性发生变化，普通应用/还原会停止，避免覆盖新改动。遇到这种情况，先备份当前 `desktop.ini`，对照备份人工合并；本版本不提供强制覆盖按钮。它也不常驻监控、不自动抢写，因此软件更新可能覆盖中文别名。

备份绑定 NTFS 文件夹标识，防止误把 A 文件夹的备份恢复到 B。**同一卷内普通移动通常保留此标识；跨卷复制、重新创建目录等情况不保证自动恢复，标识不匹配时拒绝操作。** 备份是恢复资料，不是同步格式。不要单独移动/编辑备份目录。

如需人工提取备份原文，可在 PowerShell 中读取（仅提取到你指定的新文件，不自动覆盖现有配置）：

```powershell
[xml]$state = Get-Content -LiteralPath 'E:\Tools\EcoPaste\.FolderAlias.Backup\state.xml' -Raw
# 先检查 $state.BackupState.Original.Exists 是否为 true；false 表示原先没有配置。
$bytes = [Convert]::FromBase64String($state.BackupState.Original.Bytes)
$destination = 'C:\你指定的恢复目录\original-desktop.ini'
if (Test-Path -LiteralPath $destination) { throw '目标已存在，停止提取' }
[IO.File]::WriteAllBytes($destination, $bytes)
```

手工恢复涉及文件属性和可能的外部改动；优先使用程序的冲突检测及还原功能。

## Windows 11 / 26H2 的实际限制

开发机登记版本为 **26H2，构建 26300.9457**。最终实测范围与结果见 `TEST-REPORT.md`。没有将这一机器的结果推断为所有 26H2 更新或所有文件夹都兼容。

- 这是 Shell 的显示名机制，实际磁盘目录名保持英文。地址栏的可视面包屑可能中文化；编辑完整路径、终端和应用配置仍使用真实英文路径。
- Explorer 列表、搜索、快速访问、打开文件对话框和第三方文件管理器可能采用不同的显示逻辑和缓存。不保证都使用同一个别名，也不保证即时刷新。
- 原生刷新通知不等于清空所有缓存。工具不强杀 Explorer，不重启系统。可手动按 F5 或重开父文件夹。
- 微软说明：从 2026-06-09 安全更新起，不受信任来源的 `desktop.ini` 可能被忽略，包括带 Mark-of-the-Web、部分远程来源和网络路径。本工具不关闭这个保护，并拒绝编辑带任何附加数据流的配置文件（包括来源标记），避免在替换时丢失安全信息。
- 当前版本仅处理**本地 NTFS 固定/可移动盘**的普通文件夹，完整目标路径最多 **180 字符**，为备份和临时文件预留路径空间。不处理 UNC、映射网络盘、盘符根目录、Windows 系统目录、链接/联接路径、硬链接配置、加密或脱机配置。不需要通过管理员权限绕过这些限制。
- 标准用户必须对目标文件夹有写入权限。未通过安全检查或权限不足时逐项报告失败。
- 有些软件会把显示名用于自己的界面或快捷方式文本，因此它们的界面文字可能相应变化；程序没有执行实际文件夹重命名。

## 运行环境与源码

应用是 C# / WinForms 的 **Windows x64 单 EXE**，使用 Windows 已有的 **.NET Framework 4.8**。它不是附带完整运行时的 self-contained .NET 包。本机已检测到 4.8（Release 533509），无需下载 SDK 或安装运行时。

源码在 `src`，测试源码在 `tests/CoreTests.cs`。没有 NuGet、第三方库或联网构建步骤。二进制未作 Authenticode 签名；不要为运行它关闭系统安全功能。

离线重新构建：

```powershell
# 在解压目录中运行；使用系统已有的 C# 编译器。
.\build.ps1
# 同时编译测试程序：
.\build.ps1 -Tests
# 请为每轮测试指定一个新的空目录；测试不会自动清除原有夹具。
.\tests\FolderAlias.Tests.exe 'C:\你指定的测试目录\run01'
```

`build.ps1` 调用 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`，产物为当前目录的 `FolderAlias.exe`。请先关闭正在运行的旧 EXE。如果本机策略禁止执行 PowerShell 脚本，可查看脚本中的编译命令自行执行，无需修改全局执行策略。

## 命令行

```text
FolderAlias.exe --open "E:\Tools\EcoPaste"
FolderAlias.exe --inspect "E:\Tools\EcoPaste"
FolderAlias.exe --apply "E:\Tools\EcoPaste" "剪贴板工具（EcoPaste）" --yes
FolderAlias.exe --restore "E:\Tools\EcoPaste" --yes
FolderAlias.exe --recover "E:\Tools\EcoPaste" --yes
FolderAlias.exe --refresh "E:\Tools\EcoPaste"
FolderAlias.exe --install-menu --yes
FolderAlias.exe --uninstall-menu --yes
FolderAlias.exe --menu-status
```

修改命令要求显式 `--yes`。自动化运行附加 `--report "新报告文件的绝对路径"`，成功返回 0，失败返回 1；报告须为尚不存在的 `.txt` 或 `.log` 文件，在修改前创建，不覆盖现有文件。请将报告放到目标配置文件之外。未指定报告时使用消息框显示结果。PowerShell 中运行 GUI EXE 做自动化时，用 `Start-Process -Wait -PassThru` 等待实际完成，不要仅靠启动命令返回判断成功。

## 原生机制参考

- [Microsoft Learn：使用 desktop.ini 自定义文件夹](https://learn.microsoft.com/en-us/windows/win32/shell/how-to-customize-folders-with-desktop-ini)
- [Microsoft Learn：SHChangeNotify](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shchangenotify)
- [Microsoft Learn：SIGDN 显示名](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/ne-shobjidl_core-sigdn)
- [Microsoft：Windows 11 右键菜单机制](https://blogs.windows.com/blog/2021/07/19/extending-the-context-menu-and-share-dialog-in-windows-11/)
- [Microsoft Support：2026 年 6 月 desktop.ini 信任限制](https://support.microsoft.com/en-au/servicing/os/windows/docs/2026/06/custom-folder-icons-or-localized-folder-names-might-not-appear-after-installing-the-june-2026-window)
