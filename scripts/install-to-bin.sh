dotnet publish ../Sunpack.csproj -c Release --self-contained
cd "../bin/Release/net8.0/linux-x64/publish"
sudo cp ./sunpack /usr/local/bin