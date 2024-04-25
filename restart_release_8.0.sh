set -v

git pull 
rm -rf ./bin/
dotnet build -c Release --framework net8.0
./bin/Release/net8.0/dotnetServer