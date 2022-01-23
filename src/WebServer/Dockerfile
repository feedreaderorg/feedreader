FROM mcr.microsoft.com/dotnet/aspnet:6.0-focal
WORKDIR /app
COPY bin/Release/net6.0/publish /app
ENTRYPOINT ["dotnet", "FeedReader.WebServer.dll"]