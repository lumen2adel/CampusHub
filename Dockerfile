# syntax=docker/dockerfile:1

# ---- Build stage: full SDK, discarded after publishing ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project file alone: this layer is cached until dependencies change,
# so editing C# code does not re-download every NuGet package
COPY global.json ./
COPY CampusHub/CampusHub.csproj CampusHub/
RUN dotnet restore CampusHub/CampusHub.csproj

COPY CampusHub/ CampusHub/
RUN dotnet publish CampusHub/CampusHub.csproj --configuration Release --no-restore \
    --output /app/publish /p:UseAppHost=false

# ---- Runtime stage: ASP.NET runtime only (no SDK, no source code) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Uploaded files are written to /app/wwwroot/uploads; create it for the non-root user
RUN mkdir -p /app/wwwroot/uploads && chown -R $APP_UID /app/wwwroot/uploads

# The official image ships an unprivileged 'app' user; never run the API as root
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CampusHub.dll"]
