# Windows counterpart to test-poker-engine.sh; no Unity launch or package download.
param(
    [string]$Compiler = $env:POKER_CSC,
    [string]$Runtime = $env:POKER_MONO,
    [string]$NUnit = $env:POKER_NUNIT,
    [string[]]$Fixtures = @(),
    [string]$OutputDirectory = (Join-Path ([IO.Path]::GetTempPath()) ('poker-engine-tests-' + [Guid]::NewGuid().ToString('N')))
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (!$Compiler) { throw 'Set -Compiler or POKER_CSC to a C# compiler supporting C# 7.2.' }
if (!$Runtime) { throw 'Set -Runtime or POKER_MONO to mono.' }
if (!$NUnit) { $NUnit = Join-Path $repo 'Library/PackageCache/com.unity.ext.nunit@2.0.3/net40/unity-custom/nunit.framework.dll' }
if (!(Test-Path -LiteralPath $NUnit)) { throw 'Set -NUnit or POKER_NUNIT to an existing NUnit 3 framework DLL.' }
$NUnit = (Resolve-Path -LiteralPath $NUnit).Path
$framework = [IO.Path]::GetFullPath((Join-Path (Split-Path $Runtime -Parent) '../lib/mono/4.5'))
$frameworkReferences = @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll', 'System.Runtime.Serialization.dll') | ForEach-Object { '-r:' + (Join-Path $framework $_) }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$modules = @('Core', 'Abilities', 'Combat', 'Scoring', 'Holdem', 'Draw', 'Runs', 'Progression', 'Encounters', 'Composition')
$dependencies = @{
    Core=@(); Abilities=@('Core'); Scoring=@('Core'); Combat=@('Core','Abilities'); Holdem=@('Core','Abilities')
    Draw=@('Core','Abilities','Scoring'); Runs=@('Core','Abilities','Scoring'); Progression=@('Core'); Encounters=@('Core')
    Composition=@('Core','Abilities','Combat','Scoring','Holdem','Draw','Runs','Progression','Encounters')
}
$allReferences = @()
foreach ($module in $modules) {
    $references = @($frameworkReferences) + @($dependencies[$module] | ForEach-Object { '-r:' + (Join-Path $OutputDirectory "PokerEngine.$_.dll") })
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $repo "Assets/PokerEngine/Runtime/$module") -Filter '*.cs' -Recurse | ForEach-Object FullName)
    $dll = Join-Path $OutputDirectory "PokerEngine.$module.dll"
    & $Compiler -nologo -noconfig -nostdlib -langversion:7.2 -warnaserror -target:library @references "-out:$dll" @sources
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $module" }
    $allReferences += "-r:$dll"
}
Copy-Item -LiteralPath $NUnit -Destination (Join-Path $OutputDirectory 'nunit.framework.dll') -Force
$tests = @(Get-ChildItem -LiteralPath (Join-Path $repo 'Assets/PokerEngine/Tests/Editor') -Filter '*Tests.cs' | Where-Object Name -ne 'PackagingTests.cs' | ForEach-Object FullName)
$exe = Join-Path $OutputDirectory 'PokerEngine.Tests.exe'
& $Compiler -nologo -noconfig -nostdlib -langversion:7.2 -warnaserror @frameworkReferences @allReferences "-r:$NUnit" "-out:$exe" @tests (Join-Path $repo 'Tools/PokerEngineTestRunner.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& $Runtime $exe @Fixtures
if ($LASTEXITCODE -ne 0) { throw 'Offline tests failed.' }
Write-Output "Offline binaries: $OutputDirectory"
