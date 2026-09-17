@echo off
title IoBuild Cloudflare Tunnel
echo ===================================================
echo   Iniciando Cloudflare Tunnel para IoBuild
echo ===================================================
echo.

if not exist "%USERPROFILE%\.tools\cloudflared.exe" (
    echo Descargando cloudflared.exe...
    if not exist "%USERPROFILE%\.tools" mkdir "%USERPROFILE%\.tools"
    powershell -Command "Invoke-WebRequest -Uri 'https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe' -OutFile '%USERPROFILE%\.tools\cloudflared.exe'"
)

echo Conectando http://localhost:8081 a la red de Cloudflare...
echo.
"%USERPROFILE%\.tools\cloudflared.exe" tunnel --url http://localhost:8081
pause
