FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ApiGateway/ApiGateway.csproj", "ApiGateway/"]
RUN dotnet restore 'ApiGateway/ApiGateway.csproj'

COPY ["ApiGateway", "ApiGateway/"]
RUN dotnet publish 'ApiGateway/ApiGateway.csproj' -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
EXPOSE 8080
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT [ "dotnet", "ApiGateway.dll" ]
