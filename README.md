# 文件夹中文别名 · FolderAlias 1.2.0 个人分享版

给文件夹设置中文显示名，保留实际英文目录名、路径和软件文件。支持批量设置、Unicode、原配置备份、一键还原、冲突检测和原生刷新。

**直接下载：** [Windows 11 x64 安装 EXE](https://github.com/liuguangzhiyu/FolderAlias/releases/download/v1.2.0/FolderAlias-Setup-1.2.0-x64.exe) · [完整源码 ZIP](https://github.com/liuguangzhiyu/FolderAlias/releases/download/v1.2.0/FolderAlias-1.2.0-source.zip) · [SHA-256 校验](https://github.com/liuguangzhiyu/FolderAlias/releases/download/v1.2.0/SHA256SUMS.txt) · [实测范围](TEST-REPORT.md)

也可打开 [1.2.0 版本下载页](https://github.com/liuguangzhiyu/FolderAlias/releases/tag/v1.2.0)，在附件（Assets）中选择 `FolderAlias-Setup-1.2.0-x64.exe`。只想使用工具时，无需下载源码。

本仓库同时保留源码与 1.2.0 安装产物，便于备份和下载。源码使用 MIT 许可证；签名私钥、账户凭据、开发工具与测试临时目录不在仓库中。

## 分享和安装

只需分享 **FolderAlias-Setup-1.2.0-x64.exe**。接收者普通双击，选择新的或空的安装目录，选菜单类型，再点击安装。不要对整个安装器选择“以管理员身份运行”；只有证书步骤单独请求管理员权限，让菜单正确注册给使用者本人。

安装包内置主程序、原生菜单 DLL、签名身份包、公用证书及说明。工具运行时不下载文件、不检查更新、无遥测，不需要开发者模式、SDK、Visual C++ 运行库或额外 .NET 下载。使用 Windows 11 已有的 .NET Framework 4.8；目标为 **Windows 11 x64**，未支持 ARM64 或 Windows 10。

默认位置为当前用户的 `%LocalAppData%\Programs\FolderAlias`；也可以选有写入权限的本地 NTFS 固定盘目录，例如 `E:\Tools\FolderAlias`。安装路径最长 170 字符，不支持链接、联接和 Windows 系统目录。安装不会自动设置文件夹别名。

### 第一层右键菜单

选择第一层菜单，阅读并勾选证书同意项，再点击安装。安装器会解压内置文件；如缺少专用证书，单独请求一次 Windows UAC，将其加入 **LocalMachine\TrustedPeople（本机受信任的人）**；随后为当前用户注册签名 MSIX 身份包和原生 IExplorerCommand，通过 COM 代理激活校验组件，发送 Shell 刷新通知并添加系统卸载入口。

随后右键普通文件夹 → **设置中文显示名…**。最多一次传入 64 个文件夹；更多文件夹可从主窗口批量导入。组件只启动旁边的 EXE，不在 Explorer 内运行 .NET，不执行文件夹提供的命令。COM 代理由 Windows 按需启动、空闲回收；工具没有服务、计划任务、开机启动或自己的常驻监视进程。

菜单布局、排序、应用分组及是否放入附加子菜单由 Windows 的版本和用户设置决定，不能承诺固定首位。注册后可能需要重开资源管理器窗口，必要时注销再登录；安装器不强制结束 Explorer。组织策略可能禁止侧载或证书导入，遇到这种情况会报告失败，不降低安全策略。

**签名说明：** 本包使用专用自签名代码签名证书，适合了解来源的个人分享。它不是公众信任的发行商证书，下载或初次启动仍可能出现 SmartScreen / 未知发布者提示，不能保证每台电脑都无提示安装。大范围正式发行应换用受信任的签名服务或 Microsoft Store。

- 主题：`CN=FolderAlias Local`
- 指纹：`3A126F6F9C8F0F433982D05A88731FE6FC9386ED`
- 有效期至 2029-10-12；没有联网时间戳，过期后应重新发布有效签名的版本。
- 是非 CA 的代码签名证书，不加入 Root，不关闭安全检查。
- 安装包只有公用证书，**不含私钥或签名密码**。
- 工具自身无网络功能；Windows 可能按系统现有策略进行签名或信誉检查。

### 普通右键菜单（无需管理员）

选择普通菜单，无需导入证书。Windows 11 通常通过 **显示更多选项 → 设置中文显示名…** 使用。它仅写当前用户 `HKCU\Software\Classes\Directory\shell\FolderAlias.Local`。右键入口一次传入一个文件夹；主窗口支持批量操作。

## 使用、备份和还原

点击“添加文件夹（多选）”，在系统窗口中按住 Ctrl 或 Shift 一次选择多个文件夹，也可以把多个文件夹一起拖入窗口。双击“中文显示名”列填写名字，再点击“应用勾选项”。不需要准备或粘贴批量清单，不递归处理子文件夹。

“全选 / 全不选”直接控制每一行的应用勾选框，底部显示已勾选数量。拖动或 Ctrl/Shift 选中行用于列表选择；真正应用范围以勾选框为准。列宽随窗口调整，中文名和状态可自动换行，行高自动适应内容；禁用手动拉伸行列边界，避免拖选时误拉大格子。

1.2.0 新增专用“文件夹＋中”图标，应用于主程序、安装程序、窗口和右键菜单入口。升级仍需先从原安装位置卸载旧版，再安装新版，已有别名及备份保留。

已有 desktop.ini 时，仅修改 `[.ShellClassInfo]` 下的 `LocalizedResourceName`，保留其他节、字段、注释和图标设置。首次操作前备份原始字节、属性及时间；输出使用带 BOM 的 UTF-16 LE，支持中文和有效 Unicode。

原配置支持 ASCII、带 BOM 的 UTF-8、UTF-16 LE/BE。不猜测无 BOM 的非 ASCII 编码；重复目标节、异常文件等会被拒绝。desktop.ini 增加 Hidden + System，文件夹只增加 ReadOnly 的 Shell 定制标记，不递归改变内部文件权限。

“还原勾选项”恢复本轮首次设置前的配置。原来没有 desktop.ini 时，删除本工具创建的配置；原来有则恢复原始字节、记录的属性和修改时间。重复换名字不会覆盖首次基线。备份 `.FolderAlias.Backup` 和历史保留，不自动清理。不要单独移动、编辑或删除备份。

软件更新或第三方修改配置后，工具停止应用/还原，避免覆盖新改动。它不会后台抢写配置。“恢复中断操作”针对事务处于 Pending 的异常中断，恢复到那次操作之前；不等同于还原最初基线。真实断电及硬件存储故障未实测。

## Windows 兼容性限制

开发机为 **Windows 11 26H2，26300.9457**。这不代表所有 Windows 构建、文件夹和 Explorer 视图都兼容。实测范围见源码包 `TEST-REPORT.md`。

- desktop.ini 是 Shell 显示名机制，不是真改名；完整路径、终端和应用配置仍使用真实英文路径。
- Explorer 列表、搜索、快速访问、文件选择窗口和第三方管理器可能采用不同缓存或忽略别名。
- “已保存”只代表配置写入成功；“Shell 已返回中文显示名”是接口检测结果，不代表每个已打开窗口都已刷新。可按 F5 或重开父文件夹。
- 微软说明，从 2026-06-09 相关安全更新起，不受信任来源的 desktop.ini 可能被忽略，包括网络来源或带 Mark-of-the-Web 的配置。工具不清除来源标记、不关闭保护，拒绝编辑带附加数据流的配置。
- 别名功能仅处理本地 NTFS 固定/可移动盘普通文件夹，完整目标路径最长 180 字符。不处理 UNC、网络映射、根目录、系统目录、链接/联接、硬链接配置、加密或离线文件。
- 软件更新可能覆盖别名；工具不监控。备份绑定文件夹标识，跨卷复制或重建目录后不保证可自动还原。

## 卸载和更换位置

从 Windows“设置 → 应用 → 已安装的应用 → 文件夹中文别名 → 卸载”，或再次运行安装包/安装目录的 `FolderAlias.Setup.exe`。若要恢复英文显示，请先打开主程序还原目标文件夹。

卸载仅移除当前用户的菜单及本工具安装且未被改动的文件。后来加入的文件、已设置的中文别名和 `.FolderAlias.Backup` 均保留。文件被占用或外部修改时保留并说明原因。存在旧版普通菜单时，只在原程序还存在、菜单未被第三方更改时恢复旧入口。

可勾选“同时移除专用证书”。此步骤需要管理员确认，仅处理安装器有导入记录的同一证书；若其他账户仍安装本工具菜单则保留。未勾选时证书保留，可日后从“证书说明 / 清理”移除。安装失败后若证书已导入，也从这里清理。

安装器不覆盖已有目录内容；更换位置、菜单类型或升级时，先卸载再安装。卸载使用短暂的临时副本，退出后尝试删除该副本，不注册后台任务。

## 完整源码与构建

源码另附 `FolderAlias-1.2.0-source.zip`，无需发给只想使用工具的人。

- `src/`：C# 主程序、备份事务、Shell 调用和传统菜单。
- `native/`：C 的原生菜单桥，CLSID `ED3017E2-5A5A-4CD5-A714-EC67642842A7`。
- `installer/`：离线安装、卸载、证书助手和界面。
- `packaging/`：MSIX 清单、Windows 打包 API 构建器、密钥生成脚本。
- `assets/`：原创 SVG 图标及各尺寸 ICO、PNG；`packaging/MakeIcon.cs` 可重新生成图标。
- `tests/`：核心、安装器及 Shell 接口测试。
- `CORE-REFERENCE-1.0.md`：旧版核心行为详细参考；安装和签名说明以本文件为准。

主程序运行 `build.ps1 -Tests` 即可编译，只使用 Windows 自带的 .NET Framework C# 编译器。

完整安装包使用 `build-release.ps1`，另需 TCC 0.9.27 win64 和微软 Windows SDK 的 x64 SignTool（含其同目录依赖）。只用于开发，不打进最终安装包。本版使用 Microsoft.Windows.SDK.BuildTools 10.0.28000.2705 中的 SignTool，可解压使用，不必安装整套 SDK。官方来源：

- https://download.savannah.gnu.org/releases/tinycc/tcc-0.9.27-win64-bin.zip
- https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools/10.0.28000.2705

```powershell
# PowerShell 7；自行选择路径，构建目录必须尚不存在。
.\packaging\NewSigningKey.ps1 -KeyDirectory C:\Build\PrivateKey
.\build-release.ps1 -Tcc C:\Build\tcc\tcc.exe `
  -SignTool C:\Build\SDK\x64\signtool.exe `
  -KeyDirectory C:\Build\PrivateKey -BuildDirectory C:\Build\NewBuild `
  -OutputExe C:\Build\FolderAlias-Setup.exe
```

生成密钥的脚本只写指定私有目录，不导入证书。密码经当前用户 DPAPI 保存，私钥不嵌入 EXE。重新生成密钥会改变指纹，应同步更新说明；不要把私钥目录一起分享。正式签名服务需相应调整签名步骤。

## 原生机制资料

- [Microsoft：文件资源管理器菜单扩展](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/integrate-packaged-app-with-file-explorer)
- [Microsoft：给非打包应用授予身份](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps)
- [Microsoft：MSIX 证书信任和部署问题](https://learn.microsoft.com/en-us/windows/msix/msix-troubleshooting-guide)
- [Microsoft：desktop.ini 文件夹定制](https://learn.microsoft.com/en-us/windows/win32/shell/how-to-customize-folders-with-desktop-ini)
- [Microsoft：2026 年 6 月 desktop.ini 信任限制](https://support.microsoft.com/en-au/servicing/os/windows/docs/2026/06/custom-folder-icons-or-localized-folder-names-might-not-appear-after-installing-the-june-2026-window)
