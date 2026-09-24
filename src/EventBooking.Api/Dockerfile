# src/EventBooking.Api/Dockerfile (complete)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/EventBooking.Api/EventBooking.Api.csproj -c Release -o /out --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "EventBooking.Api.dll"]
