FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/core/sdk:3.1.101-buster as build

WORKDIR /build

COPY ["Directory.Build.props", "GlobalSuppressions.cs", "./"]
COPY ["Sensors.Shared/Sensors.Shared.csproj", "Sensors.Shared/"]
COPY ["ReadOneWire/ReadOneWire.csproj", "ReadOneWire/"]

WORKDIR /build/ReadOneWire
RUN dotnet restore

WORKDIR /build
COPY ["Sensors.Shared/", "Sensors.Shared/"]
COPY ["ReadOneWire/", "ReadOneWire/"]
WORKDIR /build/ReadOneWire

RUN dotnet build -c Release

FROM build as publish
RUN dotnet publish -c Release --no-build -o /publish

FROM mcr.microsoft.com/dotnet/core/runtime:3.1.1-buster-slim-arm32v7 as final
WORKDIR /app
COPY --from=publish /publish .

ENTRYPOINT ["dotnet", "ReadOneWire.dll", "--ServerName", "server.home", "--SensorName"]
