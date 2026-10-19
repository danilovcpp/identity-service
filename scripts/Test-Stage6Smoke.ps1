#requires -Version 7.4
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Test-Stage3Smoke.ps1') -Stage6
