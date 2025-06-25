#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Local testing script for MemberPropertyMarketAlert solution
.DESCRIPTION
    This script provides comprehensive local testing capabilities including:
    - Unit tests
    - Integration tests
    - Code coverage
    - Build verification
.PARAMETER TestType
    Type of tests to run: All, Unit, Integration, Coverage
.PARAMETER Configuration
    Build configuration: Debug or Release (default: Debug)
.PARAMETER Verbose
    Enable verbose output
.EXAMPLE
    .\Test-Local.ps1 -TestType All
.EXAMPLE
    .\Test-Local.ps1 -TestType Unit -Configuration Release
.EXAMPLE
    .\Test-Local.ps1 -TestType Coverage -Verbose
#>

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("All", "Unit", "Integration", "Coverage", "Build")]
    [string]$TestType = "All",
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    
    [Parameter(Mandatory = $false)]
    [switch]$VerboseOutput
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Get script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionDir = Split-Path -Parent $ScriptDir

Write-Host "🧪 MemberPropertyMarketAlert Local Testing Script" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "Solution Directory: $SolutionDir" -ForegroundColor Gray
Write-Host "Test Type: $TestType" -ForegroundColor Gray
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host ""

# Change to solution directory
Set-Location $SolutionDir

function Write-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host "🔹 $Title" -ForegroundColor Yellow
    Write-Host ("=" * ($Title.Length + 3)) -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor Green
}

function Write-Error {
    param([string]$Message)
    Write-Host "❌ $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "ℹ️  $Message" -ForegroundColor Blue
}

function Test-Prerequisites {
    Write-Section "Checking Prerequisites"
    
    # Check if .NET SDK is installed
    try {
        $dotnetVersion = dotnet --version
        Write-Success ".NET SDK version: $dotnetVersion"
    }
    catch {
        Write-Error ".NET SDK not found. Please install .NET 8.0 SDK."
        exit 1
    }
    
    # Check if solution file exists
    if (-not (Test-Path "MemberPropertyMarketAlert.sln")) {
        Write-Error "Solution file not found in current directory."
        exit 1
    }
    
    Write-Success "Prerequisites check completed"
}

function Invoke-Build {
    Write-Section "Building Solution"
    
    try {
        Write-Info "Restoring packages..."
        dotnet restore --verbosity minimal | Out-Null
        
        Write-Info "Building solution in $Configuration configuration..."
        dotnet build --configuration $Configuration --no-restore --verbosity minimal | Out-Null
        
        Write-Success "Build completed successfully"
        return $true
    }
    catch {
        Write-Error "Build failed: $($_.Exception.Message)"
        return $false
    }
}

function Invoke-UnitTests {
    Write-Section "Running Unit Tests"
    
    $testProjects = @(
        "tests/MemberPropertyAlert.Core.Tests/MemberPropertyAlert.Core.Tests.csproj"
    )
    
    $allPassed = $true
    
    foreach ($project in $testProjects) {
        if (Test-Path $project) {
            Write-Info "Running tests for: $(Split-Path -Leaf $project)"
            
            try {
                $verbosityFlag = if ($VerboseOutput) { "normal" } else { "minimal" }
                $testResult = dotnet test $project --configuration $Configuration --no-build --verbosity $verbosityFlag --logger "console;verbosity=normal" 2>&1
                if ($LASTEXITCODE -eq 0) {
                    Write-Success "Unit tests passed for $(Split-Path -Leaf $project)"
                } else {
                    Write-Error "Unit tests failed for $(Split-Path -Leaf $project)"
                    $allPassed = $false
                }
            }
            catch {
                Write-Error "Unit tests failed for $(Split-Path -Leaf $project): $($_.Exception.Message)"
                $allPassed = $false
            }
        }
        else {
            Write-Info "Test project not found: $project (skipping)"
        }
    }
    
    return $allPassed
}

function Invoke-IntegrationTests {
    Write-Section "Running Integration Tests"
    
    $testProjects = @(
        "tests/MemberPropertyAlert.Functions.Tests/MemberPropertyAlert.Functions.Tests.csproj",
        "tests/MemberPropertyAlert.Integration.Tests/MemberPropertyAlert.Integration.Tests.csproj"
    )
    
    $allPassed = $true
    
    foreach ($project in $testProjects) {
        if (Test-Path $project) {
            Write-Info "Running integration tests for: $(Split-Path -Leaf $project)"
            
            try {
                $verbosityFlag = if ($VerboseOutput) { "normal" } else { "minimal" }
                $testResult = dotnet test $project --configuration $Configuration --no-build --verbosity $verbosityFlag --logger "console;verbosity=normal" 2>&1
                if ($LASTEXITCODE -eq 0) {
                    Write-Success "Integration tests passed for $(Split-Path -Leaf $project)"
                } else {
                    Write-Error "Integration tests failed for $(Split-Path -Leaf $project)"
                    $allPassed = $false
                }
            }
            catch {
                Write-Error "Integration tests failed for $(Split-Path -Leaf $project): $($_.Exception.Message)"
                $allPassed = $false
            }
        }
        else {
            Write-Info "Test project not found: $project (skipping)"
        }
    }
    
    return $allPassed
}

function Invoke-CodeCoverage {
    Write-Section "Running Code Coverage Analysis"
    
    try {
        Write-Info "Running tests with code coverage..."
        
        # Create coverage directory
        $coverageDir = "TestResults/Coverage"
        if (-not (Test-Path $coverageDir)) {
            New-Item -ItemType Directory -Path $coverageDir -Force | Out-Null
        }
        
        # Run tests with coverage
        dotnet test --configuration $Configuration --no-build --collect:"XPlat Code Coverage" --results-directory $coverageDir --verbosity minimal
        
        # Find coverage files
        $coverageFiles = Get-ChildItem -Path $coverageDir -Recurse -Filter "coverage.cobertura.xml"
        
        if ($coverageFiles.Count -gt 0) {
            Write-Success "Code coverage analysis completed"
            Write-Info "Coverage files generated in: $coverageDir"
            
            foreach ($file in $coverageFiles) {
                Write-Info "Coverage file: $($file.FullName)"
            }
        }
        else {
            Write-Info "No coverage files found"
        }
        
        return $true
    }
    catch {
        Write-Error "Code coverage analysis failed: $($_.Exception.Message)"
        return $false
    }
}

function Show-Summary {
    param(
        [bool]$BuildSuccess,
        [bool]$UnitTestSuccess,
        [bool]$IntegrationTestSuccess,
        [bool]$CoverageSuccess
    )
    
    Write-Section "Test Summary"
    
    if ($BuildSuccess) { Write-Success "Build: PASSED" } else { Write-Error "Build: FAILED" }
    
    if ($TestType -eq "All" -or $TestType -eq "Unit") {
        if ($UnitTestSuccess) { Write-Success "Unit Tests: PASSED" } else { Write-Error "Unit Tests: FAILED" }
    }
    
    if ($TestType -eq "All" -or $TestType -eq "Integration") {
        if ($IntegrationTestSuccess) { Write-Success "Integration Tests: PASSED" } else { Write-Error "Integration Tests: FAILED" }
    }
    
    if ($TestType -eq "All" -or $TestType -eq "Coverage") {
        if ($CoverageSuccess) { Write-Success "Code Coverage: COMPLETED" } else { Write-Error "Code Coverage: FAILED" }
    }
    
    $overallSuccess = $BuildSuccess
    if ($TestType -eq "All" -or $TestType -eq "Unit") { $overallSuccess = $overallSuccess -and $UnitTestSuccess }
    if ($TestType -eq "All" -or $TestType -eq "Integration") { $overallSuccess = $overallSuccess -and $IntegrationTestSuccess }
    if ($TestType -eq "All" -or $TestType -eq "Coverage") { $overallSuccess = $overallSuccess -and $CoverageSuccess }
    
    Write-Host ""
    if ($overallSuccess) {
        Write-Success "🎉 All tests completed successfully!"
        exit 0
    }
    else {
        Write-Error "❌ Some tests failed. Please check the output above."
        exit 1
    }
}

# Main execution
try {
    Test-Prerequisites
    
    [bool]$buildSuccess = $true
    [bool]$unitTestSuccess = $true
    [bool]$integrationTestSuccess = $true
    [bool]$coverageSuccess = $true
    
    # Always build first
    $buildSuccess = Invoke-Build
    
    if (-not $buildSuccess) {
        Write-Error "Build failed. Stopping execution."
        exit 1
    }
    
    # Run tests based on TestType parameter
    switch ($TestType) {
        "Build" {
            # Only build, no tests
        }
        "Unit" {
            $unitTestSuccess = Invoke-UnitTests
        }
        "Integration" {
            $integrationTestSuccess = Invoke-IntegrationTests
        }
        "Coverage" {
            $coverageSuccess = Invoke-CodeCoverage
        }
        "All" {
            $unitTestSuccess = Invoke-UnitTests
            $integrationTestSuccess = Invoke-IntegrationTests
            $coverageSuccess = Invoke-CodeCoverage
        }
    }
    
    Show-Summary -BuildSuccess $buildSuccess -UnitTestSuccess $unitTestSuccess -IntegrationTestSuccess $integrationTestSuccess -CoverageSuccess $coverageSuccess
}
catch {
    Write-Error "Script execution failed: $($_.Exception.Message)"
    exit 1
}
