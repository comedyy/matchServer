set -v

git pull 
dotnet build -c Release
./bin/Release/net6.0/dotnetServer