FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# copia solution e csproj primeiro
COPY wisemonitor_api.sln ./
COPY WiseMonitor.Api.csproj ./

RUN dotnet restore wisemonitor_api.sln

# copia resto do código
COPY . .

# publica explicitamente o projeto dentro da solution
RUN dotnet publish WiseMonitor.Api.csproj -c Release -o /app/publish --no-restore

# runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "WiseMonitor.Api.dll"]