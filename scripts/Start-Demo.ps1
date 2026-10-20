#requires -Version 7.4
[CmdletBinding()]
param(
    [switch] $Stop,
    [switch] $RemoveData,
    [switch] $ShowCredentials,
    [switch] $NoBrowser
)

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'This local demo script requires Windows (DPAPI credential storage).' }
if ($RemoveData -and -not $Stop) { throw 'Use -Stop -RemoveData to explicitly delete the demo database and keys.' }
if ($ShowCredentials -and ($Stop -or $NoBrowser)) { throw '-ShowCredentials is a separate read-only command.' }
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$directory = Join-Path $root 'secrets/demo'
$environmentFile = Join-Path $directory 'compose.env'
$accountFile = Join-Path $directory 'accounts.clixml'
$processFile = Join-Path $directory 'processes.json'
$project = 'identity-demo'
$api = 'https://localhost:5443'
$web = 'https://localhost:5173'
$previous = @{}
$operationLock = $null
$started = $false

function Set-DemoEnvironment([string] $name, [string] $value) {
    if (-not $previous.ContainsKey($name)) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
    [Environment]::SetEnvironmentVariable($name, $value)
}
function Invoke-DemoCompose {
    & docker compose --file (Join-Path $root 'compose.yaml') --env-file $environmentFile --project-name $project @args
    if ($LASTEXITCODE -ne 0) { throw 'Demo Docker Compose command failed.' }
}
function Invoke-DemoTool([string] $file, [string[]] $arguments) {
    & $file @arguments
    if ($LASTEXITCODE -ne 0) { throw "Demo prerequisite command failed: $file" }
}
function Read-DemoProcesses {
    if (Test-Path -LiteralPath $processFile) { return Get-Content -LiteralPath $processFile -Raw | ConvertFrom-Json }
    return [pscustomobject]@{ api = $null; web = $null }
}
function Get-DemoProcess($entry) {
    if ($null -eq $entry) { return $null }
    $process = Get-Process -Id $entry.id -ErrorAction SilentlyContinue
    if ($process -and $process.StartTime.ToUniversalTime().Ticks.ToString() -eq $entry.startedAt) { return $process }
    return $null
}
function Stop-DemoProcesses {
    $processes = Read-DemoProcesses
    foreach ($entry in @($processes.api, $processes.web)) {
        $process = Get-DemoProcess $entry
        if ($process) { Stop-Process -Id $process.Id -Force }
    }
    if (Test-Path -LiteralPath $processFile) { Remove-Item -LiteralPath $processFile }
}
function Wait-DemoEndpoint([string] $url) {
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        try {
            $response = Invoke-WebRequest $url -TimeoutSec 2 -SkipHttpErrorCheck
            if ($response.StatusCode -eq 200) { return }
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    throw 'Demo service did not become ready. See secrets/demo/*.log and *.err.'
}
function Invoke-DemoJson([string] $path, $body) {
    # Do not emit request bodies, responses with tokens or underlying HTTP exceptions.
    try {
        return Invoke-RestMethod "$api/api/v1/auth/$path" -Method Post -ContentType 'application/json' -Body ($body | ConvertTo-Json -Compress) -TimeoutSec 15
    } catch { throw "Demo API operation failed: $path. See safe API logs in secrets/demo." }
}
function Try-DemoLogin($account) {
    try {
        $response = Invoke-WebRequest "$api/api/v1/auth/login" -Method Post -ContentType 'application/json' -Body (@{
            email = $account.Credential.UserName
            password = $account.Credential.GetNetworkCredential().Password
            clientId = 'native'
        } | ConvertTo-Json -Compress) -SkipHttpErrorCheck -TimeoutSec 15
        if ($response.StatusCode -eq 401) { return $false }
        if ($response.StatusCode -ne 200) { throw 'Unexpected login status.' }
        $tokens = $response.Content | ConvertFrom-Json
        $null = Invoke-DemoJson 'logout' @{ refreshToken = $tokens.refreshToken }
        return $true
    } catch { throw 'Demo account login check failed.' }
}
function Confirm-DemoEmail([string] $email) {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(45)
    $attempted = [Collections.Generic.HashSet[string]]::new()
    do {
        $messages = Invoke-RestMethod 'http://127.0.0.1:8025/api/v1/messages?limit=100' -TimeoutSec 5
        # Enumerate recipients explicitly: Object[].Address is a .NET method in PowerShell.
        $matchingMessages = $messages.messages | Where-Object {
            $_.Subject -eq 'Verify your Identity email' -and
            (@($_.To | ForEach-Object { $_.Address }) -contains $email)
        }
        foreach ($message in $matchingMessages) {
            if (-not $attempted.Add($message.ID)) { continue }
            $mail = Invoke-RestMethod "http://127.0.0.1:8025/api/v1/message/$($message.ID)" -TimeoutSec 5
            $match = [regex]::Match($mail.Text, '#token=([A-Za-z0-9_-]{43})')
            if (-not $match.Success) { throw 'Verification email has no valid fragment token.' }
            try {
                $response = Invoke-WebRequest "$api/api/v1/auth/email/verify" -Method Post -ContentType 'application/json' -Body (
                    @{token = $match.Groups[1].Value} | ConvertTo-Json -Compress
                ) -SkipHttpErrorCheck -TimeoutSec 15
            } catch { throw 'Demo email verification request failed.' }
            if ($response.StatusCode -eq 204) { return }
            # Resend can invalidate the initial registration link before the worker delivers it.
            if ($response.StatusCode -ne 400) { throw 'Unexpected demo email verification status.' }
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw 'Demo verification email was not delivered within 45 seconds.'
}
function Test-DemoAdmin($account) {
    try {
        $csrf = Invoke-RestMethod "$web/api/v1/admin/auth/csrf" -SessionVariable session -TimeoutSec 10
        $null = Invoke-RestMethod "$web/api/v1/admin/auth/login" -WebSession $session -Method Post -Headers @{
            'X-CSRF-TOKEN' = $csrf.token
        } -ContentType 'application/json' -Body (@{
            email = $account.Credential.UserName
            password = $account.Credential.GetNetworkCredential().Password
        } | ConvertTo-Json -Compress) -TimeoutSec 10
        $me = Invoke-RestMethod "$web/api/v1/admin/auth/me" -WebSession $session -TimeoutSec 10
        if ($me.role -ne $account.Role) { throw 'Unexpected role.' }
        $null = Invoke-RestMethod "$web/api/v1/admin/dashboard" -WebSession $session -TimeoutSec 10
        $csrf = Invoke-RestMethod "$web/api/v1/admin/auth/csrf" -WebSession $session -TimeoutSec 10
        $null = Invoke-RestMethod "$web/api/v1/admin/auth/logout" -WebSession $session -Method Post -Headers @{ 'X-CSRF-TOKEN' = $csrf.token } -TimeoutSec 10
    } catch { throw 'Demo administrative login or dashboard check failed.' }
}

Push-Location $root
try {
    if ($ShowCredentials) {
        if (-not (Test-Path -LiteralPath $accountFile)) { throw 'Start the demo first.' }
        # Explicit operator display only; do not write this output to application logs.
        Import-Clixml -LiteralPath $accountFile | ForEach-Object {
            [pscustomobject]@{ Email = $_.Credential.UserName; Role = $_.Role; Password = $_.Credential.GetNetworkCredential().Password }
        } | Format-Table -AutoSize
        return
    }
    if ($Stop -and -not (Test-Path -LiteralPath $directory)) { Write-Output 'Demo is already stopped.'; return }
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    # Limit demo secrets to the current Windows user and SYSTEM. No global ACLs are changed.
    $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    & icacls $directory /inheritance:r /grant:r "*${sid}:(OI)(CI)F" '*S-1-5-18:(OI)(CI)F' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot protect the demo secret directory.' }
    try { $operationLock = [IO.File]::Open((Join-Path $directory 'operation.lock'), 'OpenOrCreate', 'ReadWrite', 'None') }
    catch { throw 'Another demo start/stop is in progress.' }
    if ($Stop) {
        Stop-DemoProcesses
        if (Test-Path -LiteralPath $environmentFile) {
            if ($RemoveData) { Invoke-DemoCompose down --volumes --remove-orphans }
            else { Invoke-DemoCompose down --remove-orphans }
        }
        if ($RemoveData) {
            $operationLock.Dispose(); $operationLock = $null
            $resolved = [IO.Path]::GetFullPath($directory)
            if ($resolved -ne [IO.Path]::GetFullPath((Join-Path $root 'secrets/demo'))) { throw 'Invalid demo cleanup target.' }
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
        Write-Output 'Demo stopped; its containers were removed.'
        return
    }
    # Restart only our own processes before rebuilding files / reinstalling node_modules on Windows.
    Stop-DemoProcesses
    foreach ($tool in @('docker', 'dotnet', 'node', 'npm')) { $null = Get-Command $tool -ErrorAction Stop }
    Invoke-DemoTool 'docker' @('info', '--format', '{{.OSType}}')
    Invoke-DemoTool 'dotnet' @('restore', 'Identity.slnx')
    Invoke-DemoTool 'dotnet' @('tool', 'restore')
    Invoke-DemoTool 'dotnet' @('build', 'Identity.slnx', '--configuration', 'Release', '--no-restore', '--disable-build-servers', '-m:1')
    Push-Location (Join-Path $root 'src/Identity.Admin.Web')
    try { Invoke-DemoTool 'npm' @('ci', '--no-audit', '--no-fund') } finally { Pop-Location }
    Invoke-DemoTool 'dotnet' @('dev-certs', 'https', '--trust', '--export-path', (Join-Path $directory 'localhost.pem'), '--format', 'Pem', '--no-password')
    if (-not (Test-Path -LiteralPath $environmentFile)) {
        # Refuse to attach newly generated credentials to an older orphaned demo database.
        $volumes = & docker volume ls --filter "label=com.docker.compose.project=$project" --format '{{.Name}}'
        if ($LASTEXITCODE -ne 0 -or $volumes) { throw 'Demo volumes exist without matching configuration. Restore secrets/demo or explicitly remove the orphaned demo volume.' }
        $databasePassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
        [IO.File]::WriteAllLines($environmentFile, @(
            "POSTGRES_PASSWORD=$databasePassword", 'POSTGRES_PORT=54320', 'MAILPIT_HTTP_PORT=8025', 'MAILPIT_SMTP_PORT=1025',
            'API_PORT=5080', 'EMAIL_VERIFICATION_URL_TEMPLATE=https://localhost:5173/verify#token={token}',
            'RESET_PASSWORD_URL_TEMPLATE=https://localhost:5173/account/reset-password#token={token}'
        ))
    }
    if (-not (Test-Path (Join-Path $directory 'outbox.key'))) {
        [IO.File]::WriteAllBytes((Join-Path $directory 'outbox.key'), [Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    }
    if (-not (Test-Path (Join-Path $directory 'jwt.pem'))) {
        $rsa = [Security.Cryptography.RSA]::Create(3072)
        try { [IO.File]::WriteAllText((Join-Path $directory 'jwt.pem'), $rsa.ExportPkcs8PrivateKeyPem()) }
        finally { $rsa.Dispose() }
    }
    New-Item -ItemType Directory -Path (Join-Path $directory 'data-protection') -Force | Out-Null
    $local = Get-Content -LiteralPath $environmentFile -Raw | ConvertFrom-StringData
    $connection = [System.Data.Common.DbConnectionStringBuilder]::new()
    $connection['Host'] = '127.0.0.1'; $connection['Port'] = $local.POSTGRES_PORT
    $connection['Database'] = 'identity'; $connection['Username'] = 'identity'; $connection['Password'] = $local.POSTGRES_PASSWORD
    $settings = @{
        Database__ConnectionString = $connection.ConnectionString
        ASPNETCORE_ENVIRONMENT = 'Development'; ASPNETCORE_URLS = "$api;http://127.0.0.1:5080"
        ASPNETCORE_Kestrel__Certificates__Default__Path = (Join-Path $directory 'localhost.pem')
        ASPNETCORE_Kestrel__Certificates__Default__KeyPath = (Join-Path $directory 'localhost.key')
        AdminAccess__Enabled = 'true'; AdminAccess__DataProtectionKeyPath = (Join-Path $directory 'data-protection')
        Jwt__Enabled = 'true'; Jwt__Issuer = 'https://identity.localhost'; Jwt__ActiveKeyId = 'demo-jwt'
        Jwt__PrivateKeyPath = (Join-Path $directory 'jwt.pem')
        Registration__Enabled = 'true'; PasswordReset__Enabled = 'true'; Invitations__Enabled = 'true'
        Outbox__Enabled = 'true'; Outbox__PollSeconds = '0.5'; OutboxEncryption__ActiveKeyId = 'demo'
        OutboxEncryption__Keys__demo = (Join-Path $directory 'outbox.key')
        Email__FromAddress = 'no-reply@identity.example'; Email__Smtp__Host = '127.0.0.1'
        Email__Smtp__Port = $local.MAILPIT_SMTP_PORT; Email__Smtp__Tls = 'None'
        Email__VerificationUrlTemplate = "$web/verify#token={token}"
        Email__ResetPasswordUrlTemplate = "$web/account/reset-password#token={token}"
        Email__InvitationUrlTemplate = "$web/account/activate#token={token}"
        Identity__Clients__0__ClientId = 'native'; Identity__Clients__0__Audience = 'identity-native'; Identity__Clients__0__Enabled = 'true'
        AuthRateLimits__IpPermitLimit = '1000'; AuthRateLimits__AccountPermitLimit = '30'
        IDENTITY_DEV_HTTPS_CERT_FILE = (Join-Path $directory 'localhost.pem'); IDENTITY_DEV_HTTPS_KEY_FILE = (Join-Path $directory 'localhost.key')
        NODE_EXTRA_CA_CERTS = (Join-Path $directory 'localhost.pem'); IDENTITY_API_PROXY_TARGET = $api
    }
    foreach ($entry in $settings.GetEnumerator()) { Set-DemoEnvironment $entry.Key $entry.Value }
    $processes = Read-DemoProcesses
    # Reuse only this script's processes; refuse occupied ports instead of stopping unrelated services.
    foreach ($service in @(@{name='api';ports=@(5080,5443)}, @{name='web';ports=@(5173)})) {
        if (-not (Get-DemoProcess $processes.($service.name))) {
            foreach ($port in $service.ports) {
                $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $port)
                try { $listener.Start() } catch { throw "Demo port $port is busy. Stop its owner or the previous demo first." }
                finally { $listener.Stop() }
            }
        }
    }
    $started = $true
    Invoke-DemoCompose up --detach --wait postgres mailpit
    Invoke-DemoTool 'dotnet' @('ef', 'database', 'update', '--project', 'src/Identity.Infrastructure', '--configuration', 'Release', '--no-build')
    foreach ($service in @('api', 'web')) {
        if (Get-DemoProcess $processes.$service) { continue }
        if ($service -eq 'api') { $file = 'dotnet'; $arguments = @('src/Identity.Api/bin/Release/net10.0/Identity.Api.dll'); $work = $root }
        else { $file = 'node'; $arguments = @('node_modules/vite/bin/vite.js'); $work = Join-Path $root 'src/Identity.Admin.Web' }
        $process = Start-Process $file -ArgumentList $arguments -WorkingDirectory $work -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $directory "$service.log") -RedirectStandardError (Join-Path $directory "$service.err")
        $processes.$service = @{ id = $process.Id; startedAt = $process.StartTime.ToUniversalTime().Ticks.ToString() }
        $processes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $processFile
    }
    Wait-DemoEndpoint "$api/health/ready"
    Wait-DemoEndpoint "$web/admin/login"
    if (-not (Test-Path -LiteralPath $accountFile)) {
        $accounts = @()
        $definitions = @(
            @{Email='admin@demo.identity.example'; Name='Demo Administrator'; Role='IdentityAdministrator'},
            @{Email='reader@demo.identity.example'; Name='Demo Reader'; Role='IdentityReadOnly'}
        )
        foreach ($number in 1..10) { $definitions += @{Email=('user{0:D2}@demo.identity.example' -f $number); Name="Demo User $number"; Role='User'} }
        foreach ($definition in $definitions) {
            $password = 'Id!' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(16))
            $accounts += [pscustomobject]@{
                Credential = [PSCredential]::new($definition.Email, (ConvertTo-SecureString $password -AsPlainText -Force))
                Name = $definition.Name; Role = $definition.Role
            }
        }
        # Windows Export-Clixml encrypts PSCredential passwords with user/machine-bound DPAPI.
        $accounts | Export-Clixml -LiteralPath $accountFile
    }
    $accounts = @(Import-Clixml -LiteralPath $accountFile)
    if ($accounts.Count -ne 12) { throw 'Unexpected demo credential file; restore it or use -Stop -RemoveData.' }
    # The operator explicitly requested a plaintext local-debug credential file.
    # Keep it in the protected, Git-ignored directory, never in source or service logs.
    $credentialLines = @(
        '# Identity — учётки для локальной отладки', '',
        'Это не секреты, а просто тестовые учётки для локальной отладки. Только локальное демо.', '',
        'Вход: https://localhost:5173/admin/login', '',
        '| Email | Доступ | Пароль |', '| --- | --- | --- |'
    )
    foreach ($account in $accounts) {
        $credentialLines += '| ' + $account.Credential.UserName + ' | ' + $account.Role + ' | ' + $account.Credential.GetNetworkCredential().Password + ' |'
    }
    $credentialLines += @('', 'Повторный запуск сохраняет эти учётки и пароли. Команда -Stop -RemoveData удаляет их вместе с демо-БД.')
    [IO.File]::WriteAllLines((Join-Path $directory 'ACCOUNTS.md'), $credentialLines)
    foreach ($account in $accounts) {
        $email = $account.Credential.UserName
        if (-not (Try-DemoLogin $account)) {
            $null = Invoke-DemoJson 'register' @{
                email = $email; password = $account.Credential.GetNetworkCredential().Password; displayName = $account.Name; clientId = 'native'
            }
            # Also recover a partially completed earlier run whose Mailpit container was removed.
            $null = Invoke-DemoJson 'email/verification-request' @{email = $email}
            Confirm-DemoEmail $email
            if (-not (Try-DemoLogin $account)) { throw 'New demo account failed its login check.' }
        }
        if ($account.Role -ne 'User') {
            Invoke-DemoTool 'dotnet' @('src/Identity.AdminCli/bin/Release/net10.0/Identity.AdminCli.dll', 'grant', '--email', $email, '--role', $account.Role)
            Test-DemoAdmin $account
        }
        Write-Output "Ready: $email ($($account.Role))"
    }
    Write-Output "Identity Console: $web/admin"
    Write-Output 'Mailpit: http://localhost:8025'
    Write-Output 'Local debug credentials: secrets/demo/ACCOUNTS.md (Git-ignored)'
    Write-Output 'Show passwords explicitly: pwsh -NoProfile -File scripts/Start-Demo.ps1 -ShowCredentials'
    Write-Output 'Stop: pwsh -NoProfile -File scripts/Start-Demo.ps1 -Stop'
    if (-not $NoBrowser) { Start-Process "$web/admin" }
} catch {
    if ($started) {
        Stop-DemoProcesses
        try { Invoke-DemoCompose down --remove-orphans } catch { Write-Warning 'Automatic demo container cleanup failed; run -Stop.' }
    }
    throw
} finally {
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
    if ($operationLock) { $operationLock.Dispose() }
    Pop-Location
}
