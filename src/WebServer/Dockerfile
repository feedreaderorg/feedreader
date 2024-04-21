# See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:7.0-jammy AS build
ARG TARGETARCH

WORKDIR /src
COPY . .
RUN dotnet restore -a $TARGETARCH

WORKDIR /src/src/WebServer
RUN dotnet publish "WebServer.csproj" -c Release -a $TARGETARCH --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:7.0-jammy-arm64v8 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FeedReader.WebServer.dll"]