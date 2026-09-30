[CmdletBinding()]
param (
    [switch]$Background,
    [switch]$Docker,
    [switch]$Web,
    [switch]$Stop,
    [switch]$Tabs,
    [switch]$Separate,
    [switch]$BuildAll,
    [int]$NewsApiPort = 56193,
    [int]$AdminPort = 56192,
    [switch]$NoPortCheck
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
    $jobNames = @("NewsApi", "AdminDashboard", "NewsScraperService", "TtsWorker", "Web")
    $jobs = Get-Job -ErrorAction SilentlyContinue | Where-Object { $jobNames -contains $_.Name }
    if ($jobs) {
        Write-Host "`nStopping PowerShell background jobs..." -ForegroundColor Yellow
        $jobs | Stop-Job -ErrorAction SilentlyContinue
        $jobs | Remove-Job -ErrorAction SilentlyContinue
        Write-Host "Background jobs stopped." -ForegroundColor Green
    }

    # 2. Stop running dotnet service processes (apphosts and dotnet.exe instances)
    $projects = @("NewsApi", "AdminDashboard", "NewsScraperService", "TtsWorker")
    Write-Host "`nStopping running service processes..." -ForegroundColor Yellow
    $stoppedCount = 0

    # A. Stop by process name (e.g. NewsApi.exe)
    foreach ($proj in $projects) {
        $procs = Get-Process -Name $proj -ErrorAction SilentlyContinue
        foreach ($p in $procs) {
            Write-Host "  Stopping $proj process ID $($p.Id)..." -ForegroundColor Gray
            Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
            $stoppedCount++
        }
    }

    # B. Stop dotnet.exe running service assemblies
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
    } catch { }

    # C. Free ports if still occupied
    foreach ($p in @($NewsApiPort, $AdminPort)) {
        try {
            $tcp = Get-NetTCPConnection -LocalPort $p -ErrorAction SilentlyContinue
            if ($tcp -and $tcp[0].OwningProcess) {
                $ownerPid = $tcp[0].OwningProcess
                Write-Host "  Stopping lingering process on port $p (PID $ownerPid)..." -ForegroundColor Gray
                Stop-Process -Id $ownerPid -Force -ErrorAction SilentlyContinue
                $stoppedCount++
            }
        } catch { }
    }

    if ($stoppedCount -gt 0) {
        Write-Host "Stopped $stoppedCount service process(es)." -ForegroundColor Green
    } else {
        Write-Host "No matching running service processes found." -ForegroundColor Gray
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
    Write-Host "`n[Docker Mode] Checking Docker daemon status..." -ForegroundColor Yellow
    docker info 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: Docker Desktop is not running or the Docker daemon is unreachable." -ForegroundColor Red
        Write-Host "Please start Docker Desktop and ensure the engine is running." -ForegroundColor Yellow
        Write-Host "Tip: You can also run services locally without Docker:" -ForegroundColor Cyan
        Write-Host "     .\run-services.ps1              (opens separate terminal windows)" -ForegroundColor White
        Write-Host "     .\run-services.ps1 -Background  (runs as background jobs)" -ForegroundColor White
        exit 1
    }
    Write-Host "[Docker Mode] Running via docker-compose..." -ForegroundColor Yellow
    docker compose up --build
    exit $LASTEXITCODE
}

# ── Port Collision & Hyper-V Exclusion Detection ─────────────────────────────
function Get-WindowsExcludedPortRanges {
    $ranges = @()
    try {
        $output = netsh interface ipv4 show excludedportrange protocol=tcp 2>$null
        foreach ($line in $output) {
            if ($line -match '^\s*(\d+)\s+(\d+)') {
                $ranges += [PSCustomObject]@{
                    Start = [int]$matches[1]
                    End   = [int]$matches[2]
                }
            }
        }
    } catch { }
    return $ranges
}

function Test-IsPortExcluded($port, $excludedRanges) {
    if (-not $excludedRanges) { return $false }
    foreach ($range in $excludedRanges) {
        if ($port -ge $range.Start -and $port -le $range.End) {
            return $true
        }
    }
    return $false
}

function Test-IsPortBusy($port) {
    try {
        $tcpConn = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
        if ($tcpConn) {
            return $tcpConn[0].OwningProcess
        }
    } catch { }

    try {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $port)
        $listener.Start()
        $listener.Stop()
        return $null
    } catch {
        return -1
    }
}

function Resolve-ServicePort($serviceName, [int]$preferredPort, $excludedRanges) {
    if ($NoPortCheck) {
        return $preferredPort
    }

    $currentPort = $preferredPort
    $maxAttempts = 50

    while ($maxAttempts -gt 0) {
        $isExcluded = Test-IsPortExcluded $currentPort $excludedRanges
        $busyPid = Test-IsPortBusy $currentPort

        if (-not $isExcluded -and $null -eq $busyPid) {
            if ($currentPort -ne $preferredPort) {
                Write-Host "  [Port Conflict Resolved] ${serviceName}: preferred port $preferredPort was unavailable. Redirected to available port $currentPort." -ForegroundColor Yellow
            }
            return $currentPort
        }

        if ($currentPort -eq $preferredPort) {
            if ($isExcluded) {
                Write-Host "  [Port Warning] ${serviceName}: preferred port $preferredPort is reserved by a Windows TCP exclusion range (Hyper-V/WSL2)." -ForegroundColor Yellow
            } elseif ($busyPid) {
                $procInfo = ""
                if ($busyPid -gt 0) {
                    try {
                        $p = Get-Process -Id $busyPid -ErrorAction SilentlyContinue
                        if ($p) { $procInfo = " by '$($p.Name)' (PID $busyPid)" }
                    } catch { }
                }
                Write-Host "  [Port Warning] ${serviceName}: preferred port $preferredPort is currently occupied$procInfo." -ForegroundColor Yellow
            }
        }

        $currentPort++
        $maxAttempts--
    }

    Write-Host "  [Warning] Could not automatically resolve a free port for $serviceName near $preferredPort. Retaining $preferredPort." -ForegroundColor Red
    return $preferredPort
}

$excludedRanges = Get-WindowsExcludedPortRanges

Write-Host "`nScanning port availability..." -ForegroundColor Yellow
$resolvedNewsApiPort = Resolve-ServicePort "NewsApi" $NewsApiPort $excludedRanges
$resolvedAdminPort   = Resolve-ServicePort "AdminDashboard" $AdminPort $excludedRanges

$apiBaseUrl = "http://localhost:$resolvedNewsApiPort"
$adminUrl   = "http://localhost:$resolvedAdminPort"
$env:ApiBaseUrl = $apiBaseUrl

# Helper to encode PowerShell script blocks into base64 (avoids quote/semicolon collisions with wt.exe)
function ConvertTo-EncodedCommand([string]$scriptText) {
    $bytes = [System.Text.Encoding]::Unicode.GetBytes($scriptText)
    return [Convert]::ToBase64String($bytes)
}

# 1. Build the backend services
$serviceProjects = @(
    "src/NewsApi/NewsApi.csproj",
    "src/AdminDashboard/AdminDashboard.csproj",
    "src/NewsScraperService/NewsScraperService.csproj",
    "src/TtsWorker/TtsWorker.csproj"
)

$env:MSBUILDDISABLENODEREUSE = "1"
if ($BuildAll) {
    Write-Host "`n[1/5] Building entire solution (including mobile client)..." -ForegroundColor Yellow
    dotnet build NigerianNewGrid.slnx -nodeReuse:false
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build encountered an issue. Shutting down build servers and retrying..." -ForegroundColor Yellow
        dotnet build-server shutdown
        dotnet build NigerianNewGrid.slnx -nodeReuse:false
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Build failed! Please fix errors before starting services." -ForegroundColor Red
            exit 1
        }
    }
} else {
    Write-Host "`n[1/5] Building backend services (NewsApi, AdminDashboard, NewsScraperService, TtsWorker)..." -ForegroundColor Yellow
    $buildFailed = $false
    foreach ($proj in $serviceProjects) {
        $projName = [System.IO.Path]::GetFileNameWithoutExtension($proj)
        Write-Host "  Building $projName..." -ForegroundColor Gray
        dotnet build $proj --nologo -v:q -nodeReuse:false
        if ($LASTEXITCODE -ne 0) {
            $buildFailed = $true
            break
        }
    }
    if ($buildFailed) {
        Write-Host "Build encountered an issue (possibly locked files). Shutting down build servers and retrying..." -ForegroundColor Yellow
        dotnet build-server shutdown
        $buildFailed = $false
        foreach ($proj in $serviceProjects) {
            $projName = [System.IO.Path]::GetFileNameWithoutExtension($proj)
            Write-Host "  Retrying build for $projName..." -ForegroundColor Gray
            dotnet build $proj --nologo -v:q -nodeReuse:false
            if ($LASTEXITCODE -ne 0) {
                $buildFailed = $true
                break
            }
        }
        if ($buildFailed) {
            Write-Host "Build failed! Please fix errors before starting services." -ForegroundColor Red
            exit 1
        }
    }
}
Write-Host "Build succeeded!" -ForegroundColor Green

if ($Background) {
    Write-Host "`n[Background Jobs Mode] Starting services in background..." -ForegroundColor Yellow

    Write-Host "Starting NewsApi ($apiBaseUrl)..." -ForegroundColor Yellow
    $newsApiJob = Start-Job -Name "NewsApi" -ScriptBlock {
        param($dir, $apiUrl) 
        Set-Location $dir
        $env:ApiBaseUrl = $apiUrl
        $env:ApiKey = "AIzaSyAh7soVCbg-JfW-gUKVZIQkbd0sdlE34QI"
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        dotnet run --no-build --project src/NewsApi/NewsApi.csproj --urls "$apiUrl"
    } -ArgumentList $rootDir, $apiBaseUrl

    Start-Sleep -Seconds 3

    Write-Host "Starting AdminDashboard ($adminUrl)..." -ForegroundColor Yellow
    $adminJob = Start-Job -Name "AdminDashboard" -ScriptBlock {
        param($dir, $admUrl, $apiUrl) 
        Set-Location $dir
        $env:ApiBaseUrl = $apiUrl
        $env:ApiKey = "AIzaSyAh7soVCbg-JfW-gUKVZIQkbd0sdlE34QI"
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        dotnet run --no-build --project src/AdminDashboard/AdminDashboard.csproj --urls "$admUrl" --ApiBaseUrl "$apiUrl"
    } -ArgumentList $rootDir, $adminUrl, $apiBaseUrl

    Write-Host "Starting NewsScraperService..." -ForegroundColor Yellow
    $scraperJob = Start-Job -Name "NewsScraperService" -ScriptBlock {
        param($dir, $apiUrl) 
        Set-Location $dir
        $env:ApiBaseUrl = $apiUrl
        $env:ApiKey = "AIzaSyAh7soVCbg-JfW-gUKVZIQkbd0sdlE34QI"
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        dotnet run --no-build --project src/NewsScraperService/NewsScraperService.csproj --ApiBaseUrl "$apiUrl"
    } -ArgumentList $rootDir, $apiBaseUrl

    Write-Host "Starting TtsWorker..." -ForegroundColor Yellow
    $ttsJob = Start-Job -Name "TtsWorker" -ScriptBlock {
        param($dir, $apiUrl) 
        Set-Location $dir
        $env:ApiBaseUrl = $apiUrl
        $env:ApiKey = "AIzaSyAh7soVCbg-JfW-gUKVZIQkbd0sdlE34QI"
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        dotnet run --no-build --project src/TtsWorker/TtsWorker.csproj --ApiBaseUrl "$apiUrl"
    } -ArgumentList $rootDir, $apiBaseUrl

    if ($Web) {
        Write-Host "Starting Web Application (Vite on http://localhost:5173)..." -ForegroundColor Yellow
        $webJob = Start-Job -Name "Web" -ScriptBlock {
            param($dir) 
            Set-Location (Join-Path $dir "src/NigerianNewsGrid.Web")
            npm run dev
        } -ArgumentList $rootDir
    }

    Write-Host "`n==================================================" -ForegroundColor Green
    Write-Host " All services started in background jobs!" -ForegroundColor Green
    Write-Host "   Admin Dashboard : $adminUrl" -ForegroundColor Cyan
    Write-Host "   News API        : $apiBaseUrl" -ForegroundColor Cyan
    if ($Web) {
        Write-Host "   Web Application : http://localhost:5173" -ForegroundColor Cyan
    }
    Write-Host "==================================================" -ForegroundColor Green
    Write-Host "Use '.\run-services.ps1 -Stop' or 'Get-Job' to view/stop jobs." -ForegroundColor Gray
}
else {
    # Check if Windows Terminal (wt.exe) is available on the system
    $hasWindowsTerminal = [bool](Get-Command wt.exe -ErrorAction SilentlyContinue)

    $useTabs = $false
    if ($Tabs) {
        if (-not $hasWindowsTerminal) {
            Write-Host "`n[Notice] -Tabs requested, but Windows Terminal (wt.exe) was not found. Falling back to separate windows." -ForegroundColor Yellow
            $useTabs = $false
        } else {
            $useTabs = $true
        }
    } elseif (-not $Separate -and $hasWindowsTerminal) {
        $useTabs = $true
    }

    # Prepare command scripts for each service
    $scriptNewsApi = @"
`$Host.UI.RawUI.WindowTitle = 'NewsApi ($apiBaseUrl)'
[Console]::Title = 'NewsApi ($apiBaseUrl)'
Set-Location '$rootDir'
`$env:ApiBaseUrl = '$apiBaseUrl'
`$env:ApiKey = 'AIzaSyAh7soVCbg-JfW-gUKVZIQkbd0sdlE34QI'
`$env:ASPNETCORE_ENVIRONMENT = 'Development'
Write-Host '==================================================' -ForegroundColor Cyan
Write-Host '             NewsApi ($apiBaseUrl)' -ForegroundColor Green
Write-Host '==================================================' -ForegroundColor Cyan
dotnet run --no-build --project src/NewsApi/NewsApi.csproj --urls '$apiBaseUrl'
"@

    $scriptAdmin = @"
`$Host.UI.RawUI.WindowTitle = 'AdminDashboard ($adminUrl)'
[Console]::Title = 'AdminDashboard ($adminUrl)'
Start-Sleep -Seconds 2
Set-Location '$rootDir'
`$env:ApiBaseUrl = '$apiBaseUrl'
`$env:ApiKey = 'AIzaSyAh7soVCbg-JfW-gUKVZIQkbd0sdlE34QI'
`$env:ASPNETCORE_ENVIRONMENT = 'Development'
Write-Host '==================================================' -ForegroundColor Cyan
Write-Host '          AdminDashboard ($adminUrl)' -ForegroundColor Green
Write-Host '==================================================' -ForegroundColor Cyan
dotnet run --no-build --project src/AdminDashboard/AdminDashboard.csproj --urls '$adminUrl' --ApiBaseUrl '$apiBaseUrl'
"@

    $scriptScraper = @"
`$Host.UI.RawUI.WindowTitle = 'NewsScraperService'
[Console]::Title = 'NewsScraperService'
Start-Sleep -Seconds 3
Set-Location '$rootDir'
`$env:ApiBaseUrl = '$apiBaseUrl'
`$env:ApiKey = 'AIzaSyAh7soVCbg-JfW-gUKVZIQkbd0sdlE34QI'
`$env:ASPNETCORE_ENVIRONMENT = 'Development'
Write-Host '==================================================' -ForegroundColor Cyan
Write-Host '            NewsScraperService' -ForegroundColor Green
Write-Host '==================================================' -ForegroundColor Cyan
dotnet run --no-build --project src/NewsScraperService/NewsScraperService.csproj --ApiBaseUrl '$apiBaseUrl'
"@

    $scriptTts = @"
`$Host.UI.RawUI.WindowTitle = 'TtsWorker'
[Console]::Title = 'TtsWorker'
Start-Sleep -Seconds 4
Set-Location '$rootDir'
`$env:ApiBaseUrl = '$apiBaseUrl'
`$env:ApiKey = 'AIzaSyAh7soVCbg-JfW-gUKVZIQkbd0sdlE34QI'
`$env:ASPNETCORE_ENVIRONMENT = 'Development'
Write-Host '==================================================' -ForegroundColor Cyan
Write-Host '                TtsWorker' -ForegroundColor Green
Write-Host '==================================================' -ForegroundColor Cyan
dotnet run --no-build --project src/TtsWorker/TtsWorker.csproj --ApiBaseUrl '$apiBaseUrl'
"@

    $scriptWeb = @"
`$Host.UI.RawUI.WindowTitle = 'NigerianNewsGrid Web (http://localhost:5173)'
[Console]::Title = 'NigerianNewsGrid Web (http://localhost:5173)'
Set-Location '$rootDir\src\NigerianNewsGrid.Web'
Write-Host '==================================================' -ForegroundColor Cyan
Write-Host '    Nigerian News Grid Web (http://localhost:5173)' -ForegroundColor Green
Write-Host '==================================================' -ForegroundColor Cyan
npm run dev
"@

    $encNewsApi = ConvertTo-EncodedCommand $scriptNewsApi
    $encAdmin   = ConvertTo-EncodedCommand $scriptAdmin
    $encScraper = ConvertTo-EncodedCommand $scriptScraper
    $encTts     = ConvertTo-EncodedCommand $scriptTts
    $encWeb     = ConvertTo-EncodedCommand $scriptWeb

    $launchSucceeded = $false

    if ($useTabs) {
        Write-Host "`nLaunching all services in a single tabbed Windows Terminal window..." -ForegroundColor Cyan

        $tabCommands = @(
            "new-tab --title NewsApi-$resolvedNewsApiPort --tabColor #0ea5e9 -d `"$rootDir`" powershell.exe -NoExit -EncodedCommand $encNewsApi",
            "new-tab --title AdminDashboard-$resolvedAdminPort --tabColor #8b5cf6 -d `"$rootDir`" powershell.exe -NoExit -EncodedCommand $encAdmin",
            "new-tab --title NewsScraperService --tabColor #10b981 -d `"$rootDir`" powershell.exe -NoExit -EncodedCommand $encScraper",
            "new-tab --title TtsWorker --tabColor #f59e0b -d `"$rootDir`" powershell.exe -NoExit -EncodedCommand $encTts"
        )

        if ($Web) {
            $webDir = Join-Path $rootDir "src\NigerianNewsGrid.Web"
            $tabCommands += "new-tab --title Web-5173 --tabColor #ec4899 -d `"$webDir`" powershell.exe -NoExit -EncodedCommand $encWeb"
        }

        $wtArgString = $tabCommands -join " ; "

        try {
            Start-Process wt.exe -ArgumentList $wtArgString
            $launchSucceeded = $true

            Write-Host "`n==================================================" -ForegroundColor Green
            Write-Host " All services launched in 1 tabbed Windows Terminal window!" -ForegroundColor Green
            Write-Host "   Admin Dashboard : $adminUrl" -ForegroundColor Cyan
            Write-Host "   News API        : $apiBaseUrl" -ForegroundColor Cyan
            if ($Web) {
                Write-Host "   Web Application : http://localhost:5173" -ForegroundColor Cyan
            }
            Write-Host "==================================================" -ForegroundColor Green
            Write-Host "Tip: Use '.\run-services.ps1 -Separate' to open individual windows instead." -ForegroundColor Gray
        } catch {
            Write-Host "`n[Notice] Windows Terminal launch failed ($($_.Exception.Message)). Falling back to separate windows..." -ForegroundColor Yellow
            $launchSucceeded = $false
        }
    }

    if (-not $launchSucceeded) {
        # Fallback or explicit -Separate: Open separate terminal windows for each service
        Write-Host "`n[2/5] Starting NewsApi ($apiBaseUrl)..." -ForegroundColor Yellow
        Start-Process powershell -ArgumentList "-NoExit", "-EncodedCommand", $encNewsApi

        Start-Sleep -Seconds 2

        Write-Host "`n[3/5] Starting AdminDashboard ($adminUrl)..." -ForegroundColor Yellow
        Start-Process powershell -ArgumentList "-NoExit", "-EncodedCommand", $encAdmin

        Write-Host "`n[4/5] Starting NewsScraperService..." -ForegroundColor Yellow
        Start-Process powershell -ArgumentList "-NoExit", "-EncodedCommand", $encScraper

        Write-Host "`n[5/5] Starting TtsWorker..." -ForegroundColor Yellow
        Start-Process powershell -ArgumentList "-NoExit", "-EncodedCommand", $encTts

        if ($Web) {
            Write-Host "`n[6/6] Starting Web Application (http://localhost:5173)..." -ForegroundColor Yellow
            Start-Process powershell -ArgumentList "-NoExit", "-EncodedCommand", $encWeb
        }

        Write-Host "`n==================================================" -ForegroundColor Green
        Write-Host " All services launched in separate windows!" -ForegroundColor Green
        Write-Host "   Admin Dashboard : $adminUrl" -ForegroundColor Cyan
        Write-Host "   News API        : $apiBaseUrl" -ForegroundColor Cyan
        if ($Web) {
            Write-Host "   Web Application : http://localhost:5173" -ForegroundColor Cyan
        }
        Write-Host "==================================================" -ForegroundColor Green
    }
}

