#requires -Version 7.4
# Reuse the isolated bootstrap and cleanup from the preceding stage.
& (Join-Path $PSScriptRoot 'Test-Stage3Smoke.ps1') -Stage4
