param(
    [ValidateSet("Debug","Release")]
    [string]$Configuration = "Release",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"

dotnet restore .\EncoBudsTray.sln
dotnet build .\EncoBudsTray.sln -c $Configuration

if ($SelfContained) {
    dotnet publish .\EncoBudsTray.csproj `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true
}
