# Simple Bicep validation script
param(
    [string]$Environment = 'dev'
)

Write-Host "Testing Bicep template for environment: $Environment" -ForegroundColor Cyan

# Check Azure CLI
if (!(Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Host "Azure CLI not found!" -ForegroundColor Red
    exit 1
}

# Check login
$account = az account show --output json 2>$null
if (!$account) {
    Write-Host "Not logged in to Azure CLI. Please run 'az login' first." -ForegroundColor Red
    exit 1
}

Write-Host "Azure CLI is available and logged in" -ForegroundColor Green

# Check files
$templateFile = ".\infra\main.bicep"
$parametersFile = ".\infra\main.$Environment.parameters.json"

if (!(Test-Path $templateFile)) {
    Write-Host "Template file not found: $templateFile" -ForegroundColor Red
    exit 1
}

if (!(Test-Path $parametersFile)) {
    Write-Host "Parameters file not found: $parametersFile" -ForegroundColor Red
    exit 1
}

Write-Host "Template and parameter files found" -ForegroundColor Green

# Validate template syntax
Write-Host "Validating Bicep template syntax..." -ForegroundColor Yellow
az bicep build --file $templateFile --stdout | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Bicep template syntax is valid" -ForegroundColor Green
} else {
    Write-Host "✗ Bicep template has syntax errors" -ForegroundColor Red
    exit 1
}

Write-Host "" 
Write-Host "✓ Basic validation completed successfully!" -ForegroundColor Green
Write-Host "Template files are ready for deployment." -ForegroundColor White
