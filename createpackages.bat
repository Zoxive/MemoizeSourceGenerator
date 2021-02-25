del nupkgs\*.nupkg

dotnet restore

dotnet pack MemoizeSourceGenerator.Attribute\MemoizeSourceGenerator.Attribute.csproj --configuration release --output nupkgs
dotnet pack MemoizeSourceGenerator\MemoizeSourceGenerator.csproj --configuration release --output nupkgs

f:\Utilities\nuget.exe push nupkgs\*.nupkg -source "https://api.nuget.org/v3/index.json"