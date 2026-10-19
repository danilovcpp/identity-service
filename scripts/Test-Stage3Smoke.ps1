#requires -Version 7.4
# Isolated local-only smoke. Requires a Release build, dotnet-ef and Docker Linux containers.
param([switch] $Stage4, [switch] $Stage5, [switch] $Stage6, [switch] $Stage7)
$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $root
$suffix = [Guid]::NewGuid().ToString('N')
if ($Stage7) { $Stage6 = $true }
if ($Stage6) { $Stage5 = $true }
$stage = if ($Stage7) { 7 } elseif ($Stage6) { 6 } elseif ($Stage5) { 5 } elseif ($Stage4) { 4 } else { 3 }
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
        'JWT_ISSUER=https://identity.localhost', 'EMAIL_VERIFICATION_URL_TEMPLATE=http://localhost:3000/verify#token={token}',
        'RESET_PASSWORD_URL_TEMPLATE=http://localhost:3000/reset#token={token}', 'AUTH_IP_PERMIT_LIMIT=1000', 'AUTH_ACCOUNT_PERMIT_LIMIT=100',
        'INVITATIONS_ENABLED=false'
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
    if ($Stage4 -or $Stage5) {
        $headers = @{ Authorization = "Bearer $($pair.accessToken)" }
        $sessions = Invoke-RestMethod "$api/api/v1/sessions" -Headers $headers
        Assert-True ($sessions.Count -eq 2 -and @($sessions | Where-Object isCurrent).Count -eq 1) 'Own session list invalid.'
        $rotated = Invoke-RestMethod "$api/api/v1/auth/refresh" -Method Post -ContentType 'application/json' -Body (@{ refreshToken = $pair.refreshToken } | ConvertTo-Json -Compress)
        Assert-True ($rotated.refreshToken -cne $pair.refreshToken) 'Rotation reused the old token.'
        if ($Stage7) {
            $profile = Invoke-RestMethod "$api/api/v1/users/me" -Headers @{ Authorization = "Bearer $($rotated.accessToken)" }
            Assert-True ($profile.email -eq 'smoke@identity.example' -and $profile.status -eq 'active') 'Rotated token cannot read own profile.'
        }
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
        if ($Stage7) {
            $last = Invoke-RestMethod "$api/api/v1/auth/refresh" -Method Post -ContentType 'application/json' -Body (@{ refreshToken = $last.refreshToken } | ConvertTo-Json -Compress)
            $profile = Invoke-RestMethod "$api/api/v1/users/me" -Headers @{ Authorization = "Bearer $($last.accessToken)" }
            Assert-True ($profile.emailVerified) 'Profile email is not verified.'
        }
        $logoutBody = @{ refreshToken = $last.refreshToken } | ConvertTo-Json -Compress
        $logout = Invoke-WebRequest "$api/api/v1/auth/logout" -Method Post -ContentType 'application/json' -Body $logoutBody
        $repeat = Invoke-WebRequest "$api/api/v1/auth/logout" -Method Post -ContentType 'application/json' -Body $logoutBody
        Assert-True ($logout.StatusCode -eq 204 -and $repeat.StatusCode -eq 204) 'Logout is not idempotent.'
        $afterLogout = Invoke-WebRequest "$api/api/v1/auth/refresh" -Method Post -ContentType 'application/json' -Body $logoutBody -SkipHttpErrorCheck
        Assert-True ($afterLogout.StatusCode -eq 401) 'Logout left a refresh token active.'
        $state = Invoke-Compose exec -T postgres psql -U identity -d identity -t -A -c "SELECT (SELECT count(*) FROM sessions), (SELECT count(*) FROM refresh_tokens), (SELECT bool_and(revoked_at IS NOT NULL) FROM refresh_tokens), (SELECT bool_and(audience = 'identity-native') FROM sessions), (SELECT count(*) FROM security_audit_events WHERE event_type = 'refresh_replay_detected');"
        $expectedState = if ($Stage7) { '4|7|t|t|1' } else { '4|6|t|t|1' }
        Assert-True (($state -join '').Trim() -eq $expectedState) 'Stage 4 persisted state invalid.'
        Write-Output 'Stage 4 Docker smoke passed: registration/email/login/JWKS -> rotation -> restart -> rotation -> replay/family revoke -> own sessions/revoke -> logout-all -> login/logout -> failed refresh; non-root.'
    } else {
        Write-Output 'Stage 3 Docker smoke passed: register -> Mailpit -> verify -> login -> public JWKS -> restart -> login; persisted sessions/hashes, cleared outbox, non-root.'
    }
    if ($Stage5) {
        $profilePair = Invoke-RestMethod "$api/api/v1/auth/login" -Method Post -ContentType 'application/json' -Body $body
        $profileHeaders = @{ Authorization = "Bearer $($profilePair.accessToken)" }
        $profile = Invoke-RestMethod "$api/api/v1/users/me" -Headers $profileHeaders
        Assert-True ($profile.email -eq 'smoke@identity.example' -and $profile.emailVerified -and $profile.status -eq 'active') 'Profile invalid.'
        $updated = Invoke-RestMethod "$api/api/v1/users/me" -Method Patch -ContentType 'application/json' -Headers $profileHeaders -Body '{"displayName":"Smoke Updated"}'
        Assert-True ($updated.displayName -eq 'Smoke Updated' -and $updated.id -eq $profile.id) 'Profile update invalid.'
        $changedPassword = 'Changed smoke passphrase ' + [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(16))
        $change = Invoke-WebRequest "$api/api/v1/auth/password/change" -Method Post -Headers $profileHeaders -ContentType 'application/json' -Body (@{ currentPassword = $password; newPassword = $changedPassword } | ConvertTo-Json -Compress)
        Assert-True ($change.StatusCode -eq 204) 'Password change failed.'
        $staleProfile = Invoke-WebRequest "$api/api/v1/users/me" -Headers $profileHeaders -SkipHttpErrorCheck
        Assert-True ($staleProfile.StatusCode -eq 401) 'Password change retained old JWT session.'
        $oldLogin = Invoke-WebRequest "$api/api/v1/auth/login" -Method Post -ContentType 'application/json' -Body $body -SkipHttpErrorCheck
        Assert-True ($oldLogin.StatusCode -eq 401) 'Old password still works.'
        $changedBody = @{ email = 'smoke@identity.example'; password = $changedPassword; clientId = 'native' } | ConvertTo-Json -Compress
        $changedPair = Invoke-RestMethod "$api/api/v1/auth/login" -Method Post -ContentType 'application/json' -Body $changedBody
        $ignored = Invoke-RestMethod "$mailpit/api/v1/messages" -Method Delete
        $forgot = Invoke-WebRequest "$api/api/v1/auth/password/forgot" -Method Post -ContentType 'application/json' -Body '{"email":"smoke@identity.example"}'
        Assert-True ($forgot.StatusCode -eq 202) 'Forgot password did not return generic 202.'
        $resetEmail = $null
        for ($attempt = 0; $attempt -lt 30; $attempt++) {
            $messages = Invoke-RestMethod "$mailpit/api/v1/messages"
            $resetEmail = @($messages.messages | Where-Object Subject -eq 'Reset your Identity password') | Select-Object -First 1
            if ($null -ne $resetEmail) { break }
            Start-Sleep -Seconds 1
        }
        Assert-True ($null -ne $resetEmail) 'Reset email was not delivered.'
        $detail = Invoke-RestMethod "$mailpit/api/v1/message/$($resetEmail.ID)"
        $resetMatch = [regex]::Match($detail.Text, '#token=([A-Za-z0-9_-]{43})')
        Assert-True $resetMatch.Success 'Reset link missing.'
        $resetPassword = 'Reset smoke passphrase ' + [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(16))
        $resetBody = @{ token = $resetMatch.Groups[1].Value; newPassword = $resetPassword } | ConvertTo-Json -Compress
        $reset = Invoke-WebRequest "$api/api/v1/auth/password/reset" -Method Post -ContentType 'application/json' -Body $resetBody
        $repeatReset = Invoke-WebRequest "$api/api/v1/auth/password/reset" -Method Post -ContentType 'application/json' -Body $resetBody -SkipHttpErrorCheck
        Assert-True ($reset.StatusCode -eq 204 -and $repeatReset.StatusCode -eq 400) 'Password reset is not single-use.'
        $staleRefresh = Invoke-WebRequest "$api/api/v1/auth/refresh" -Method Post -ContentType 'application/json' -Body (@{ refreshToken = $changedPair.refreshToken } | ConvertTo-Json -Compress) -SkipHttpErrorCheck
        Assert-True ($staleRefresh.StatusCode -eq 401) 'Password reset retained old refresh session.'
        $resetLoginBody = @{ email = 'smoke@identity.example'; password = $resetPassword; clientId = 'native' } | ConvertTo-Json -Compress
        $resetPair = Invoke-RestMethod "$api/api/v1/auth/login" -Method Post -ContentType 'application/json' -Body $resetLoginBody
        $deactivate = Invoke-WebRequest "$api/api/v1/users/me/deactivate" -Method Post -ContentType 'application/json' -Headers @{ Authorization = "Bearer $($resetPair.accessToken)" } -Body (@{ password = $resetPassword } | ConvertTo-Json -Compress)
        Assert-True ($deactivate.StatusCode -eq 204) 'Deactivation failed.'
        $disabledLogin = Invoke-WebRequest "$api/api/v1/auth/login" -Method Post -ContentType 'application/json' -Body $resetLoginBody -SkipHttpErrorCheck
        Assert-True ($disabledLogin.StatusCode -eq 401) 'Deactivated account can log in.'
        $state = Invoke-Compose exec -T postgres psql -U identity -d identity -t -A -c "SELECT (SELECT count(*) FROM sessions), (SELECT count(*) FROM refresh_tokens), (SELECT bool_and(revoked_at IS NOT NULL) FROM refresh_tokens), (SELECT bool_and(status = 3 AND display_name = 'Smoke Updated') FROM users), (SELECT bool_and(consumed_at IS NOT NULL) FROM one_time_tokens WHERE purpose = 1), (SELECT count(*) FROM security_audit_events WHERE event_type = 'password_changed'), (SELECT count(*) FROM security_audit_events WHERE event_type = 'password_reset'), (SELECT count(*) FROM security_audit_events WHERE event_type = 'account_deactivated');"
        $expectedFinal = if ($Stage7) { '7|10|t|t|t|1|1|1' } else { '7|9|t|t|t|1|1|1' }
        Assert-True (($state -join '').Trim() -eq $expectedFinal) 'Stage 5 persisted state invalid.'
        Write-Output 'Stage 5 Docker smoke passed: profile/update -> password change/all-session revoke -> Mailpit reset/single-use -> new login -> deactivation -> failed login; stage 3/4 checks included.'
    }
    if ($Stage6) {
        $invalid = Invoke-WebRequest "$api/api/v1/auth/password/reset" -Method Post -ContentType 'application/json' -Body '{"token":"invalid","newPassword":"x"}' -SkipHttpErrorCheck
        Assert-True ($invalid.StatusCode -eq 400) 'Expected generic invalid reset response.'
        Assert-True ($invalid.Headers['X-Correlation-ID'].Count -eq 1) 'Missing correlation header.'
        Assert-True ($invalid.Headers['X-Content-Type-Options'] -contains 'nosniff') 'Missing security headers.'
        $indexes = Invoke-Compose exec -T postgres psql -U identity -d identity -tAc "SELECT count(*) FROM pg_indexes WHERE indexname = 'ix_outbox_processed_at';"
        Assert-True (($indexes -join '').Trim() -eq '1') 'Missing security maintenance index.'
        Write-Output 'Stage 6 Docker smoke passed: security headers/correlation, generic error contract, maintenance migration; stage 3/4/5 flows included.'
    }
    if ($Stage7) {
        $containerId = (Invoke-Compose ps -q api | Select-Object -Last 1).Trim()
        $health = 'starting'
        for ($attempt = 0; $attempt -lt 40 -and $health -ne 'healthy'; $attempt++) {
            $health = & docker inspect --format '{{.State.Health.Status}}' $containerId
            if ($LASTEXITCODE -ne 0) { throw 'Cannot read container health.' }
            if ($health -ne 'healthy') { Start-Sleep -Seconds 1 }
        }
        Assert-True ($health -eq 'healthy') 'Container is not healthy.'
        Invoke-Compose exec -T api dotnet Identity.Api.dll --health-check
        $version = & docker inspect --format '{{index .Config.Labels "org.opencontainers.image.version"}}' $containerId
        if ($LASTEXITCODE -ne 0) { throw 'Cannot read container version.' }
        Assert-True ($version -eq '1.0.0') 'Container release version invalid.'
        Write-Output 'Stage 7 Docker smoke passed: register -> Mailpit -> verify -> login -> refresh -> me -> logout -> failed refresh; non-root, healthcheck and version 1.0.0; all earlier stage smoke checks included.'
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
