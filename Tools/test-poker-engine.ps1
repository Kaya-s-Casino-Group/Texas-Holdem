[CmdletBinding()]
param([string[]]$Fixtures = @())
$script = Join-Path $PSScriptRoot '../External/BattleToolkit/Tools/test-poker-engine.ps1'
if (!(Test-Path $script)) { throw 'Run git submodule update --init --recursive first.' }
& $script -Fixtures $Fixtures
exit $LASTEXITCODE
