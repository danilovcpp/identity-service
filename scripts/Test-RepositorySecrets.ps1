#requires -Version 7.4
param([switch] $IncludeUntracked, [switch] $SelfTest)
$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

function Get-SecretPathRule([string] $name) {
    if ($name -match '(^|/)(secrets|data-protection-keys)/|\.(pem|key|pfx|p12|pgpass)$|(^|/)key-[0-9a-f-]{36}\.xml$') { return 'secret-file' }
    if (($name -match '(^|/)\.env($|\.)|(^|/)[^/]+\.env$') -and -not $name.EndsWith('.example')) { return 'environment-file' }
    return $null
}
function Get-SecretContentRule([string] $content) {
    if ($content -match '<masterKey(?:\s|>)') { return 'data-protection-key' }
    if ($content -match '-----BEGIN (RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY-----') { return 'private-key' }
    if ($content -match '\bgh[pousr]_[A-Za-z0-9]{36,}\b|\bgithub_pat_[A-Za-z0-9_]{60,}\b|\bAKIA[A-Z0-9]{16}\b') { return 'access-key' }
    if ($content -match '\beyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\b') { return 'jwt-literal' }
    return $null
}
if ($SelfTest) {
    foreach ($name in @('.env', '.env.production', 'deploy/production.env', 'secrets/key.txt', 'private.pem', 'data-protection-keys/key.xml', 'key-00000000-0000-0000-0000-000000000001.xml')) {
        if (-not (Get-SecretPathRule $name)) { throw 'Secret guard path self-test failed.' }
    }
    foreach ($name in @('.env.example', 'deploy/production.env.example', 'src/Identity.Api/Program.cs')) {
        if (Get-SecretPathRule $name) { throw 'Secret guard allowed-path self-test failed.' }
    }
    $header = ('-' * 5) + 'BEGIN ' + 'PRIVATE KEY' + ('-' * 5)
    if ((Get-SecretContentRule $header) -ne 'private-key') { throw 'Secret guard content self-test failed.' }
    if ((Get-SecretContentRule ('<master' + 'Key>')) -ne 'data-protection-key') { throw 'Secret guard Data Protection self-test failed.' }
    if ((Get-SecretContentRule ('ghp_' + ('a' * 36))) -ne 'access-key') { throw 'Secret guard token self-test failed.' }
    if (Get-SecretContentRule 'Password=<from-secret-provider>') { throw 'Secret guard placeholder self-test failed.' }
}
Push-Location $root
try {
    $names = (& git -c core.quotepath=false ls-files -z) -split "`0" | Where-Object { $_ }
    if ($LASTEXITCODE -ne 0) { throw 'Cannot list tracked files.' }
    if ($IncludeUntracked) {
        $names += (& git -c core.quotepath=false ls-files --others --exclude-standard -z) -split "`0" | Where-Object { $_ }
        if ($LASTEXITCODE -ne 0) { throw 'Cannot list untracked files.' }
    }
    $findings = @()
    foreach ($name in ($names | Sort-Object -Unique)) {
        $rule = Get-SecretPathRule $name
        if (-not $rule -and (Test-Path -LiteralPath $name -PathType Leaf)) {
            $rule = Get-SecretContentRule ([System.IO.File]::ReadAllText((Join-Path $root $name)))
        }
        # Never echo a matching line/value; only the filename and constant rule.
        if ($rule) { $findings += "$name ($rule)" }
    }
    if ($findings.Count -gt 0) { throw ('Repository secret guard rejected: ' + ($findings -join ', ')) }
    Write-Output 'Repository secret guard passed (current Git tree; not a full-history or all-secret-formats scanner).'
} finally { Pop-Location }
