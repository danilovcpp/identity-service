# Dot-sourced only by Test-AdminWebSmoke.ps1 -Production after isolated seed/OpenAPI checks.
# Uses release Compose unchanged; the override only connects disposable dependencies.
$productionEnvironmentFile = Join-Path $directory 'release-compose.env'
$productionOverride = Join-Path $directory 'release-override.yaml'
$productionProject = "$project-release"
function Invoke-ProductionCompose {
    & docker compose --env-file $productionEnvironmentFile --project-name $productionProject -f (Join-Path $root 'deploy/compose.production.yaml') -f $productionOverride @args
    if ($LASTEXITCODE -ne 0) { throw 'Production container fixture failed.' }
}
& docker build --tag identity-smoke-api:stage12 $root
if ($LASTEXITCODE -ne 0) { throw 'API release image build failed.' }
& docker build --tag identity-smoke-admin-web:stage12 (Join-Path $root 'src/Identity.Admin.Web')
if ($LASTEXITCODE -ne 0) { throw 'Frontend release image build failed.' }
Stop-Process -Id $process.Id -Force
$process = $null
# SMTP requires real STARTTLS. The fixture certificate is the only added trust anchor.
$mailOverride = Join-Path $directory 'mail-tls.yaml'
$certPath = $cert.Replace('\', '/')
$keyPath = $key.Replace('\', '/')
@"
services:
  mailpit:
    environment:
      MP_SMTP_TLS_CERT: /run/tls/cert.pem
      MP_SMTP_TLS_KEY: /run/tls/key.pem
      MP_SMTP_REQUIRE_STARTTLS: 'true'
    volumes:
      - '${certPath}:/run/tls/cert.pem:ro'
      - '${keyPath}:/run/tls/key.pem:ro'
"@ | Set-Content -LiteralPath $mailOverride
& docker compose --env-file $environmentFile --project-name $project -f (Join-Path $root 'compose.yaml') -f $mailOverride up --detach --wait mailpit
if ($LASTEXITCODE -ne 0) { throw 'STARTTLS Mailpit fixture failed.' }
$octet = Get-Random -Minimum 100 -Maximum 230
$subnetPrefix = "172.29.$octet"
$runtimeFile = Join-Path $directory 'runtime.env'
$hostConfig = Join-Path $directory 'identity-host.conf'
$networkConfig = Join-Path $directory 'admin-networks.conf'
(Get-Content -LiteralPath (Join-Path $root 'deploy/identity-host.conf.example') -Raw).Replace('identity.example', 'localhost 127.0.0.1') | Set-Content -LiteralPath $hostConfig
# Local fixture only: permit browser traffic, deny a more-specific probe container.
@('0.0.0.0/1 1;', '128.0.0.0/1 1;', "$subnetPrefix.5 0;") | Set-Content -LiteralPath $networkConfig
$runtime = @{}
foreach ($item in $settings.GetEnumerator()) {
    if ($item.Key -notmatch '^(IDENTITY_(?!_)|NODE_|ASPNETCORE_|Database__)') { $runtime[$item.Key] = [string]$item.Value }
}
$runtime['AllowedHosts'] = 'localhost;127.0.0.1'
$runtimeDatabasePassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
# Keep the schema/bootstrap owner out of the Production HTTP process.
$grants = @"
CREATE ROLE identity_app LOGIN PASSWORD '$runtimeDatabasePassword';
GRANT CONNECT ON DATABASE identity TO identity_app;
GRANT USAGE ON SCHEMA public TO identity_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE users, credentials, sessions,
  refresh_tokens, one_time_tokens, outbox_messages, security_audit_events TO identity_app;
GRANT SELECT ON TABLE identity_administrators TO identity_app;
GRANT SELECT, INSERT, UPDATE ON TABLE admin_sessions TO identity_app;
"@
$grantOutput = $grants | & docker compose --env-file $environmentFile --project-name $project -f (Join-Path $root 'compose.yaml') exec -T postgres psql -U identity -d identity -v ON_ERROR_STOP=1 2>&1
if ($LASTEXITCODE -ne 0) { throw 'Restricted runtime database grants failed.' }
$grantOutput = $null
$grants = $null
$runtime['Database__ConnectionString'] = "Host=postgres;Database=identity;Username=identity_app;Password=$runtimeDatabasePassword"
$runtime['Jwt__ActiveKeyId'] = 'release-smoke'
$runtime['Jwt__PrivateKeyPath'] = '/run/secrets/identity_jwt_key'
$runtime.Remove('OutboxEncryption__Keys__smoke')
$runtime['OutboxEncryption__ActiveKeyId'] = 'release-smoke'
$runtime['OutboxEncryption__Keys__release-smoke'] = '/run/secrets/identity_outbox_key'
$runtime['Email__Smtp__Host'] = 'mailpit'
$runtime['Email__Smtp__Port'] = '1025'
$runtime['Email__Smtp__Tls'] = 'StartTls'
$runtime['SSL_CERT_FILE'] = '/run/tls/fixture-ca.pem'
[IO.File]::WriteAllLines($runtimeFile, @($runtime.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }))
[IO.File]::WriteAllLines($productionEnvironmentFile, @(
    'IDENTITY_IMAGE=identity-smoke-api:stage12', 'IDENTITY_ADMIN_WEB_IMAGE=identity-smoke-admin-web:stage12',
    "IDENTITY_RUNTIME_ENV_FILE=$($runtimeFile.Replace('\','/'))", 'IDENTITY_HTTPS_BIND_IP=127.0.0.1', "IDENTITY_HTTPS_PORT=$webPort",
    "IDENTITY_EDGE_SUBNET=$subnetPrefix.0/28", "IDENTITY_PROXY_IP=$subnetPrefix.2", "IDENTITY_API_IP=$subnetPrefix.3",
    "IDENTITY_HOST_CONFIG=$($hostConfig.Replace('\','/'))", "ADMIN_NETWORKS_FILE=$($networkConfig.Replace('\','/'))",
    "ADMIN_KEY_DIRECTORY=$($keyDirectory.Replace('\','/'))", "JWT_KEY_FILE=$($jwtKey.Replace('\','/'))", "OUTBOX_KEY_FILE=$($outbox.Replace('\','/'))",
    "TLS_CERTIFICATE_FILE=$certPath", "TLS_KEY_FILE=$keyPath"
))
$fixtureUserSetting = ''
if ($IsLinux) {
    # The disposable bind directory belongs to the CI runner; preserve its permissions.
    $runnerUid = (& id -u).Trim()
    $runnerGid = (& id -g).Trim()
    if ($runnerUid -notmatch '^[1-9][0-9]*$' -or $runnerGid -notmatch '^\d+$') { throw 'Run browser fixtures as a non-root operator.' }
    $fixtureUserSetting = "    user: '${runnerUid}:${runnerGid}'"
}
@"
services:
  api:
$fixtureUserSetting
    networks:
      fixture: {}
    volumes:
      - '${certPath}:/run/tls/fixture-ca.pem:ro'
networks:
  fixture:
    external: true
    name: ${project}_default
"@ | Set-Content -LiteralPath $productionOverride
Invoke-ProductionCompose config --quiet
$productionStarted = $true
try { Invoke-ProductionCompose up --detach --wait --wait-timeout 90 }
catch {
    & docker compose --env-file $productionEnvironmentFile --project-name $productionProject -f (Join-Path $root 'deploy/compose.production.yaml') -f $productionOverride logs --no-log-prefix api identity-admin-web 2>&1 | ForEach-Object {
        $line = [string]$_
        if ($line.Contains($password) -or $line.Contains($databasePassword) -or $line.Contains($runtimeDatabasePassword) -or $line.Contains($pfxPassword)) { Write-Output '[Suppressed fixture credentials]' }
        else { Write-Output $line }
    }
    throw
}
Invoke-ProductionCompose exec -T identity-admin-web nginx -t
$composeArguments = @('--env-file', $productionEnvironmentFile, '--project-name', $productionProject,
    '-f', (Join-Path $root 'deploy/compose.production.yaml'), '-f', $productionOverride)
$apiContainer = (& docker compose @composeArguments ps -q api).Trim()
$webContainer = (& docker compose @composeArguments ps -q identity-admin-web).Trim()
foreach ($container in @($apiContainer, $webContainer)) {
    $inspection = (& docker inspect $container | ConvertFrom-Json)[0]
    if (-not $inspection.HostConfig.ReadonlyRootfs -or $inspection.Config.User -match '^(?:$|0(?::|$)|root(?::|$))') { throw 'Release container hardening is missing.' }
}
$apiInspection = (& docker inspect $apiContainer | ConvertFrom-Json)[0]
if (@($apiInspection.HostConfig.PortBindings.PSObject.Properties).Count -ne 0) { throw 'API port must not be published.' }
# A denied source cannot bypass network restrictions through headers, casing or encoding.
$probe = @'
import https from 'node:https';
import http from 'node:http';
const base = process.env.PROBE_URL;
function status(path, origin = base) {
  return new Promise((resolve, reject) => {
    const transport = origin.startsWith('https:') ? https : http;
    const request = transport.get(origin + path, { rejectUnauthorized: false, headers: { Host: 'localhost', 'X-Forwarded-For': '10.8.0.1', 'X-Forwarded-Proto': 'https' } }, response => {
      response.resume();
      resolve(response.statusCode);
    });
    request.on('error', reject);
  });
}
for (const path of ['/admin/login', '/admin/users', '/api/v1/admin/auth/csrf', '/API/v1/ADMIN/auth/csrf', '/api/v1/%61dmin/auth/csrf', '/api/v1/admin%2fauth/csrf']) {
  const code = await status(path);
  if (code !== 403) throw new Error(`Admin network restriction failed: ${path} returned ${code}`);
}
for (const path of ['/account/activate', '/account/reset-password']) {
  const code = await status(path);
  if (code !== 200) throw new Error(`Public account route returned ${code}`);
}
console.log('Denied-source admin ACL and public account routes passed.');
if (await status('/api/v1/admin/auth/csrf', process.env.API_PROBE_URL) !== 400) throw new Error('An untrusted direct API peer supplied forwarded HTTPS');
console.log('Direct API rejects spoofed HTTPS from an untrusted peer.');
'@
& docker run --rm --network "${productionProject}_edge" --ip "$subnetPrefix.5" -e "PROBE_URL=https://$subnetPrefix.2`:8443" -e "API_PROBE_URL=http://$subnetPrefix.3`:8080" node:22-alpine@sha256:0a7108bf6c7bf5de370ffb1a3ed6be93d405b43ff159f681a8d18c0e2bc2e402 node --input-type=module -e $probe
if ($LASTEXITCODE -ne 0) { throw 'Denied-source security probe failed.' }
$webOrigin = "https://127.0.0.1:$webPort"
$restartSession = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$csrfBefore = Invoke-RestMethod "$webOrigin/api/v1/admin/auth/csrf" -SkipCertificateCheck -WebSession $restartSession
$null = Invoke-RestMethod "$webOrigin/api/v1/admin/auth/login" -SkipCertificateCheck -WebSession $restartSession -Method Post -ContentType 'application/json' -Headers @{ 'X-CSRF-TOKEN' = $csrfBefore.token } -Body (@{ email='admin@identity.example'; password=$password } | ConvertTo-Json -Compress)
Invoke-ProductionCompose restart api
Invoke-ProductionCompose up --detach --wait --wait-timeout 90 api
$retainedProfile = Invoke-RestMethod "$webOrigin/api/v1/admin/auth/me" -SkipCertificateCheck -WebSession $restartSession
if ($retainedProfile.role -ne 'IdentityAdministrator') { throw 'Admin key/session persistence failed across API restart.' }
$csrfAfter = Invoke-RestMethod "$webOrigin/api/v1/admin/auth/csrf" -SkipCertificateCheck -WebSession $restartSession
$null = Invoke-RestMethod "$webOrigin/api/v1/admin/auth/logout" -SkipCertificateCheck -WebSession $restartSession -Method Post -Headers @{ 'X-CSRF-TOKEN' = $csrfAfter.token }
Write-Output 'Admin cookie and durable key ring survived Production API restart.'
Set-TestEnvironment IDENTITY_E2E_PRODUCTION 'true'
# Playwright uses the built static image through TLS; no Vite server is started.
