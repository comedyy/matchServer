set -v

git pull 
rm -rf ./bin/
dotnet build --framework net6.0
./bin/Debug/net6.0/dotnetServer