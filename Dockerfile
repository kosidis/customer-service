# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached until a project file changes.
COPY src/CustomerService.Api/CustomerService.Api.csproj src/CustomerService.Api/
RUN dotnet restore src/CustomerService.Api/CustomerService.Api.csproj

COPY src/ src/
RUN dotnet publish src/CustomerService.Api/CustomerService.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app/publish .

USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "CustomerService.Api.dll"]
