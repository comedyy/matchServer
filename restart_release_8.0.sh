set -v

git pull 
dotnet build -c Release
./bin/Debug/net8.0/dotnetServer