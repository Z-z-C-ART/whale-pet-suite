# 独立桌宠脚本

不依赖助手，纯 PowerShell / VBS，双击或命令行都能用。

| 文件 | 作用 |
|---|---|
| `启动桌宠.vbs` | 静默启动独立桌宠（无黑框；已运行则直接退出，不会开出第二只） |
| `停止桌宠.ps1` | 优雅停止独立桌宠（读 `standalone-runtime.json` 的端口 + token，`POST /shutdown`） |
| `检查状态.ps1` | 打印独立桌宠端口/PID/健康状态、DSH 插件安装情况、桌面宠物窗口 |
| `开机自启-启用.ps1` | 在「启动」文件夹放快捷方式，登录后自动启动 |
| `开机自启-取消.ps1` | 取消开机自启（快捷方式进回收站，不是永久删除） |

## 用法

```powershell
# 启动（双击也可以）
wscript.exe .\启动桌宠.vbs

# 停止
powershell -ExecutionPolicy Bypass -File .\停止桌宠.ps1

# 看状态
powershell -ExecutionPolicy Bypass -File .\检查状态.ps1

# 开机自启
powershell -ExecutionPolicy Bypass -File .\开机自启-启用.ps1
powershell -ExecutionPolicy Bypass -File .\开机自启-取消.ps1
```

## 注意

- 这些脚本只管理**独立桌宠**；DSH 联动那只由 DSH 插件自己启停。
- `启动桌宠.vbs` 通过 `.dsh\dsh-pet\standalone-runtime.json` 判断是否已运行；如果你遇到“点了没反应/多开”，建议改用 `assistant/` 里的助手——它按端口扫描，能识别并清理重复实例。
- 停止脚本需要桌宠运行状态文件里的 token；如果文件被清理，脚本会提示“未在运行”，此时可用任务管理器结束 `node.exe`（命令行含 `standalone.js`）或使用助手的「清理重复」。
