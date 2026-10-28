# 鲸鱼娘桌宠 · 两种形态

一只鲸鱼娘，两种活法，外加一个把它们管起来的本地小助手。

| 形态 | 说明 | 什么时候用 |
|---|---|---|
| 🧍 **独立桌宠** | 不打开 DSH 也能在桌面上待机、走动、点击互动 | 平时只想有只宠物陪着 |
| 🐋 **DSH 联动桌宠** | 由 DeepSeek Harness 的插件拉起，跟着 agent 切「思考 / 干活 / 等审批 / 出错 / 完成」，显示余额 | 写代码、跑任务时想一眼看到状态 |
| 🛠 **鲸鱼娘桌宠助手** | 一个约 30 KB 的 WinForms 小工具：状态总览、启停、隐藏/显示、清理重复、开机自启、托盘、右键菜单隐藏 | 懒得记脚本和端口 |

> 两者可以同时存在：平时开独立那只，打开 DSH 后它自己再拉起联动那只；关掉 DSH，联动那只自动退出，独立那只不受影响。

## 目录

```text
assistant/   鲸鱼娘桌宠助手（C# / WinForms）源码、exe、安装脚本、图标
scripts/     独立桌宠的启停 / 状态 / 开机自启脚本（PowerShell + VBS）
docs/        半独立部署说明、GitHub 项目调研
README.md    本文件
LICENSE      MIT（代码）
NOTICE.md    第三方素材说明（图标来自 dsh-pet 立绘）
```

## 快速开始

### 0. 前置

- Windows 10/11
- Node.js 22+
- DeepSeek Harness（桌面版或 `dsh web` 均可）
- 桌宠本体：`npm install -g dsh-pet`，并给 DSH 装上插件：

```powershell
& 'D:\dsh桌面版\resources\runtime\cli\bin\dsh.cmd' plugin --profile desktop add dsh-pet
```

> `dsh web` 用户把 `--profile desktop` 换成 `--profile web`。

### 1. 平时：独立桌宠

- 双击 `scripts/启动桌宠.vbs`（无黑框，单实例保护），或
- 运行助手后点「启动桌宠」。

停止：托盘退出 / `scripts/停止桌宠.ps1` / 助手「停止桌宠」。

### 2. 用 DSH：联动桌宠

正常启动 DSH 即可，插件会拉起桌面宠物；关掉 DSH 那只自动退出。

### 3. 图形化管理（推荐）

```powershell
powershell -ExecutionPolicy Bypass -File .\assistant\安装到本机.ps1
```

装到 `%LOCALAPPDATA%\Programs\WhalePetHelper`，创建桌面 / 开始菜单快捷方式并启动。详见 [assistant/README.md](assistant/README.md)。

助手提供：

- 状态总览：独立实例端口 / PID、DSH 连接、任务状态、余额
- 启动 / 停止（启动前扫描 3080–3115，已在运行只确保显示；重复实例自动清理）
- 隐藏 / 显示（所有独立实例 + DSH 那只）
- 清理重复、打开 DSH、打开配置目录、开机自启、托盘常驻
- 右键菜单：**更多 → 鲸鱼娘助手 → 隐藏桌宠**

> 「隐藏」是**临时收起**：助手启动、发现新的 DSH 宿主、助手退出时都会把 `display` 恢复成 `desktop`，避免下次开 DSH 不出现。

## 为什么有两套脚本 + 一个助手？

最早只有 `scripts/` 里的几个 PowerShell / VBS（启停、状态、开机自启）。用起来嫌麻烦，于是把同样的能力收进 `assistant/`，并额外做了「扫描实例、清理重复、自动恢复隐藏、右键菜单」。

两者互不依赖：只用脚本也能跑；助手只是把脚本的能力图形化，并修掉了多开 / 隐藏后不出现的问题。

## 已知问题与修复

- **启动偶尔失败 / 越点越多**：旧脚本/旧助手只认 `standalone-runtime.json`，且桌宠本身没有单实例锁。现在助手按 `/health` 扫描 3080–3115，启动前清理重复实例，按钮防连点，启动后最长等 45 秒。
- **隐藏后重开 DSH 不出现**：`display: none` 会持久化。现在启动 / 发现新 DSH 宿主 / 退出助手时自动恢复。
- 细节见 [assistant/README.md](assistant/README.md) 的「已知问题与修复」。

## 许可与素材

- 代码：MIT（[LICENSE](LICENSE)）。
- 图标由 [dsh-pet](https://github.com/PC2005-cloud/dsh-pet) 自带立绘裁切，版权归原作者，详见 [NOTICE.md](NOTICE.md)；商用请替换。
- 本仓库**不包含** dsh-pet 的代码与动画素材，只通过 npm / DSH 插件引用；也不包含 Live2D 模型。
