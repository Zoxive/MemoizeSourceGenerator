del nupkgs\*.nupkg

dotnet restore

dotnet pack Zoxive.MemoizeSourceGenerator.Attribute\Zoxive.MemoizeSourceGenerator.Attribute.csproj --configuration release --output nupkgs
dotnet pack Zoxive.MemoizeSourceGenerator\Zoxive.MemoizeSourceGenerator.csproj --configuration release --output nupkgs

:: f:\Utilities\nuget.exe push nupkgs\*.nupkg -source "https://api.nuget.org/v3/index.json"