# 鲸鱼娘桌宠助手

一个约 30 KB 的本地小工具（C# / .NET Framework WinForms，双击即用，不需要装运行时），把之前那 5 个 PowerShell/VBS 脚本的功能收进一个窗口，并给桌宠右键菜单加了「隐藏」。

## 能做什么

| 功能 | 说明 |
|---|---|
| 状态总览 | 独立桌宠是否在跑（端口 / PID）、DSH 是否连上、当前任务状态与余额 |
| 启动 / 停止 | 启动前扫描 3080–3115 识别已有实例：已在运行就只「确保显示」，重复实例自动清理；停止会停掉**所有**实例 |
| 隐藏 / 显示 | 同时隐藏或恢复所有独立实例 + DSH 那只 |
| 清理重复 | 一键停掉所有独立桌宠实例（修掉历史遗留的多开） |`n| 打开 DSH | 优先用 `dsh://open` 唤起，失败再直接启动桌面版 |
| 打开配置目录 | 直接打开 `%USERPROFILE%\.dsh\dsh-pet` |
| 开机自启 | 勾选即写入 `HKCU\...\Run`，登录后静默到托盘；取消即删除 |
| 托盘常驻 | 关窗口只是最小化到托盘；托盘菜单可显示/隐藏宠物、启停、退出 |
| 右键菜单 | 在桌宠右键菜单的「更多 → 鲸鱼娘助手 → 隐藏桌宠」里加了一项 |

> 关于右键菜单的位置：`dsh-pet` 只给第三方开放「更多」这一层（`POST /menu/item`），所以无法插到第一层。点击后助手会收到长轮询事件，把两只宠物一起隐藏；恢复用本窗口或托盘里的「显示桌宠」。

## 安装

```powershell
powershell -ExecutionPolicy Bypass -File .\安装到本机.ps1
```

- 安装位置：`%LOCALAPPDATA%\Programs\WhalePetHelper\鲸鱼娘桌宠助手.exe`
- 会创建桌面和开始菜单快捷方式，然后启动。
- 加 `-Autostart` 可同时开启开机自启；`-NoLaunch` 只安装不启动。

**为什么不直接在交付目录里双击？** 实测在这台机器上，从 `D:\DSH交付` 直接启动的程序会被当前环境的执行限制拦住对注册表 / 启动目录的写入（开机自启会报「访问被拒绝」）；装到 `%LOCALAPPDATA%` 后一切正常。程序本身也内置了「首次运行自动搬迁」的兜底，但推荐直接用安装脚本。

## 卸载

```powershell
Get-Process '鲸鱼娘桌宠助手' -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'WhalePetHelper' -ErrorAction SilentlyContinue
Remove-Item "$env:LOCALAPPDATA\Programs\WhalePetHelper" -Recurse -Force
Remove-Item "$env:USERPROFILE\Desktop\鲸鱼娘桌宠助手.lnk" -Force -ErrorAction SilentlyContinue
Remove-Item "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\鲸鱼娘桌宠助手.lnk" -Force -ErrorAction SilentlyContinue
```

（删除目录前请确认没有正在运行的实例。）

## 实现要点

- 独立桌宠的端口 / 令牌来自 `%USERPROFILE%\.dsh\dsh-pet\standalone-runtime.json`；停止用 `POST /shutdown` + `x-dsh-pet-token`。
- DSH 宿主端口每次启动随机，助手用「枚举本机监听端口 → 探 `/dsh-pet-7340/state`」的方式发现（结果缓存 30 秒）。
- 隐藏 / 显示：`POST /dsh-pet-7340/pet-display`，`{"id":"main","display":"none"|"desktop"}`；独立桌宠那条要带令牌。
- 右键菜单：`POST /menu/item` 注册 `{owner:"鲸鱼娘助手", labels:["隐藏桌宠"]}`，再长轮询 `GET /menu/events?owner=...&wait=15000`；退出时 DELETE 注销，避免留下无效项。
- 单实例：命名 Mutex + 命名事件，第二次双击会把已有窗口叫到前面。

## 源码与编译

源码：[src/PetHelper.cs](src/PetHelper.cs)

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:winexe /optimize+ `
  /win32icon:鲸鱼娘桌宠助手.ico `
  /out:鲸鱼娘桌宠助手.exe `
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
  /r:System.Web.Extensions.dll /r:System.Net.Http.dll /r:Microsoft.VisualBasic.dll `
  src\PetHelper.cs
```

## 图标

- 素材取自 `dsh-pet` 自带的 **`assets/pic/notify-done.png`**（和桌宠用的是同一张鲸鱼娘立绘，本身带透明通道，无文字）。
- [make-icon.ps1](make-icon.ps1) 会裁掉透明边、居中留白，生成 **16/24/32/48/64/128/256** 七个尺寸的 [鲸鱼娘桌宠助手.ico](鲸鱼娘桌宠助手.ico)，并用 `/win32icon` 嵌进 exe；窗口标题栏和托盘图标优先从 exe 里取（`Icon.ExtractAssociatedIcon`），所以三处图标一致。
- 预览：[icon-preview.png](icon-preview.png)；重新生成：`powershell -ExecutionPolicy Bypass -File .\make-icon.ps1`。

## 常见问题

- **状态显示「独立桌宠：未运行」**：先点「启动桌宠」；若仍失败，检查 `npm ls -g dsh-pet` 与 `%APPDATA%\npm\dsh-pet-standalone.cmd` 是否存在。
- **「DSH：未运行」但 DSH 开着**：端口扫描最多需要几秒，点「刷新状态」；确认 `dsh-pet` 插件已装进当前 profile。
- **隐藏后找不到宠物**：右键托盘图标 →「显示桌宠」。
- **勾选开机自启失败**：确认程序是从 `%LOCALAPPDATA%\Programs\WhalePetHelper` 运行的（见上面的安装说明）。

## 仓库结构

```text
鲸鱼娘桌宠助手.exe     已编译好的程序（可直接运行）
安装到本机.ps1         安装到 %LOCALAPPDATA% + 创建快捷方式
make-icon.ps1          从鲸鱼娘立绘生成多尺寸 ico
鲸鱼娘桌宠助手.ico     应用图标（已嵌入 exe）
icon-preview.png       图标预览
src/PetHelper.cs       全部源码（C# / WinForms）
```

## 许可

- 代码：MIT，见 [LICENSE](LICENSE)。
- 图标衍生自 dsh-pet 自带立绘，版权归原作者；详见 [NOTICE.md](NOTICE.md)。商用请自行替换素材。
## 已知问题与修复（v1.0.1）

早期版本有两个相关 bug：

1. **「启动桌宠」偶尔失败**：旧逻辑只认 `standalone-runtime.json` 里的端口。这个文件在「先启动、再停止其中一个实例」后会消失（停止时被清理），于是助手认为没在运行，点击启动后虽然进程起来了却检测不到；另外如果宠物处于隐藏状态（`display: none`），启动后也看不到窗口，看起来就是“没启动”。
2. **多次点击后开 DSH 出现多只**：旧逻辑没有单实例检测，每次点击都会 `spawn` 一个新的 `dsh-pet-standalone` 进程（端口依次 3080、3081…），这些实例都会各自渲染一只宠物；DSH 再拉起自己的那只，就成了“多只”。

现在的做法：

- 用 `/health` 扫描 **3080–3115** 识别所有实例（返回 `name: dsh-pet-standalone` 才算），不依赖 runtime 文件；
- 启动前若已有 1 个实例：只确保 `display: desktop`（可见）；
- 若已有多个：先全部停掉再启动一个；
- 启动后轮询 45 秒确认，并防止按钮重复点击（按钮会禁用 + 内部 `starting` 锁）；
- 停止 / 清理重复：优雅 shutdown 拿得到 token 的那只，其余按 PID 结束；
- 状态栏会显示 `运行中（已隐藏）` 或 `运行中 ×N ⚠ 重复`。
## 关于「隐藏」的语义（v1.0.2）

`dsh-pet` 的隐藏是通过把 `display` 写成 `none` 实现的，这个值会持久化到 `main-config.jsonc`——所以**以前隐藏过一次，之后重开 DSH 就不会再自动出现**（插件读到 none 就不拉起桌面宠物）。

现在助手把「隐藏」当成**临时收起**处理：

- 助手启动时：如果发现 `display: none`，自动恢复成 `desktop`；
- 发现新的 DSH 宿主时（DSH 重启会换随机端口）：同样自动恢复；
- 助手退出时：把仍是 `none` 的显示状态恢复成 `desktop`。

也就是说：点「隐藏」只影响当前这次运行；重开 DSH、重启助手或退出助手后，宠物都会回来。如果你需要长期不显示，去插件设置里改 `display`，或者不要启动助手。