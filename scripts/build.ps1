param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [switch] $SkipTests,
    [switch] $SkipPublish
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $PSCommandPath
Set-Location $scriptRoot\..
$repoRoot = (Get-Location).Path

function Fail-Step($message)
{
    Write-Error $message
    exit 1
}

function Assert-Command($name)
{
    if (-not (Get-Command $name -ErrorAction SilentlyContinue))
    {
        Fail-Step "Required command missing: $name"
    }
}

function Invoke-DotNet
{
    & dotnet @args
    if ($LASTEXITCODE -ne 0)
    {
        Fail-Step "dotnet $($args -join ' ') failed with exit code $LASTEXITCODE"
    }
}

Assert-Command dotnet

$solution = Get-ChildItem -Path . -Filter *.sln | Select-Object -First 1
if (-not $solution)
{
    Fail-Step "No solution file found in repository root. Run this script after scaffolding the .NET solution."
}

Write-Host "[build] restore"
Invoke-DotNet restore $solution.FullName

Write-Host "[build] compile"
Invoke-DotNet build $solution.FullName -c $Configuration -v minimal

if (-not $SkipTests)
{
    Write-Host "[build] tests"
    Invoke-DotNet test $solution.FullName -c $Configuration -v minimal --no-build
}

$appProject = Get-ChildItem -Recurse -Filter GameSubRelay.App.csproj | Select-Object -First 1
if (-not $appProject)
{
    Write-Warning "No GameSubRelay.App.csproj found; skipping publish. This is expected before App layer scaffolding is in place."
    exit 0
}

if (-not $SkipPublish)
{
    $outDir = Join-Path $repoRoot "dist\win-x64"
    if (Test-Path $outDir)
    {
        Remove-Item -Recurse -Force $outDir
    }

    Write-Host "[build] publish"
    Invoke-DotNet publish $appProject.FullName -c $Configuration -r $Runtime --self-contained false -o $outDir
    Write-Host "[build] published to $outDir"
}

Write-Host "[build] done"
