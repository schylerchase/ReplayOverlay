# Replay Overlay Build Script
# Usage: .\build.ps1 [-Configuration Release|Debug] [-SkipTests] [-SkipOverlay]

param(
    [string]$Configuration = "Debug",
    [switch]$SkipTests,
    [switch]$SkipOverlay
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

Write-Host "=== Building Replay Overlay ($Configuration) ===" -ForegroundColor Cyan

# 1. Build C# Host
Write-Host "`n--- C# Host ---" -ForegroundColor Yellow
dotnet build "$root\src\OBSReplay.Host\OBSReplay.Host.csproj" -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "C# build failed" }

$hostExe = "$root\src\OBSReplay.Host\bin\$Configuration\net8.0-windows\ReplayOverlay.exe"
if (-not (Test-Path $hostExe)) {
    throw "Host exe not found at $hostExe after build. The C# build may have succeeded but produced no output."
}

# 2. Build C++ Overlay (requires CMake + MSVC)
if (-not $SkipOverlay) {
    $overlayDir = "$root\src\OBSReplay.Overlay"
    $buildDir = "$root\build\overlay"

    if (Get-Command cmake -ErrorAction SilentlyContinue) {
        Write-Host "`n--- C++ Overlay ---" -ForegroundColor Yellow

        if (-not (Test-Path $buildDir)) {
            New-Item -ItemType Directory -Force -Path $buildDir | Out-Null

            # Auto-detect VS generator
            $vsInstances = & "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe" `
                -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
                -property installationVersion 2>$null

            if ($vsInstances) {
                $major = ($vsInstances -split '\.')[0]
                $generatorMap = @{
                    "18" = "Visual Studio 18 2026"
                    "17" = "Visual Studio 17 2022"
                    "16" = "Visual Studio 16 2019"
                }
                $generator = $generatorMap[$major]
                if (-not $generator) { $generator = "Visual Studio 17 2022" }
            } else {
                $generator = "Visual Studio 17 2022"
            }

            Write-Host "Using CMake generator: $generator" -ForegroundColor Gray
            cmake -B $buildDir -S $overlayDir -G $generator -A x64
            if ($LASTEXITCODE -ne 0) { throw "CMake configure failed" }
        }

        cmake --build $buildDir --config $Configuration
        if ($LASTEXITCODE -ne 0) { throw "C++ build failed" }
    } else {
        throw "CMake not found. CMake is required to build the C++ overlay. Install CMake and ensure it is on PATH."
    }
}

# 3. Run Tests
if (-not $SkipTests) {
    Write-Host "`n--- C# Tests ---" -ForegroundColor Yellow
    dotnet test "$root\tests\OBSReplay.Host.Tests\OBSReplay.Host.Tests.csproj" -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "C# tests failed" }

    $buildDir = "$root\build\overlay"
    if ((Get-Command ctest -ErrorAction SilentlyContinue) -and (Test-Path "$buildDir")) {
        Write-Host "`n--- C++ Tests ---" -ForegroundColor Yellow
        ctest --test-dir $buildDir --build-config $Configuration --output-on-failure
    }
}

# 4. Copy outputs to dist/
$dist = "$root\dist\$Configuration"
New-Item -ItemType Directory -Force -Path $dist | Out-Null

# Publish C# host (self-contained copy with all DLLs)
$hostOut = "$root\src\OBSReplay.Host\bin\$Configuration\net8.0-windows"
if (Test-Path "$hostOut\ReplayOverlay.exe") {
    Copy-Item "$hostOut\*" "$dist\" -Recurse -Force
    Write-Host "Copied C# host to $dist" -ForegroundColor Green
}

# Copy C++ overlay (CMake outputs to bin/$Configuration/)
$overlayExe = "$root\build\overlay\bin\$Configuration\OverlayRenderer.exe"
if (Test-Path $overlayExe) {
    Copy-Item $overlayExe "$dist\" -Force
    Write-Host "Copied C++ overlay to $dist" -ForegroundColor Green

    # Also copy to C# host output directory for dev/debug runs
    if (Test-Path $hostOut) {
        Copy-Item $overlayExe "$hostOut\" -Force
        Write-Host "Copied C++ overlay to $hostOut (for dev)" -ForegroundColor Green
    }
} elseif (-not $SkipOverlay) {
    throw "Overlay exe not found at $overlayExe. The C++ overlay build may have failed."
} else {
    Write-Host "Overlay exe not found at $overlayExe (skipped)" -ForegroundColor DarkYellow
}

Write-Host "`n=== Build Complete ===" -ForegroundColor Cyan
