# Backend: chess API (.NET 8). Build from repo root:
#   docker build -f backend/Dockerfile -t rizalanggoro/fmlx-chess:backend .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/Chess.Api/Chess.Api.csproj Chess.Api/
RUN dotnet restore Chess.Api/Chess.Api.csproj
COPY backend/Chess.Api/ Chess.Api/
RUN dotnet publish Chess.Api/Chess.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
# Port default image .NET 8 (ASPNETCORE_HTTP_PORTS=8080), tanpa override
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Chess.Api.dll"]
