# 检查半独立桌宠的部署状态
$ErrorActionPreference = 'Continue'

Write-Host '=== 1) 独立桌宠（不需要 DSH）==='
$runtime = Join-Path $env:USERPROFILE '.dsh\dsh-pet\standalone-runtime.json'
if (Test-Path $runtime) {
    $info = Get-Content $runtime -Raw -Encoding UTF8 | ConvertFrom-Json
    $alive = Get-NetTCPConnection -LocalPort $info.port -State Listen -ErrorAction SilentlyContinue
    Write-Host ("  运行时文件：{0}" -f $runtime)
    Write-Host ("  端口：{0}　进程 PID：{1}　监听中：{2}" -f $info.port, $info.pid, [bool]$alive)
    if ($alive) {
        $state = & curl.exe -s -m 5 "http://127.0.0.1:$($info.port)/dsh-pet-7340/state"
        Write-Host ("  状态接口：{0}" -f ($state -replace '\s+', ' '))
    }
} else {
    Write-Host '  未运行（没有 standalone-runtime.json）'
}

Write-Host ''
Write-Host '=== 2) DSH 桌宠插件（随 DSH 启停）==='
$profiles = @('desktop', 'web')
foreach ($p in $profiles) {
    $pkg = Join-Path $env:USERPROFILE ".dsh\profiles\$p\package.json"
    if (Test-Path $pkg) {
        $j = Get-Content $pkg -Raw -Encoding UTF8 | ConvertFrom-Json
        $has = $j.dependencies.PSObject.Properties.Name -contains 'dsh-pet'
        Write-Host ("  profile {0}：{1}" -f $p, $(if ($has) { '已安装 dsh-pet' } else { '未安装' }))
    }
}

Write-Host ''
Write-Host '=== 3) 桌面上的桌宠窗口 ==='
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public class PetWinCheck {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
}
"@ -ErrorAction SilentlyContinue
$found = 0
[PetWinCheck]::EnumWindows({
    param($h, $l)
    $procId = 0
    [void][PetWinCheck]::GetWindowThreadProcessId($h, [ref]$procId)
    $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
    if ($proc -and $proc.ProcessName -eq 'electron') {
        $sb = New-Object System.Text.StringBuilder 256
        [void][PetWinCheck]::GetWindowText($h, $sb, 256)
        if ($sb.ToString() -like '*桌宠*' -and [PetWinCheck]::IsWindowVisible($h)) {
            Write-Host ("  窗口 PID {0}：{1}" -f $procId, $sb.ToString())
        }
    }
    return $true
}, [IntPtr]::Zero) | Out-Null

Write-Host ''
Write-Host '说明：DSH 开着时，桌面上允许同时出现两只——一只是独立桌宠（无 agent 功能），'
Write-Host '      另一只是 DSH 自己拉起的（显示任务状态、审批提醒）。关掉 DSH 后后者会自动退出。'
