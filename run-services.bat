@echo off
title Launch Nigerian News Grid Services
cd /d "%~dp0"

REM Leverage PowerShell script for intelligent port collision and Windows exclusion handling
where powershell >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-services.ps1" %*
    exit /b %ERRORLEVEL%
)

echo ==================================================
echo  Starting Nigerian News Grid Admin Dashboard ^& Services
echo ==================================================

echo.
echo [1/5] Building solution...
set MSBUILDDISABLENODEREUSE=1
dotnet build NigerianNewGrid.slnx -nodeReuse:false
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Build encountered an issue (possibly locked files). Shutting down build servers and retrying...
    dotnet build-server shutdown >nul 2>&1
    dotnet build NigerianNewGrid.slnx -nodeReuse:false
)
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Build failed! Please fix errors before starting services.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [2/5] Starting NewsApi (http://localhost:56193)...
start "NewsApi" cmd /k "cd /d "%~dp0" && dotnet run --project src/NewsApi/NewsApi.csproj"

timeout /t 3 /nobreak > nul

echo.
echo [3/5] Starting AdminDashboard (http://localhost:56192)...
start "AdminDashboard" cmd /k "cd /d "%~dp0" && dotnet run --project src/AdminDashboard/AdminDashboard.csproj"

echo.
echo [4/5] Starting NewsScraperService...
start "NewsScraperService" cmd /k "cd /d "%~dp0" && dotnet run --project src/NewsScraperService/NewsScraperService.csproj"

echo.
echo [5/5] Starting TtsWorker...
start "TtsWorker" cmd /k "cd /d "%~dp0" && dotnet run --project src/TtsWorker/TtsWorker.csproj"

echo.
echo ==================================================
echo  All services launched in separate windows!
echo    Admin Dashboard : http://localhost:56192
echo    News API        : http://localhost:56193
echo ==================================================
pause
