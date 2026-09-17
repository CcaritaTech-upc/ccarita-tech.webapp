# IoBuild Monolith — Dockerfile
# Build context: ./backend
# Produces single ASP.NET Core 9 image (iobuild-api) without YARP/RabbitMQ/Redis.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files for restore caching
COPY IoBuild.sln ./
COPY src/IoBuild.Api/IoBuild.Api.csproj src/IoBuild.Api/
COPY src/IoBuild.Modules.Abstractions/IoBuild.Modules.Abstractions.csproj src/IoBuild.Modules.Abstractions/
COPY tests/Architecture/IoBuild.Architecture.Tests.csproj tests/Architecture/
COPY tests/Contract/IoBuild.Contract.Tests.csproj tests/Contract/
COPY tests/Modules/IoBuild.Modules.Tests.csproj tests/Modules/
COPY tests/Integration/IoBuild.Integration.Tests.csproj tests/Integration/
COPY tests/TestKit/IoBuild.TestKit.csproj tests/TestKit/
COPY tools/IoBuild.Cutover/IoBuild.Cutover.csproj tools/IoBuild.Cutover/
COPY tools/IoBuild.LegacyImporter/IoBuild.LegacyImporter.csproj tools/IoBuild.LegacyImporter/

RUN dotnet restore IoBuild.sln

# Copy everything and publish
COPY . .
RUN dotnet publish src/IoBuild.Api/IoBuild.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "IoBuild.Api.dll"]
