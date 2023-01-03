FROM mcr.microsoft.com/dotnet/runtime:7.0-jammy
WORKDIR /app
RUN apt update && apt install curl -y
COPY bin/Release/net7.0/publish /app
ENTRYPOINT ["dotnet", "FeedReader.TaskServer.dll"]