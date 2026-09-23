FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/EventBooking.Web/EventBooking.Web.csproj -c Release -o /app

FROM nginxinc/nginx-unprivileged:alpine AS runtime
COPY --from=build /app/wwwroot /usr/share/nginx/html
COPY src/EventBooking.Web/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 8080
