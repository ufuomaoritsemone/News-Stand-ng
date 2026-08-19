[CmdletBinding()]
param (
    [switch]$Background,
    [switch]$Docker,
    [switch]$Stop
)

$rootDir = $PSScriptRoot
if ([string]::IsNullOrEmpty($rootDir)) {
    $rootDir = Get-Location
}
Set-Location $rootDir

if ($Stop) {
    Write-Host "==================================================" -ForegroundColor Cyan
    Write-Host " Stopping Nigerian News Grid Services " -ForegroundColor Cyan
    Write-Host "==================================================" -ForegroundColor Cyan

    # 1. Stop PowerShell background jobs
    $jobNames = @("NewsApi", "AdminDashboard", "NewsScraperService", "TtsWorker")
    $jobs = Get-Job -ErrorAction SilentlyContinue | Where-Object { $jobNames -contains $_.Name }
    if ($jobs) {
        Write-Host "`nStopping PowerShell background jobs..." -ForegroundColor Yellow
        $jobs | Stop-Job -ErrorAction SilentlyContinue
        $jobs | Remove-Job -ErrorAction SilentlyContinue
        Write-Host "Background jobs stopped." -ForegroundColor Green
    }

    # 2. Stop dotnet processes running service assemblies/projects
    $projects = @("NewsApi", "AdminDashboard", "NewsScraperService", "TtsWorker")
    Write-Host "`nStopping running dotnet service processes..." -ForegroundColor Yellow
    $stoppedCount = 0
    try {
        $processes = Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" -ErrorAction SilentlyContinue
        foreach ($proc in $processes) {
            foreach ($proj in $projects) {
                if ($proc.CommandLine -and $proc.CommandLine -like "*$proj*") {
                    Write-Host "  Stopping process ID $($proc.ProcessId) ($proj)..." -ForegroundColor Gray
                    Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
                    $stoppedCount++
                    break
                }
            }
        }
    } catch {
        # Fallback if Get-CimInstance is unavailable
    }

    if ($stoppedCount -gt 0) {
        Write-Host "Stopped $stoppedCount dotnet process(es)." -ForegroundColor Green
    } else {
        Write-Host "No matching dotnet service processes found." -ForegroundColor Gray
    }

    # 3. Stop Docker containers if Docker flag set
    if ($Docker) {
        Write-Host "`n[Docker Mode] Stopping docker compose containers..." -ForegroundColor Yellow
        docker compose down
    }

    Write-Host "`n==================================================" -ForegroundColor Green
    Write-Host " All services stopped!" -ForegroundColor Green
    Write-Host "==================================================" -ForegroundColor Green
    exit 0
}

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " Starting Nigerian News Grid Admin Dashboard & Services " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

if ($Docker) {
    Write-Host "`n[Docker Mode] Running via docker-compose..." -ForegroundColor Yellow
    docker compose up --build
    exit $LASTEXITCODE
}

# 1. Build the solution
Write-Host "`n[1/5] Building solution..." -ForegroundColor Yellow
dotnet build NigerianNewGrid.slnx
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed! Please fix errors before starting services." -ForegroundColor Red
    exit 1
}
Write-Host "Build succeeded!" -ForegroundColor Green

if ($Background) {
    Write-Host "`n[Background Jobs Mode] Starting services in background..." -ForegroundColor Yellow

    Write-Host "Starting NewsApi..." -ForegroundColor Yellow
    $newsApiJob = Start-Job -Name "NewsApi" -ScriptBlock {
        param($dir) Set-Location $dir; dotnet run --project src/NewsApi/NewsApi.csproj
    } -ArgumentList $rootDir

    Start-Sleep -Seconds 3

    Write-Host "Starting AdminDashboard..." -ForegroundColor Yellow
    $adminJob = Start-Job -Name "AdminDashboard" -ScriptBlock {
        param($dir) Set-Location $dir; dotnet run --project src/AdminDashboard/AdminDashboard.csproj
    } -ArgumentList $rootDir

    Write-Host "Starting NewsScraperService..." -ForegroundColor Yellow
    $scraperJob = Start-Job -Name "NewsScraperService" -ScriptBlock {
        param($dir) Set-Location $dir; dotnet run --project src/NewsScraperService/NewsScraperService.csproj
    } -ArgumentList $rootDir

    Write-Host "Starting TtsWorker..." -ForegroundColor Yellow
    $ttsJob = Start-Job -Name "TtsWorker" -ScriptBlock {
        param($dir) Set-Location $dir; dotnet run --project src/TtsWorker/TtsWorker.csproj
    } -ArgumentList $rootDir

    Write-Host "`n==================================================" -ForegroundColor Green
    Write-Host " All services started in background jobs!" -ForegroundColor Green
    Write-Host "   Admin Dashboard : http://localhost:56192" -ForegroundColor Cyan
    Write-Host "   News API        : http://localhost:56193" -ForegroundColor Cyan
    Write-Host "==================================================" -ForegroundColor Green
    Write-Host "Use '.\run-services.ps1 -Stop' or 'Get-Job' to view/stop jobs." -ForegroundColor Gray
}
else {
    # Default: Open separate terminal windows for each service
    Write-Host "`n[2/5] Starting NewsApi (http://localhost:56193)..." -ForegroundColor Yellow
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Location '$rootDir'; Write-Host '--- NewsApi ---' -ForegroundColor Cyan; dotnet run --project src/NewsApi/NewsApi.csproj"

    Start-Sleep -Seconds 3

    Write-Host "`n[3/5] Starting AdminDashboard (http://localhost:56192)..." -ForegroundColor Yellow
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Location '$rootDir'; Write-Host '--- AdminDashboard ---' -ForegroundColor Cyan; dotnet run --project src/AdminDashboard/AdminDashboard.csproj"

    Write-Host "`n[4/5] Starting NewsScraperService..." -ForegroundColor Yellow
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Location '$rootDir'; Write-Host '--- NewsScraperService ---' -ForegroundColor Cyan; dotnet run --project src/NewsScraperService/NewsScraperService.csproj"

    Write-Host "`n[5/5] Starting TtsWorker..." -ForegroundColor Yellow
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Location '$rootDir'; Write-Host '--- TtsWorker ---' -ForegroundColor Cyan; dotnet run --project src/TtsWorker/TtsWorker.csproj"

    Write-Host "`n==================================================" -ForegroundColor Green
    Write-Host " All services launched in separate windows!" -ForegroundColor Green
    Write-Host "   Admin Dashboard : http://localhost:56192" -ForegroundColor Cyan
    Write-Host "   News API        : http://localhost:56193" -ForegroundColor Cyan
    Write-Host "==================================================" -ForegroundColor Green
}
