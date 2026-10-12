param([Parameter(Mandatory=$true)][string]$KeyDirectory)
$ErrorActionPreference = 'Stop'
# Build-time only. Never places the key or certificate into any Windows certificate store.
$keyPath = [IO.Path]::GetFullPath($KeyDirectory)
if (Test-Path -LiteralPath $keyPath) { throw 'Choose a new private key directory.' }
New-Item -ItemType Directory -Path $keyPath | Out-Null
$rsa = [Security.Cryptography.RSA]::Create(3072)
try {
    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=FolderAlias Local', $rsa, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false, $false, 0, $true))
    $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new([Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature, $true))
    $usages = [Security.Cryptography.OidCollection]::new()
    $null = $usages.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3'))
    $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($usages, $true))
    $certificate = $request.CreateSelfSigned([DateTimeOffset]::Now.AddDays(-1), [DateTimeOffset]::Now.AddYears(3))
    try {
        $random = [byte[]]::new(32)
        [Security.Cryptography.RandomNumberGenerator]::Fill($random)
        $password = [Convert]::ToBase64String($random)
        [IO.File]::WriteAllBytes((Join-Path $keyPath 'publisher.pfx'), $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $password))
        ConvertTo-SecureString -String $password -AsPlainText -Force | Export-Clixml -LiteralPath (Join-Path $keyPath 'password.dpapi.xml')
        [IO.File]::WriteAllBytes((Join-Path $keyPath 'Publisher.cer'), $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert))
        Write-Output ('Created dedicated public certificate: ' + $certificate.Thumbprint)
        Write-Output ('Expires: ' + $certificate.NotAfter.ToString('yyyy-MM-dd'))
    } finally { if ($certificate) { $certificate.Dispose() } }
} finally { $rsa.Dispose() }
