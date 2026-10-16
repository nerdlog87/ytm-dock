<#
.SYNOPSIS
  YTM Dock 를 단일 exe 로 publish 한다.

.EXAMPLE
  .\publish.ps1                 # 프레임워크 의존 (작음, .NET 10 Desktop Runtime 필요)
  .\publish.ps1 -SelfContained  # 런타임 포함 (크지만 아무 PC 에서 바로 실행)
#>
param(
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $root "src\YtmDock\YtmDock.csproj"
$out  = Join-Path $root ("publish\" + ($(if ($SelfContained) { "self-contained" } else { "framework-dependent" })))

$args = @(
    "publish", $proj,
    "-c", "Release",
    "-r", "win-x64",
    "-o", $out,
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:DebugType=none"
)
if ($SelfContained) {
    $args += "--self-contained"; $args += "true"
    # 런타임 포함 exe 는 WPF+WinForms 가 통째로 들어가 ~150MB → 단일 파일 압축으로 줄인다 (첫 실행이 조금 느려짐)
    $args += "-p:EnableCompressionInSingleFile=true"
}
else { $args += "--self-contained"; $args += "false" }

Write-Host "dotnet $($args -join ' ')" -ForegroundColor DarkGray
& dotnet @args
if ($LASTEXITCODE -ne 0) { Write-Host "`n빌드 실패 (dotnet 종료 코드 $LASTEXITCODE)" -ForegroundColor Red; exit $LASTEXITCODE }

$exe = Join-Path $out "YtmDock.exe"
if (Test-Path $exe) {
    $mb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host "`n완료: $exe  ($mb MB)" -ForegroundColor Green
}
