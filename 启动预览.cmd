@echo off
setlocal
cd /d "%~dp0"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_NOLOGO=1"
if exist "%~dp0.tools\dotnet\dotnet.exe" (
    "%~dp0.tools\dotnet\dotnet.exe" run --project "%~dp0src\ShuatiKing.App"
) else (
    dotnet run --project "%~dp0src\ShuatiKing.App"
)
if errorlevel 1 pause
