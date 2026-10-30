param([switch]$Interactive)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
dotnet build $PSScriptRoot -c Release -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Native smoke build failed.' }
$outputDirectory = Join-Path $root 'outputs\native-smoke'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$report = Join-Path $outputDirectory ('report-' + [Guid]::NewGuid().ToString('N') + '.json')
$assembly = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'bin\Release\net10.0-windows\FamilyCreator.Revit.NativeSmoke.dll'))
$manifest = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2027\FamilyCreator.NativeSmoke.addin'
if (Test-Path -LiteralPath $manifest) { throw 'A native smoke manifest already exists. Finish that test first.' }
@"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns><AddIn Type="Application"><Name>Family Creator Native Smoke</Name><Assembly>$assembly</Assembly><AddInId>754075BA-B041-4D79-971C-2CC60555F33C</AddInId><FullClassName>FamilyCreator.NativeSmoke.App</FullClassName><VendorId>SGFC</VendorId><AllowLoadingIntoExistingSession>No</AllowLoadingIntoExistingSession></AddIn></RevitAddIns>
"@ | Set-Content -LiteralPath $manifest -Encoding utf8
$windowStyle = if ($Interactive) { 'Normal' } else { 'Hidden' }
$process = Start-Process -FilePath 'C:\Program Files\Autodesk\Revit 2027\Revit.exe' -ArgumentList '/nosplash' -WindowStyle $windowStyle -Environment @{ FAMILYCREATOR_NATIVE_TEST_REPORT = $report } -PassThru
$state = @{ ProcessId = $process.Id; Report = $report; Manifest = $manifest }
$state | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputDirectory 'session.json')
$state | ConvertTo-Json
# Read the report, then close only this disposable Revit process and remove this exact manifest.
# This launcher never closes the user's existing Revit session.
