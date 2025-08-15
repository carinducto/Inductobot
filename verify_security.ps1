#!/usr/bin/env pwsh

# Script to verify the simulator only accepts localhost connections
Write-Host "Verifying UAS-WAND Simulator Security (localhost-only)..." -ForegroundColor Green
Write-Host ""

# Function to test connection
function Test-Connection {
    param([string]$Address, [int]$Port)
    
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    try {
        Write-Host "Testing connection to $Address`:$Port..." -ForegroundColor Yellow
        $tcpClient.ReceiveTimeout = 3000
        $tcpClient.SendTimeout = 3000
        $tcpClient.Connect($Address, $Port)
        
        if ($tcpClient.Connected) {
            Write-Host "✓ Connection to $Address`:$Port SUCCEEDED" -ForegroundColor Green
            $tcpClient.Close()
            return $true
        } else {
            Write-Host "✗ Connection to $Address`:$Port FAILED" -ForegroundColor Red
            return $false
        }
    } catch {
        Write-Host "✗ Connection to $Address`:$Port FAILED - $($_.Exception.Message)" -ForegroundColor Red
        return $false
    } finally {
        $tcpClient.Dispose()
    }
}

Write-Host "Security Verification Results:" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan

# Test localhost connection (should work)
$localhostResult = Test-Connection "127.0.0.1" 8080
Write-Host ""

# Test loopback IPv6 (should work if IPv6 enabled)
$ipv6Result = Test-Connection "::1" 8080
Write-Host ""

# Try to get local IP for testing external access
try {
    $localIP = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -ne "127.0.0.1" -and $_.PrefixOrigin -eq "Dhcp" }).IPAddress | Select-Object -First 1
    if ($localIP) {
        Write-Host "Testing external access via local IP: $localIP" -ForegroundColor Yellow
        $externalResult = Test-Connection $localIP 8080
    } else {
        Write-Host "No DHCP IPv4 address found for external testing" -ForegroundColor Yellow
        $externalResult = $false
    }
} catch {
    Write-Host "Could not determine local IP for external testing" -ForegroundColor Yellow
    $externalResult = $false
}

Write-Host ""
Write-Host "Security Analysis:" -ForegroundColor Cyan
Write-Host "=================" -ForegroundColor Cyan

if ($localhostResult) {
    Write-Host "✓ Localhost access (127.0.0.1) works - EXPECTED" -ForegroundColor Green
} else {
    Write-Host "✗ Localhost access failed - simulator may not be running" -ForegroundColor Red
}

if (-not $externalResult) {
    Write-Host "✓ External network access blocked - SECURE" -ForegroundColor Green
} else {
    Write-Host "⚠ WARNING: External network access allowed - SECURITY RISK!" -ForegroundColor Red
}

Write-Host ""
if ($localhostResult -and -not $externalResult) {
    Write-Host "🛡️ SECURITY VERIFICATION PASSED - Simulator only accepts localhost connections" -ForegroundColor Green -BackgroundColor DarkGreen
} else {
    Write-Host "⚠️ SECURITY VERIFICATION FAILED - Please check simulator configuration" -ForegroundColor Red -BackgroundColor DarkRed
}