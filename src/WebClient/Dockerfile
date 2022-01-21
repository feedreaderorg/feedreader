FROM nginx:alpine
COPY bin/Release/net6.0/publish/wwwroot /app/wwwroot
COPY feedreader.webclient.conf.template /etc/nginx/templates/
COPY nginx.conf /etc/nginx/