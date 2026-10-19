#requires -Version 7.4
# Local, disposable HTTPS fixture. No production settings or global certificate trust are changed.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$suffix = [Guid]::NewGuid().ToString('N')
$project = "identity-admin-web-$suffix"
$directory = Join-Path $root "secrets/admin-web-$suffix"
$environmentFile = Join-Path $directory 'compose.env'
$process = $null
$started = $false
$previous = @{}
$ports = [Collections.Generic.HashSet[int]]::new()
function Set-TestEnvironment([string] $name, [string] $value) {
    if (-not $previous.ContainsKey($name)) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
    [Environment]::SetEnvironmentVariable($name, $value)
}
function Get-TestPort {
    do {
        $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
        $listener.Start()
        try { $port = $listener.LocalEndpoint.Port } finally { $listener.Stop() }
    } until ($ports.Add($port))
    return $port
}
function Invoke-TestCompose {
    & docker compose --env-file $environmentFile --project-name $project @args
    if ($LASTEXITCODE -ne 0) { throw 'Disposable Docker fixture failed.' }
}
Push-Location $root
try {
    New-Item -ItemType Directory -Path $directory | Out-Null
    $keyDirectory = Join-Path $directory 'data-protection'
    New-Item -ItemType Directory -Path $keyDirectory | Out-Null
    $outbox = Join-Path $directory 'outbox.key'
    [IO.File]::WriteAllBytes($outbox, [Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $rsa = [Security.Cryptography.RSA]::Create(3072)
    try {
        $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=localhost', $rsa,
            [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $san = [Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
        $san.AddDnsName('localhost')
        $san.AddIpAddress([Net.IPAddress]::Loopback)
        $request.CertificateExtensions.Add($san.Build())
        $certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-1), [DateTimeOffset]::UtcNow.AddDays(1))
        try {
            $pfxPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
            $pfx = Join-Path $directory 'localhost.pfx'
            $cert = Join-Path $directory 'localhost.pem'
            $key = Join-Path $directory 'localhost.key'
            [IO.File]::WriteAllBytes($pfx, $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $pfxPassword))
            [IO.File]::WriteAllText($cert, $certificate.ExportCertificatePem())
            [IO.File]::WriteAllText($key, $rsa.ExportPkcs8PrivateKeyPem())
        } finally { $certificate.Dispose() }
    } finally { $rsa.Dispose() }
    $pgPort = Get-TestPort
    $smtpPort = Get-TestPort
    $mailPort = Get-TestPort
    $apiPort = Get-TestPort
    $webPort = Get-TestPort
    $databasePassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    [IO.File]::WriteAllLines($environmentFile, @(
        "POSTGRES_PASSWORD=$databasePassword", "POSTGRES_PORT=$pgPort", "MAILPIT_SMTP_PORT=$smtpPort", "MAILPIT_HTTP_PORT=$mailPort",
        'EMAIL_VERIFICATION_URL_TEMPLATE=http://localhost/verify#token={token}', 'RESET_PASSWORD_URL_TEMPLATE=http://localhost/reset#token={token}'
    ))
    $started = $true
    Invoke-TestCompose up --detach --wait postgres mailpit
    Set-TestEnvironment Database__ConnectionString "Host=127.0.0.1;Port=$pgPort;Database=identity;Username=identity;Password=$databasePassword"
    & dotnet ef database update --project src/Identity.Infrastructure --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Fixture migrations failed.' }
    $api = "https://localhost:$apiPort"
    $settings = @{
        ASPNETCORE_ENVIRONMENT='Development'; ASPNETCORE_URLS=$api
        ASPNETCORE_Kestrel__Certificates__Default__Path=$pfx; ASPNETCORE_Kestrel__Certificates__Default__Password=$pfxPassword
        AdminAccess__Enabled='true'; AdminAccess__DataProtectionKeyPath=$keyDirectory
        Registration__Enabled='true'; Outbox__Enabled='true'; Outbox__PollSeconds='0.1'
        OutboxEncryption__ActiveKeyId='smoke'; OutboxEncryption__Keys__smoke=$outbox
        Email__FromAddress='no-reply@identity.example'; Email__VerificationUrlTemplate='https://localhost/verify#token={token}'
        Email__Smtp__Host='127.0.0.1'; Email__Smtp__Port="$smtpPort"; Email__Smtp__Tls='None'
        Identity__Clients__0__ClientId='native'; Identity__Clients__0__Audience='identity-native'; Identity__Clients__0__Enabled='true'
        SecurityMaintenance__Enabled='false'; SessionCleanup__Enabled='false'
        AuthRateLimits__IpPermitLimit='1000'; AuthRateLimits__AccountPermitLimit='100'
        IDENTITY_DEV_HTTPS_CERT_FILE=$cert; IDENTITY_DEV_HTTPS_KEY_FILE=$key; NODE_EXTRA_CA_CERTS=$cert
        IDENTITY_API_PROXY_TARGET=$api; IDENTITY_E2E_BASE_URL="https://127.0.0.1:$webPort"; IDENTITY_E2E_PORT="$webPort"
        IDENTITY_OPENAPI_URL="$api/openapi/v1.json"
    }
    foreach ($entry in $settings.GetEnumerator()) { Set-TestEnvironment $entry.Key $entry.Value }
    $process = Start-Process dotnet -ArgumentList @('src/Identity.Api/bin/Release/net10.0/Identity.Api.dll') -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $directory 'api.log') -RedirectStandardError (Join-Path $directory 'api.err')
    $ready = $false
    for ($i = 0; $i -lt 40; $i++) {
        try { if ((Invoke-RestMethod "$api/health/ready" -SkipCertificateCheck -TimeoutSec 2).status -eq 'ready') { $ready = $true; break } } catch { }
        Start-Sleep -Milliseconds 500
    }
    if (-not $ready) { throw 'HTTPS API fixture did not become ready.' }
    $password = 'Ephemeral smoke ' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(16))
    foreach ($account in @(@{ email='admin@identity.example'; role='IdentityAdministrator' }, @{ email='reader@identity.example'; role='IdentityReadOnly' })) {
        $null = Invoke-RestMethod "$api/api/v1/auth/register" -SkipCertificateCheck -Method Post -ContentType 'application/json' -Body (@{email=$account.email; password=$password; clientId='native'} | ConvertTo-Json -Compress)
        $messageId = $null
        for ($i = 0; $i -lt 40; $i++) {
            $messages = Invoke-RestMethod "http://127.0.0.1:$mailPort/api/v1/messages"
            $message = $messages.messages | Select-Object -First 1
            if ($message) { $messageId = $message.ID; break }
            Start-Sleep -Milliseconds 250
        }
        if (-not $messageId) { throw 'Verification email was not delivered.' }
        $mail = Invoke-RestMethod "http://127.0.0.1:$mailPort/api/v1/message/$messageId"
        $match = [regex]::Match($mail.Text, '#token=([A-Za-z0-9_-]{43})')
        if (-not $match.Success) { throw 'Verification link is missing.' }
        $null = Invoke-RestMethod "$api/api/v1/auth/email/verify" -SkipCertificateCheck -Method Post -ContentType 'application/json' -Body (@{token=$match.Groups[1].Value} | ConvertTo-Json -Compress)
        & dotnet src/Identity.AdminCli/bin/Release/net10.0/Identity.AdminCli.dll grant --email $account.email --role $account.role
        if ($LASTEXITCODE -ne 0) { throw 'CLI fixture bootstrap failed.' }
        $null = Invoke-RestMethod "http://127.0.0.1:$mailPort/api/v1/messages" -Method Delete
    }
    Set-TestEnvironment IDENTITY_E2E_PASSWORD $password
    Push-Location (Join-Path $root 'src/Identity.Admin.Web')
    try {
        & npm run api:check
        if ($LASTEXITCODE -ne 0) { throw 'Generated API contract drift detected.' }
        & npm run test:e2e
        if ($LASTEXITCODE -ne 0) { throw 'Browser smoke failed.' }
        foreach ($file in Get-ChildItem -LiteralPath 'dist' -Recurse -File) {
            $content = [IO.File]::ReadAllText($file.FullName)
            if ($content.Contains($password) -or $content.Contains($databasePassword) -or $content.Contains($pfxPassword)) { throw 'Fixture secret found in frontend bundle.' }
        }
    } finally { Pop-Location }
} finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    if ($started) { Invoke-TestCompose down --volumes --remove-orphans }
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
    # Delete only this invocation's generated files, after validating the absolute target.
    $resolved = [IO.Path]::GetFullPath($directory)
    $expectedParent = [IO.Path]::GetFullPath((Join-Path $root 'secrets'))
    if ([IO.Path]::GetDirectoryName($resolved) -ne $expectedParent -or [IO.Path]::GetFileName($resolved) -ne "admin-web-$suffix") { throw 'Invalid cleanup target.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
    Pop-Location
}
