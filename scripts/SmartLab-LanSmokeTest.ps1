param(
    [Parameter(Mandatory = $false)]
    [string]$ServerUrl = "http://localhost:5047/",

    [Parameter(Mandatory = $false)]
    [string]$PcNumber,

    [Parameter(Mandatory = $false)]
    [string]$MacAddress
)

$ErrorActionPreference = "Stop"
$base = $ServerUrl.TrimEnd('/')

Write-Host "SmartLab LAN smoke test" -ForegroundColor Cyan
Write-Host "Server: $base"

Write-Host "1. TCP/API reachability..."
try {
    $tcp = Test-NetConnection -ComputerName ([Uri]$ServerUrl).Host -Port ([Uri]$ServerUrl).Port -WarningAction SilentlyContinue
    if (-not $tcp.TcpTestSucceeded) {
        throw "TCP connection failed."
    }
    Write-Host "   PASS" -ForegroundColor Green
}
catch {
    Write-Host "   FAIL: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "2. Machine presence endpoint..."
if ($PcNumber -and $MacAddress) {
    try {
        $body = @{
            pcNumber = $PcNumber
            macAddress = $MacAddress
        } | ConvertTo-Json

        $response = Invoke-WebRequest `
            -Uri "$base/api/PC/presence" `
            -Method Post `
            -ContentType "application/json" `
            -Body $body `
            -UseBasicParsing

        Write-Host "   HTTP $($response.StatusCode)" -ForegroundColor Green
        Write-Host "   $($response.Content)"
    }
    catch {
        $status = $_.Exception.Response.StatusCode.value__
        Write-Host "   HTTP $status (expected 200 for a correctly provisioned PC; 403/404/409 indicate identity/configuration failure)." -ForegroundColor Yellow
    }
}
else {
    Write-Host "   SKIPPED: supply -PcNumber and -MacAddress to test workstation identity." -ForegroundColor Yellow
}

Write-Host "3. Next staged tests" -ForegroundColor Cyan
@(
    "1 server + 1 client",
    "1 server + 2 clients",
    "5 clients",
    "10 clients",
    "1 full laboratory",
    "multiple laboratories",
    "DHCP/IP change",
    "server restart",
    "client restart",
    "LAN disconnect/reconnect",
    "Teacher authorization",
    "Need Assistance notifications",
    "PC commands",
    "screen monitoring",
    "teacher screen sharing"
) | ForEach-Object { Write-Host "   - $_" }

Write-Host "Smoke test preparation complete." -ForegroundColor Green
