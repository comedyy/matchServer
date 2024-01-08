set -v

git pull 
dotnet build -c Release
./bin/Release/net6.0/dotnetServer

if [[ ! -f "appConfig.txt" ]]; then
	echo "port,5000" > appConfig.txt
fi