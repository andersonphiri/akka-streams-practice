@echo off
dotnet publish ../src/AkkaStreamz.WebApi.App/AkkaStreamz.WebApi.App.csproj --os linux --arch x64 -c Release -p:PublishProfile=DefaultContainer