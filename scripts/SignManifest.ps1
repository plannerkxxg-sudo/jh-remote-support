param([Parameter(Mandatory=$true)][string]$PayloadPath, [Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
if (!$env:JH_UPDATE_SIGNING_KEY_B64) { throw 'Set the publisher-only GitHub secret JH_UPDATE_SIGNING_KEY_B64.' }
$xml = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($env:JH_UPDATE_SIGNING_KEY_B64))
$provider = New-Object System.Security.Cryptography.CspParameters -ArgumentList 24
$rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider -ArgumentList @(3072, $provider)
$rsa.PersistKeyInCsp = $false
try {
  $rsa.FromXmlString($xml)
  [xml]$expected = Get-Content -Raw (Join-Path $PSScriptRoot '../updates/publisher-public.xml')
  [xml]$actual = $rsa.ToXmlString($false)
  if ($actual.RSAKeyValue.Modulus -ne $expected.RSAKeyValue.Modulus -or $actual.RSAKeyValue.Exponent -ne $expected.RSAKeyValue.Exponent) {
    throw 'Signing key differs from the public key trusted by employee clients.'
  }
  $payload = [IO.File]::ReadAllBytes((Resolve-Path $PayloadPath))
  $signature = $rsa.SignData($payload, 'SHA256')
  $envelope = @{ payload = [Convert]::ToBase64String($payload); signature = [Convert]::ToBase64String($signature) } | ConvertTo-Json -Compress
  [IO.File]::WriteAllText($OutputPath, $envelope, (New-Object Text.UTF8Encoding($false)))
} finally { $rsa.Dispose(); $xml = $null }
