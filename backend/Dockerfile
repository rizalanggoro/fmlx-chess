FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Chess.Api/Chess.Api.csproj Chess.Api/
RUN dotnet restore Chess.Api/Chess.Api.csproj
COPY Chess.Api/ Chess.Api/
RUN dotnet publish Chess.Api/Chess.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
# Container listen HTTP saja (TLS di-terminate di luar / reverse proxy)
ENV ASPNETCORE_URLS=http://+:5004
EXPOSE 5004
USER $APP_UID
ENTRYPOINT ["dotnet", "Chess.Api.dll"]
