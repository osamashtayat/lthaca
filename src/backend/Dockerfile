# syntax=docker/dockerfile:1
# Build context: the repository root (docker compose sets it).
#   target api         the API (ASP.NET Core runtime, non-root, HTTP on 8080 behind the web proxy)
#   target migrations  a one-shot EF Core migrations bundle; reads ConnectionStrings__Ithaca

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY .config/dotnet-tools.json .config/
COPY src/backend/Ithaca.Domain/Ithaca.Domain.csproj src/backend/Ithaca.Domain/
COPY src/backend/Ithaca.Application/Ithaca.Application.csproj src/backend/Ithaca.Application/
COPY src/backend/Ithaca.Infrastructure/Ithaca.Infrastructure.csproj src/backend/Ithaca.Infrastructure/
COPY src/backend/Ithaca.Api/Ithaca.Api.csproj src/backend/Ithaca.Api/
RUN dotnet restore src/backend/Ithaca.Api/Ithaca.Api.csproj && dotnet tool restore
COPY src/backend/ src/backend/
# Versioned prompt files ship with the API (linked from Ithaca.Api.csproj).
COPY prompts/ prompts/
RUN dotnet publish src/backend/Ithaca.Api/Ithaca.Api.csproj -c Release -o /out/api --no-restore -p:UseAppHost=false

FROM build AS migrations-build
RUN dotnet ef migrations bundle \
      --project src/backend/Ithaca.Infrastructure --startup-project src/backend/Ithaca.Api \
      --configuration Release --output /out/efbundle --force

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrations
WORKDIR /app
COPY --from=migrations-build /out/efbundle ./efbundle
USER $APP_UID
ENTRYPOINT ["./efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /out/api ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Ithaca.Api.dll"]
