# 把「鲸鱼娘桌宠助手」安装到当前用户，并创建桌面 / 开始菜单快捷方式
# 用法：右键 → 使用 PowerShell 运行；或在终端执行 powershell -ExecutionPolicy Bypass -File .\安装到本机.ps1
param(
    [switch]$Autostart,
    [switch]$NoLaunch
)
$ErrorActionPreference = 'Stop'

$source = Join-Path $PSScriptRoot '鲸鱼娘桌宠助手.exe'
if (-not (Test-Path $source)) { throw "找不到 $source" }

$installDir = Join-Path $env:LOCALAPPDATA 'Programs\WhalePetHelper'
$target = Join-Path $installDir '鲸鱼娘桌宠助手.exe'
New-Item -ItemType Directory -Path $installDir -Force | Out-Null

# 先结束正在运行的旧版本，避免文件占用
Get-Process '鲸鱼娘桌宠助手' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
Copy-Item $source $target -Force
Write-Host "已安装到：$target"

$shell = New-Object -ComObject WScript.Shell
function New-Shortcut($path, $description) {
    $lnk = $shell.CreateShortcut($path)
    $lnk.TargetPath = $target
    $lnk.WorkingDirectory = $installDir
    $lnk.Description = $description
    $lnk.IconLocation = $target + ',0'
    $lnk.Save()
    Write-Host "已创建快捷方式：$path"
}

$desktop = [Environment]::GetFolderPath('Desktop')
New-Shortcut (Join-Path $desktop '鲸鱼娘桌宠助手.lnk') '鲸鱼娘桌宠助手'

$programs = [Environment]::GetFolderPath('Programs')
New-Shortcut (Join-Path $programs '鲸鱼娘桌宠助手.lnk') '鲸鱼娘桌宠助手'

if ($Autostart) {
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    New-Item -Path $runKey -Force | Out-Null
    Set-ItemProperty -Path $runKey -Name 'WhalePetHelper' -Value ('"' + $target + '" --tray')
    Write-Host '已开启开机自启（HKCU Run）。'
}

if (-not $NoLaunch) {
    Start-Process -FilePath $target
    Write-Host '已启动助手。'
}
