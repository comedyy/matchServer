set -v

git pull 
rm -rf ./bin/
dotnet build -c Release --framework net6.0

rm -rf d://serverPublish
mkdir d://serverPublish
cp -r bin d://serverPublish/bin
cp -r restart_release_6.0.sh d://serverPublish/restart_release_6.0.sh
cp -r __appconfig d://serverPublish/__appconfig
