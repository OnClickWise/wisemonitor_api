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

# =====================================================
# Instala o Chromium e as bibliotecas necessárias para ele
# rodar, usado pelo PuppeteerSharp na geração de PDFs de
# relatório (ver Services/Reports/ReportPdfService.cs).
# Sem essas bibliotecas o container retorna erros como:
# "libglib-2.0.so.0: cannot open shared object file"
# =====================================================
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        chromium \
        fonts-liberation \
        libasound2 \
        libatk-bridge2.0-0 \
        libatk1.0-0 \
        libc6 \
        libcairo2 \
        libcups2 \
        libdbus-1-3 \
        libexpat1 \
        libfontconfig1 \
        libgbm1 \
        libgcc-s1 \
        libglib2.0-0 \
        libgtk-3-0 \
        libnspr4 \
        libnss3 \
        libpango-1.0-0 \
        libpangocairo-1.0-0 \
        libstdc++6 \
        libx11-6 \
        libx11-xcb1 \
        libxcb1 \
        libxcomposite1 \
        libxdamage1 \
        libxext6 \
        libxfixes3 \
        libxrandr2 \
        libxrender1 \
        libxshmfence1 \
        xdg-utils \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

# Caminho do Chromium instalado no container Linux
ENV CHROMIUM_EXECUTABLE_PATH=/usr/bin/chromium

EXPOSE 8080

ENTRYPOINT ["dotnet", "WiseMonitor.Api.dll"]