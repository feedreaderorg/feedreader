FROM mcr.microsoft.com/dotnet/runtime:5.0-focal AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0-focal AS build
WORKDIR /src
COPY . .
WORKDIR /src/src/TaskServer
RUN dotnet build -c Release -o /app/build

FROM build AS publish
WORKDIR /src/src/TaskServer
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FeedReader.TaskServer.dll"]