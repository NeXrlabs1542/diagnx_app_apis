# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (cached layer), then build.
COPY src/DiagnX.Api/DiagnX.Api.csproj src/DiagnX.Api/
RUN dotnet restore src/DiagnX.Api/DiagnX.Api.csproj

COPY src/DiagnX.Api/ src/DiagnX.Api/
RUN dotnet publish src/DiagnX.Api/DiagnX.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    PORT=8080
COPY --from=build /app/publish .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "DiagnX.Api.dll"]
