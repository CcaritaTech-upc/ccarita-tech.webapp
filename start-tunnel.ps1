# ===================================================
# IoBuild - Cloudflare Tunnel Launcher
# ===================================================

$toolsDir = "$env:USERPROFILE\.tools"
$cloudflared = Join-Path $toolsDir "cloudflared.exe"

if (!(Test-Path $cloudflared)) {
    Write-Host "Descargando cloudflared.exe..." -ForegroundColor Cyan
    if (!(Test-Path $toolsDir)) { New-Item -ItemType Directory -Path $toolsDir -Force | Out-Null }
    Invoke-WebRequest -Uri "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe" -OutFile $cloudflared
}

Write-Host "Iniciando túnel seguro de Cloudflare hacia http://localhost:8081..." -ForegroundColor Green
Write-Host "Copia el enlace .trycloudflare.com que aparezca a continuacion:`n" -ForegroundColor Yellow

& $cloudflared tunnel --url http://localhost:8081
