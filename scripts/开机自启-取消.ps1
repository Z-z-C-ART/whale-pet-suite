# 取消「开机自动启动独立桌宠」
# 快捷方式不永久删除：送入回收站，需要时可以还原。
$ErrorActionPreference = 'Stop'

$startup = [Environment]::GetFolderPath('Startup')
$lnkPath = Join-Path $startup '鲸鱼娘桌宠.lnk'

if (-not (Test-Path $lnkPath)) {
    Write-Host '没有找到开机自启快捷方式，可能本来就没启用。'
    exit 0
}

Add-Type -AssemblyName Microsoft.VisualBasic
[Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile(
    $lnkPath,
    [Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs,
    [Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin
)

Write-Host "已取消开机自启（快捷方式已移入回收站）：$lnkPath"
