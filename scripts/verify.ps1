$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
dotnet test "$repoRoot\tests\LightControls.Tests\LightControls.Tests.csproj"
dotnet build "$repoRoot\src\LightControls.Desktop\LightControls.Desktop.csproj"
