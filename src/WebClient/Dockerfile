FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-noble AS build
ARG TARGETARCH

WORKDIR /src
COPY . .
RUN dotnet restore -a $TARGETARCH

WORKDIR /src/src/WebClient
RUN dotnet publish "WebClient.csproj" -c Release -a $TARGETARCH --no-restore -o /app/publish

FROM nginx:alpine AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /src/src/WebClient/feedreader.webclient.conf.template /etc/nginx/templates/
COPY --from=build /src/src/WebClient/nginx.conf /etc/nginx/