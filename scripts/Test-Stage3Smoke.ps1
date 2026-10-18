#requires -Version 7.4
# Isolated local-only smoke. Requires a Release build, dotnet-ef and Docker Linux containers.
param([switch] $Stage4)
$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $root
$suffix = [Guid]::NewGuid().ToString('N')
$stage = if ($Stage4) { 4 } else { 3 }
$project = "identity-stage$stage-smoke-$suffix"
$environmentFile = Join-Path $root ".env.stage$stage-smoke-$suffix"
$secretDirectory = Join-Path $root "secrets/stage$stage-smoke-$suffix"
$outboxPath = Join-Path $secretDirectory 'outbox.key'
$jwtPath = Join-Path $secretDirectory 'jwt.pem'
$previousConnection = [Environment]::GetEnvironmentVariable('Database__ConnectionString')
$started = $false
$selectedPorts = [System.Collections.Generic.HashSet[int]]::new()

function Get-FreePort {
    do {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $listener.Start()
        try { $port = $listener.LocalEndpoint.Port } finally { $listener.Stop() }
    } until ($selectedPorts.Add($port))
    return $port
}

function Invoke-Compose {
    & docker compose --env-file $environmentFile --project-name $project @args
    if ($LASTEXITCODE -ne 0) { throw 'Docker Compose command failed.' }
}

function Assert-True([bool] $condition, [string] $message) {
    if (-not $condition) { throw $message }
}

try {
    New-Item -ItemType Directory -Path $secretDirectory | Out-Null
    [System.IO.File]::WriteAllBytes($outboxPath, [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $rsa = [System.Security.Cryptography.RSA]::Create(3072)
    try { [System.IO.File]::WriteAllText($jwtPath, $rsa.ExportPkcs8PrivateKeyPem()) } finally { $rsa.Dispose() }
    $databasePassword = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $apiPort = Get-FreePort
    $postgresPort = Get-FreePort
    $mailpitPort = Get-FreePort
    $smtpPort = Get-FreePort
    [System.IO.File]::WriteAllLines($environmentFile, @(
        "POSTGRES_PASSWORD=$databasePassword", "API_PORT=$apiPort", "POSTGRES_PORT=$postgresPort",
        "MAILPIT_HTTP_PORT=$mailpitPort", "MAILPIT_SMTP_PORT=$smtpPort",
        "OUTBOX_KEY_FILE=$($outboxPath.Replace('\', '/'))", "JWT_KEY_FILE=$($jwtPath.Replace('\', '/'))",
        'JWT_ISSUER=https://identity.localhost', 'EMAIL_VERIFICATION_URL_TEMPLATE=http://localhost:3000/verify#token={token}'
    ))
    Invoke-Compose config --quiet
    $started = $true
    Invoke-Compose up --detach --wait postgres mailpit
    $env:Database__ConnectionString = "Host=127.0.0.1;Port=$postgresPort;Database=identity;Username=identity;Password=$databasePassword"
    & dotnet ef database update --project src/Identity.Infrastructure --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Migration application failed.' }
    Invoke-Compose up --build --detach --wait api
    $api = "http://127.0.0.1:$apiPort"
    $mailpit = "http://127.0.0.1:$mailpitPort"
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try {
            $health = Invoke-RestMethod "$api/health/ready" -TimeoutSec 2
            if ($health.status -eq 'ready') { $ready = $true; break }
        } catch { }
        Start-Sleep -Seconds 1
    }
    Assert-True $ready 'API readiness failed.'
    $password = 'Smoke passphrase ' + [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(16))
    $body = @{ email = 'smoke@identity.example'; password = $password; clientId = 'native' } | ConvertTo-Json -Compress
    $registered = Invoke-WebRequest "$api/api/v1/auth/register" -Method Post -ContentType 'application/json' -Body $body
    Assert-True ($registered.StatusCode -eq 202) 'Registration did not return 202.'
    $emailId = $null
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $messages = Invoke-RestMethod "$mailpit/api/v1/messages" -TimeoutSec 3
        if ($messages.total -gt 0) { $emailId = $messages.messages[0].ID; break }
        Start-Sleep -Seconds 1
    }
    Assert-True ($null -ne $emailId) 'Mailpit did not receive the verification email.'
    $email = Invoke-RestMethod "$mailpit/api/v1/message/$emailId"
    $match = [regex]::Match($email.Text, '#token=([A-Za-z0-9_-]{43})')
    Assert-True $match.Success 'Verification link missing.'
    $verified = Invoke-WebRequest "$api/api/v1/auth/email/verify" -Method Post -ContentType 'application/json' -Body (@{ token = $match.Groups[1].Value } | ConvertTo-Json -Compress)
    Assert-True ($verified.StatusCode -eq 204) 'Email verification failed.'
    $pair = Invoke-RestMethod "$api/api/v1/auth/login" -Method Post -ContentType 'application/json' -Body $body
    Assert-True ($pair.expiresIn -eq 900 -and $pair.tokenType -eq 'Bearer' -and $pair.refreshToken.Length -eq 43 -and $pair.accessToken.Split('.').Length -eq 3) 'Login token response invalid.'
    $jwks = Invoke-RestMethod "$api/.well-known/jwks.json"
    Assert-True ($jwks.keys.Count -eq 1 -and $jwks.keys[0].kid -eq 'dev-jwt' -and $jwks.keys[0].alg -eq 'RS256') 'JWKS invalid.'
    Assert-True ((@($jwks.keys[0].PSObject.Properties.Name | Sort-Object) -join ',') -eq 'alg,e,kid,kty,n,use') 'JWKS contains unexpected parameters.'
    Invoke-Compose restart api
    $live = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try {
            $health = Invoke-RestMethod "$api/health/live" -TimeoutSec 2
            if ($health.status -eq 'healthy') { $live = $true; break }
        } catch { }
        Start-Sleep -Seconds 1
    }
    Assert-True $live 'API did not restart.'
    $after = Invoke-RestMethod "$api/.well-known/jwks.json"
    Assert-True ($after.keys[0].n -ceq $jwks.keys[0].n) 'RSA key changed across restart.'
    $second = Invoke-RestMethod "$api/api/v1/auth/login" -Method Post -ContentType 'application/json' -Body $body
    Assert-True ($second.refreshToken -cne $pair.refreshToken) 'New session reused a refresh token.'
    $database = Invoke-Compose exec -T postgres psql -U identity -d identity -t -A -c 'SELECT (SELECT count(*) FROM sessions), (SELECT count(*) FROM refresh_tokens), (SELECT bool_and(length(token_hash)=64 AND consumed_at IS NULL) FROM refresh_tokens), (SELECT bool_and(payload IS NULL) FROM outbox_messages);'
    Assert-True (($database -join '').Trim() -eq '2|2|t|t') 'Unexpected persisted session/token/outbox state.'
    $uid = Invoke-Compose exec -T api id -u
    Assert-True (($uid -join '').Trim() -ne '0') 'API runs as root.'
    if ($Stage4) {
        $headers = @{ Authorization = "Bearer $($pair.accessToken)" }
        $sessions = Invoke-RestMethod "$api/api/v1/sessions" -Headers $headers
        Assert-True ($sessions.Count -eq 2 -and @($sessions | Where-Object isCurrent).Count -eq 1) 'Own session list invalid.'
        $rotated = Invoke-RestMethod "$api/api/v1/auth/refresh" -Method Post -ContentType 'application/json' -Body (@{ refreshToken = $pair.refreshToken } | ConvertTo-Json -Compress)
        Assert-True ($rotated.refreshToken -cne $pair.refreshToken) 'Rotation reused the old token.'
        Invoke-Compose restart api
        $restarted = $false
        for ($attempt = 0; $attempt -lt 30; $attempt++) {
            try {
                $health = Invoke-RestMethod "$api/health/live" -TimeoutSec 2
                if ($health.status -eq 'healthy') { $restarted = $true; break }
            } catch { }
            Start-Sleep -Seconds 1
        }
        Assert-True $restarted 'API did not restart after rotation.'
        $next = Invoke-RestMethod "$api/api/v1/auth/refresh" -Method Post -ContentType 'application/json' -Body (@{ refreshToken = $rotated.refreshToken } | ConvertTo-Json -Compress)
        $replay = Invoke-WebRequest "$api/api/v1/auth/refresh" -Method Post -ContentType 'application/json' -Body (@{ refreshToken = $pair.refreshToken } | ConvertTo-Json -Compress) -SkipHttpErrorCheck
        Assert-True ($replay.StatusCode -eq 401) 'Replay was accepted.'
        $replayText = if ($replay.Content -is [byte[]]) { [System.Text.Encoding]::UTF8.GetString($replay.Content) } else { $replay.Content }
        Assert-True (($replayText | ConvertFrom-Json).code -eq 'invalid_refresh_token') 'Replay problem code invalid.'
        $denied = Invoke-WebRequest "$api/api/v1/auth/refresh" -Method Post -ContentType 'application/json' -Body (@{ refreshToken = $next.refreshToken } | ConvertTo-Json -Compress) -SkipHttpErrorCheck
        Assert-True ($denied.StatusCode -eq 401) 'Replay left the latest refresh token active.'
        $deniedAccess = Invoke-WebRequest "$api/api/v1/sessions" -Headers @{ Authorization = "Bearer $($next.accessToken)" } -SkipHttpErrorCheck
        Assert-True ($deniedAccess.StatusCode -eq 401) 'Revoked session JWT was accepted by Identity.'
        $otherHeaders = @{ Authorization = "Bearer $($second.accessToken)" }
        $third = Invoke-RestMethod "$api/api/v1/auth/login" -Method Post -ContentType 'application/json' -Body $body
        $list = Invoke-RestMethod "$api/api/v1/sessions" -Headers $otherHeaders
        $target = @($list | Where-Object { -not $_.isCurrent })
        Assert-True ($target.Count -eq 1) 'Session revoke target invalid.'
        $revoked = Invoke-WebRequest "$api/api/v1/sessions/$($target[0].id)" -Method Delete -Headers $otherHeaders
        Assert-True ($revoked.StatusCode -eq 204) 'Own session revoke failed.'
        $all = Invoke-WebRequest "$api/api/v1/auth/logout-all" -Method Post -Headers $otherHeaders
        Assert-True ($all.StatusCode -eq 204) 'Logout-all failed.'
        $afterAll = Invoke-WebRequest "$api/api/v1/sessions" -Headers $otherHeaders -SkipHttpErrorCheck
        Assert-True ($afterAll.StatusCode -eq 401) 'Logout-all left current JWT active on Identity.'
        $last = Invoke-RestMethod "$api/api/v1/auth/login" -Method Post -ContentType 'application/json' -Body $body
        $logoutBody = @{ refreshToken = $last.refreshToken } | ConvertTo-Json -Compress
        $logout = Invoke-WebRequest "$api/api/v1/auth/logout" -Method Post -ContentType 'application/json' -Body $logoutBody
        $repeat = Invoke-WebRequest "$api/api/v1/auth/logout" -Method Post -ContentType 'application/json' -Body $logoutBody
        Assert-True ($logout.StatusCode -eq 204 -and $repeat.StatusCode -eq 204) 'Logout is not idempotent.'
        $afterLogout = Invoke-WebRequest "$api/api/v1/auth/refresh" -Method Post -ContentType 'application/json' -Body $logoutBody -SkipHttpErrorCheck
        Assert-True ($afterLogout.StatusCode -eq 401) 'Logout left a refresh token active.'
        $state = Invoke-Compose exec -T postgres psql -U identity -d identity -t -A -c "SELECT (SELECT count(*) FROM sessions), (SELECT count(*) FROM refresh_tokens), (SELECT bool_and(revoked_at IS NOT NULL) FROM refresh_tokens), (SELECT bool_and(audience = 'identity-native') FROM sessions), (SELECT count(*) FROM security_audit_events WHERE event_type = 'refresh_replay_detected');"
        Assert-True (($state -join '').Trim() -eq '4|6|t|t|1') 'Stage 4 persisted state invalid.'
        Write-Output 'Stage 4 Docker smoke passed: registration/email/login/JWKS -> rotation -> restart -> rotation -> replay/family revoke -> own sessions/revoke -> logout-all -> login/logout -> failed refresh; non-root.'
    } else {
        Write-Output 'Stage 3 Docker smoke passed: register -> Mailpit -> verify -> login -> public JWKS -> restart -> login; persisted sessions/hashes, cleared outbox, non-root.'
    }
} finally {
    [Environment]::SetEnvironmentVariable('Database__ConnectionString', $previousConnection)
    $cleanupFailed = $false
    if ($started) {
        & docker compose --env-file $environmentFile --project-name $project down --volumes --remove-orphans
        $cleanupFailed = $LASTEXITCODE -ne 0
    }
    # Delete only this run's known files; no recursive deletion or shared-project cleanup.
    foreach ($file in @($outboxPath, $jwtPath, $environmentFile)) {
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file -Force }
    }
    if (Test-Path -LiteralPath $secretDirectory) { Remove-Item -LiteralPath $secretDirectory }
    Pop-Location
    if ($cleanupFailed) { throw 'Temporary Docker resources could not be removed; inspect the smoke project.' }
}
