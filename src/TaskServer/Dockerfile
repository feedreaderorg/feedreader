FROM mcr.microsoft.com/dotnet/runtime:6.0-focal
WORKDIR /app
COPY bin/Release/net6.0/publish /app
ENTRYPOINT ["dotnet", "FeedReader.TaskServer.dll"]