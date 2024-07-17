# dotnet publish ../Sunpack.csproj -c Release --self-contained
cd "../bin/Release/net8.0/linux-x64/publish"
mkdir ~/.local/sunpack
mkdir ~/.local/sunpack/bin
mkdir ~/.local/sunpack/lib
sudo cp ./sunpack ~/.local/sunpack/bin
sudo cp ./liblua54.so ~/.local/sunpack/lib

sudo ln -s ~/.local/sunpack/bin/sunpack /usr/local/bin/sunpack