$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot 'src\FamilyCreator.Revit\FamilyCreator.Revit.csproj'
dotnet build $projectFile -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Build failed; installation stopped.' }
$buildDirectory = Join-Path $projectRoot 'src\FamilyCreator.Revit\bin\Release\net10.0-windows'
$addinDirectory = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2027'
$destination = Join-Path $addinDirectory 'FamilyCreator'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($name in @('FamilyCreator.Revit.dll', 'FamilyCreator.Revit.deps.json', 'FamilyCreator.Revit.pdb')) {
    Copy-Item -LiteralPath (Join-Path $buildDirectory $name) -Destination $destination -Force
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
Set-Content -LiteralPath $manifestPath -Value $manifest -Encoding utf8
Write-Output "Installed: $manifestPath"
Write-Output 'Open Revit 2027, then Add-Ins > Family Creator. An existing Revit session may need a restart.'
