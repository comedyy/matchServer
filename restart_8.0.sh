set -v

git pull 
rm -rf ./bin/
dotnet build --framework net8.0
./bin/Debug/net8.0/dotnetServer