# 停止独立桌宠（仅影响 dsh-pet-standalone，不影响 DSH 自己的桌宠）
$ErrorActionPreference = 'Stop'

$runtime = Join-Path $env:USERPROFILE '.dsh\dsh-pet\standalone-runtime.json'
if (-not (Test-Path $runtime)) {
    Write-Host '独立桌宠未在运行（没有找到 standalone-runtime.json）。'
    exit 0
}

$info = Get-Content $runtime -Raw -Encoding UTF8 | ConvertFrom-Json
$port = $info.port
$token = $info.token

# 先确认进程还活着，避免文件残留时误报
$alive = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
if (-not $alive) {
    Write-Host "端口 $port 没有在监听，桌宠可能已经退出。"
    exit 0
}

$resp = & curl.exe -s -m 6 -X POST -H "x-dsh-pet-token: $token" "http://127.0.0.1:$port/shutdown"
if ($LASTEXITCODE -ne 0) {
    Write-Host "停止失败（curl 退出码 $LASTEXITCODE）。可改为右键托盘图标退出。"
    exit 1
}

Start-Sleep -Milliseconds 800
if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) {
    Write-Host "已发送停止请求，但端口 $port 仍在监听；请右键托盘图标退出。"
    exit 1
}
Write-Host "独立桌宠已退出（端口 $port）。"
