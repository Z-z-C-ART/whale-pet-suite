# 启用「开机自动启动独立桌宠」
# 在启动文件夹放一个快捷方式，登录 Windows 后静默启动桌宠。
$ErrorActionPreference = 'Stop'

$launcher = Join-Path $PSScriptRoot '启动桌宠.vbs'
if (-not (Test-Path $launcher)) {
    Write-Host "找不到启动脚本：$launcher"
    exit 1
}

$startup = [Environment]::GetFolderPath('Startup')
$lnkPath = Join-Path $startup '鲸鱼娘桌宠.lnk'
$wscript = Join-Path $env:WINDIR 'System32\wscript.exe'

$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut($lnkPath)
$lnk.TargetPath = $wscript
$lnk.Arguments = '"' + $launcher + '"'
$lnk.WorkingDirectory = $PSScriptRoot
$lnk.WindowStyle = 7
$lnk.Description = '鲸鱼娘桌宠（独立模式，开机自启）'
$lnk.Save()

Write-Host "已创建开机自启快捷方式：$lnkPath"
Write-Host '下次登录 Windows 时会自动启动桌宠；想取消运行「开机自启-取消.ps1」。'
