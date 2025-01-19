FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-noble AS build
ARG TARGETARCH

WORKDIR /src
COPY . .

WORKDIR /src/src/WebClient
RUN dotnet publish "WebClient.csproj" -c Release -a $TARGETARCH -o /app/publish

FROM nginx:alpine AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /src/src/WebClient/feedreader.webclient.conf.template /etc/nginx/templates/
COPY --from=build /src/src/WebClient/nginx.conf /etc/nginx/