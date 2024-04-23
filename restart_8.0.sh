set -v

git pull 
dotnet build -c Release
./bin/Release/net8.0/dotnetServer