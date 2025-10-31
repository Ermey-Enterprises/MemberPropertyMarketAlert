[CmdletBinding()]
param(
    [switch]$SkipTests,
    [switch]$StartFunctions,
    [switch]$StartWebhookClient,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Test-Prerequisite {
    param(
        [Parameter(Mandatory = $true)][string]$CommandName,
        [string]$InstallHint
    )

    if (-not (Get-Command $CommandName -ErrorAction SilentlyContinue)) {
        if ([string]::IsNullOrWhiteSpace($InstallHint)) {
            throw "Missing required command '$CommandName'."
        }

        throw "Missing required command '$CommandName'. $InstallHint"
    }
}

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)][string]$Title,
        [Parameter(Mandatory = $true)][ScriptBlock]$Action
    )

    Write-Host "==> $Title" -ForegroundColor Cyan
    & $Action
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "'$Title' failed with exit code $exitCode."
    }
    Write-Host "✔ $Title" -ForegroundColor Green
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$solutionPath = Join-Path $repoRoot "MemberPropertyMarketAlert.sln"
$functionsProjectPath = Join-Path $repoRoot "src/MemberPropertyAlert.Functions"
$webhookProjectPath = Join-Path $repoRoot "src/MemberPropertyAlert.MockWebhookClient"

Push-Location $repoRoot
try {
    Test-Prerequisite -CommandName "dotnet" -InstallHint "Install the .NET 8 SDK from https://dotnet.microsoft.com/download."

    if ($StartFunctions) {
        try {
            Test-Prerequisite -CommandName "func" -InstallHint "Install Azure Functions Core Tools v4 (https://learn.microsoft.com/azure/azure-functions/functions-run-local)."
        }
        catch {
            Write-Warning ($_.Exception.Message)
            $StartFunctions = $false
        }
    }

    Invoke-Step -Title "Restore solution" -Action { dotnet restore $solutionPath }
    Invoke-Step -Title "Build solution ($Configuration)" -Action { dotnet build $solutionPath --configuration $Configuration --no-restore }

    if (-not $SkipTests) {
        Invoke-Step -Title "Run tests" -Action { dotnet test $solutionPath --configuration $Configuration --no-build }
    }
    else {
        Write-Host "Skipping tests (per --SkipTests)." -ForegroundColor Yellow
    }

    if ($StartFunctions) {
        $funcCommand = "cd `"$functionsProjectPath`"; func start"
        Write-Host "Launching Azure Functions host in a new window..." -ForegroundColor Cyan
        Start-Process powershell.exe -ArgumentList "-NoExit", "-Command", $funcCommand | Out-Null
    }

    if ($StartWebhookClient) {
        $webhookCommand = "cd `"$webhookProjectPath`"; dotnet run"
        Write-Host "Launching mock webhook client in a new window..." -ForegroundColor Cyan
        Start-Process powershell.exe -ArgumentList "-NoExit", "-Command", $webhookCommand | Out-Null
    }

    Write-Host "Quickstart completed." -ForegroundColor Green
    if (-not $StartFunctions) {
        Write-Host "Tip: re-run with -StartFunctions to automatically open the Functions host." -ForegroundColor DarkGray
    }
    if (-not $StartWebhookClient) {
        Write-Host "Tip: re-run with -StartWebhookClient to stream webhook notifications." -ForegroundColor DarkGray
    }
}
finally {
    Pop-Location
}
