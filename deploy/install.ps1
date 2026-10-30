$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot 'src\FamilyCreator.Revit\FamilyCreator.Revit.csproj'
dotnet build $projectFile -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Build failed; installation stopped.' }
$buildDirectory = Join-Path $projectRoot 'src\FamilyCreator.Revit\bin\Release\net10.0-windows'
$addinDirectory = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2027'
$files = @(Get-ChildItem -LiteralPath $buildDirectory -File | Where-Object {
    $_.Extension -in @('.dll', '.json', '.pdb') -and $_.Name -notlike 'RevitAPI*'
} | Sort-Object Name)
$fingerprintInput = ($files | ForEach-Object { $_.Name + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash }) -join "`n"
$fingerprint = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($fingerprintInput))).Substring(0, 16)
$destination = Join-Path $addinDirectory "FamilyCreator\build-$fingerprint"
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($file in $files) {
    $target = Join-Path $destination $file.Name
    if (-not (Test-Path -LiteralPath $target)) {
        Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
    }
    if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
        throw "Installed file does not match build: $($file.Name)"
    }
}
$assemblyPath = [System.Security.SecurityElement]::Escape((Join-Path $destination 'FamilyCreator.Revit.dll'))
$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Family Creator</Name>
    <Assembly>$assemblyPath</Assembly>
    <AddInId>8F460328-0E88-447B-BBC0-A7898C8CB0EA</AddInId>
    <FullClassName>FamilyCreator.Revit.App</FullClassName>
    <VendorId>SGFC</VendorId>
    <VendorDescription>Family Creator local development</VendorDescription>
    <AllowLoadingIntoExistingSession>Yes</AllowLoadingIntoExistingSession>
  </AddIn>
</RevitAddIns>
"@
$manifestPath = Join-Path $addinDirectory 'FamilyCreator.addin'
$temporaryManifest = Join-Path $addinDirectory ('FamilyCreator-' + [Guid]::NewGuid() + '.tmp')
Set-Content -LiteralPath $temporaryManifest -Value $manifest -Encoding utf8
[System.IO.File]::Move($temporaryManifest, $manifestPath, $true)
Write-Output "Installed: $manifestPath"
Write-Output 'Open Revit 2027, then Add-Ins > Family Creator. An existing Revit session may need a restart.'
