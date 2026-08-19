@echo off
title Launch Nigerian News Grid Services
echo ==================================================
echo  Starting Nigerian News Grid Admin Dashboard ^& Services
echo ==================================================

cd /d "%~dp0"

echo.
echo [1/5] Building solution...
dotnet build NigerianNewGrid.slnx
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
