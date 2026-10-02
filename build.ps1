<#
.SYNOPSIS
    Builds and tests every template, the same way CI does. Run it before pushing.

.DESCRIPTION
    1. dotnet build + test for each .NET template (warnings are errors, so a PASS means zero warnings)
    2. dotnet pack of the template package
    3. Docker: each template's image (the Dockerfiles run the tests too), the Liquibase rollback round trip,
       and Dockerfile.verify (install the pack, generate every template under a new name, build + test)

    Everything is logged to artifacts/build.log. The summary at the end lists PASS/FAIL per step.

.PARAMETER SkipDocker
    Only run the .NET steps (no Docker needed).

.EXAMPLE
    ./build.ps1
    ./build.ps1 -SkipDocker
#>
param([switch]$SkipDocker)

$ErrorActionPreference = 'Continue'
$root = $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$log = Join-Path $artifacts 'build.log'
$writer = New-Object System.IO.StreamWriter($log, $false, (New-Object System.Text.UTF8Encoding($false)))
$writer.AutoFlush = $true
$writer.WriteLine("Build started $(Get-Date -Format o)")

$results = New-Object System.Collections.Generic.List[object]

function Invoke-Step([string]$Name, [string]$WorkingDirectory, [string[][]]$Commands) {
    Write-Host ""
    Write-Host "=== $Name ===" -ForegroundColor Cyan
    $writer.WriteLine("")
    $writer.WriteLine("=== $Name ===")
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $ok = $true

    Push-Location $WorkingDirectory
    try {
        foreach ($command in $Commands) {
            $exe, $arguments = $command
            $writer.WriteLine("> $($command -join ' ')")
            & $exe @arguments 2>&1 | ForEach-Object { $line = "$_"; Write-Host $line; $writer.WriteLine($line) }
            if ($LASTEXITCODE -ne 0) { $ok = $false; break }
        }
    }
    catch {
        $writer.WriteLine($_.Exception.Message)
        Write-Host $_.Exception.Message -ForegroundColor Red
        $ok = $false
    }
    finally {
        Pop-Location
    }

    $results.Add([pscustomobject]@{
        Step    = $Name
        Result  = if ($ok) { 'PASS' } else { 'FAIL' }
        Seconds = [int]$watch.Elapsed.TotalSeconds
    })
}

$dotnetTemplates = @(
    @{ Name = 'api-idesign';    Solution = 'Company.Product.slnx' },
    @{ Name = 'worker-dapr';    Solution = 'Company.Product.Worker.slnx' },
    @{ Name = 'worker-rebus';   Solution = 'Company.Product.Worker.slnx' },
    @{ Name = 'scheduler-dapr'; Solution = 'Company.Product.Scheduler.slnx' }
)

foreach ($template in $dotnetTemplates) {
    $dir = Join-Path $root "templates/$($template.Name)"
    Invoke-Step "dotnet build + test: $($template.Name)" $dir @(
        @('dotnet', 'build', $template.Solution, '-c', 'Release'),
        @('dotnet', 'test', $template.Solution, '-c', 'Release', '--no-build')
    )
}

Invoke-Step 'dotnet pack: template package' $root @(
    ,@('dotnet', 'pack', 'CodingTemplates.csproj', '-c', 'Release', '-o', 'artifacts/packages')
)

if (-not $SkipDocker) {
    foreach ($name in 'api-idesign', 'worker-dapr', 'worker-rebus', 'scheduler-dapr', 'db-liquibase') {
        Invoke-Step "docker build: $name" (Join-Path $root "templates/$name") @(
            ,@('docker', 'build', '-t', "coding-templates/${name}:local", '.')
        )
    }

    Invoke-Step 'liquibase: rollback round trip' (Join-Path $root 'templates/db-liquibase') @(
        @('docker', 'compose', '--profile', 'verify', 'run', '--rm', '--build', 'verify'),
        @('docker', 'compose', '--profile', 'verify', 'down', '-v')
    )

    Invoke-Step 'Dockerfile.verify: generated templates' $root @(
        ,@('docker', 'build', '-f', 'Dockerfile.verify', '.')
    )
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
$summary = $results | Format-Table -AutoSize | Out-String
Write-Host $summary
$writer.WriteLine("=== Summary ===")
$writer.WriteLine($summary)
$writer.Close()
Write-Host "Full log: $log"

if ($results.Result -contains 'FAIL') { exit 1 }
exit 0
