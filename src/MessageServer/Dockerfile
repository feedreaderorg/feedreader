FROM mcr.microsoft.com/dotnet/aspnet:6.0-focal AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0-focal AS build
WORKDIR /src
COPY . .
WORKDIR /src/src/MessageServer
RUN dotnet build -c Release -o /app/build

FROM build AS publish
WORKDIR /src/src/MessageServer
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FeedReader.MessageServer.dll"]