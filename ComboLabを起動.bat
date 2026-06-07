@echo off
setlocal
cd /d "%~dp0"

set "DOTNET_CLI_HOME=%CD%\.dotnet"
set "NUGET_PACKAGES=%CD%\.nuget\packages"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo .NET 8 Desktop Runtime or SDK is required.
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

dotnet build ComboLab.csproj --configuration Release --nologo
if errorlevel 1 (
    echo.
    echo Build failed.
    pause
    exit /b 1
)

start "" "%CD%\bin\Release\net8.0-windows\ComboLab.exe"
exit /b 0
