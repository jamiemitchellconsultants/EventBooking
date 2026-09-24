# deploy/home-lab/web/Dockerfile (complete)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/EventBooking.Web/EventBooking.Web.csproj -c Release -o /out

FROM caddy:2.10-alpine AS runtime
COPY --from=build /out/wwwroot /srv
COPY deploy/home-lab/web/Caddyfile /etc/caddy/Caddyfile
USER 1000:1000
EXPOSE 8080
