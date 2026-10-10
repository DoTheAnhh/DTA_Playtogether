<#
Đóng gói DTA Playtogether (bản C#) vào dist\ (chạy qua build.bat cho tiện):
  dist\Client\   GỬI NGƯỜI DÙNG: 1 file DTA_Playtogether.exe tự chứa (.NET + mọi thư viện nén bên trong), bấm đúp là chạy.
  dist\Server\   ĐEM LÊN VPS: 1 file DTA_ServerPanel.exe (máy chủ + cửa sổ quản lý key; thêm --headless để chạy không giao diện).

An toàn: tool / máy chủ trong dist đang chạy thì dừng và báo (không tự tắt). Build hết vào thư mục tạm rồi mới thay file - build lỗi thì
dist giữ nguyên bản cũ. Chỉ thay file chương trình: dữ liệu (data\, runtime\, server.db, key.json, chứng chỉ...) giữ nguyên. Thư mục
tạm luôn được xoá kể cả khi lỗi.

Tham số:
  -Server host:port   máy chủ key mà bản tool sẽ gọi (mặc định: địa chỉ ghi trong ServerClient).
  -Secrets thư_mục    thư mục chứa cert.pem + key.pem + secret.key của máy chủ: chép vào dist\Server (không bao giờ nằm trong mã nguồn).
  -Linux              build thêm DTA_Server cho VPS Linux (dist\Server-linux).
#>
param([string]$Server = "", [string]$Secrets = "", [switch]$Linux)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dist = Join-Path $root "dist"
$client = Join-Path $dist "Client"
$serverDir = Join-Path $dist "Server"
$common = @("-c", "Release", "--self-contained", "true", "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true",
            "-p:EnableCompressionInSingleFile=true", "-p:DebugType=none", "-nologo", "-v", "q")

$running = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($dist, [StringComparison]::OrdinalIgnoreCase) }
if ($running) {
    $names = ($running | ForEach-Object { $_.Path.Substring($dist.Length + 1) }) -join ", "
    Write-Host "Đang mở: $names - hãy đóng rồi build lại (file đang chạy không thay được)." -ForegroundColor Red
    exit 1
}

$temps = @()
# Build 1 project vào thư mục tạm; trả đường dẫn thư mục tạm.
function Publish([string]$project, [string]$runtime, [string[]]$extra = @()) {
    $temp = Join-Path $dist ("_tmp_" + [IO.Path]::GetFileNameWithoutExtension($project) + "_" + $runtime)
    if (Test-Path $temp) { Remove-Item -Recurse -Force $temp }
    $script:temps += $temp
    Write-Host "Đang build $project ($runtime)..."
    & dotnet publish (Join-Path $root $project) -r $runtime -o $temp @common @extra | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Build lỗi: $project" }
    return $temp
}

# Thay file chương trình trong <out> bằng file trong <temp>, bỏ các file thừa <stale> (thư mục con / dữ liệu giữ nguyên).
function Install([string]$temp, [string]$out, [string[]]$stale = @()) {
    New-Item -ItemType Directory -Force $out | Out-Null
    Get-ChildItem $temp -File | Where-Object { $stale -notcontains $_.Name } | Copy-Item -Destination $out -Force
    foreach ($old in $stale) { Remove-Item (Join-Path $out $old) -ErrorAction SilentlyContinue }
}

try {
    $serverArgs = @()
    if ($Server) { $serverArgs = @("-p:DtaServer=$Server") }
    $clientTemp = Publish "src\DTA.App\DTA.App.csproj" "win-x64" $serverArgs
    $serverTemp = Publish "src\DTA.ServerPanel\DTA.ServerPanel.csproj" "win-x64"
    $linuxTemp = if ($Linux) { Publish "src\DTA.Server\DTA.Server.csproj" "linux-x64" } else { $null }

    Install $clientTemp $client
    Write-Host "Client: $client\DTA_Playtogether.exe" -ForegroundColor Green
    Install $serverTemp $serverDir @("DTA_Server.exe", "DTA_Server.runtimeconfig.json")
    if ($Secrets) {
        foreach ($name in "cert.pem", "key.pem", "secret.key") { Copy-Item (Join-Path $Secrets $name) $serverDir -Force }
    }
    elseif (-not (Test-Path (Join-Path $serverDir "secret.key"))) {
        Write-Warning "dist\Server chưa có cert.pem / key.pem / secret.key - chạy lại với -Secrets <thư mục> hoặc chép tay trước khi mở máy chủ."
    }
    Write-Host "Server: $serverDir\DTA_ServerPanel.exe" -ForegroundColor Green
    if ($linuxTemp) {
        Install $linuxTemp (Join-Path $dist "Server-linux")
        Write-Host "Server Linux: $(Join-Path $dist 'Server-linux')\DTA_Server" -ForegroundColor Green
    }
}
catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
finally {
    foreach ($temp in $temps) { if (Test-Path $temp) { Remove-Item -Recurse -Force $temp -ErrorAction SilentlyContinue } }
}
