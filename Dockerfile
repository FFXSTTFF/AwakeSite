FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Awake.API/Awake.API.csproj",                      "src/Awake.API/"]
COPY ["src/Awake.Application/Awake.Application.csproj",       "src/Awake.Application/"]
COPY ["src/Awake.Domain/Awake.Domain.csproj",                 "src/Awake.Domain/"]
COPY ["src/Awake.Infrastructure/Awake.Infrastructure.csproj", "src/Awake.Infrastructure/"]

RUN dotnet restore "src/Awake.API/Awake.API.csproj"

COPY . .
RUN dotnet publish "src/Awake.API/Awake.API.csproj" -c Release -o /publish --no-restore

# Python playwright shares the same browser cache path (~/.cache/ms-playwright) as .NET Playwright
RUN apt-get update && apt-get install -y --no-install-recommends python3-pip \
    && rm -rf /var/lib/apt/lists/* \
    && pip3 install playwright==1.51.0 --break-system-packages \
    && playwright install --with-deps chromium

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    libnss3 libnspr4 libatk1.0-0 libatk-bridge2.0-0 libcups2 libdrm2 \
    libdbus-1-3 libxkbcommon0 libx11-6 libxcomposite1 libxdamage1 \
    libxext6 libxfixes3 libxrandr2 libgbm1 libpango-1.0-0 libcairo2 \
    libasound2t64 && rm -rf /var/lib/apt/lists/*

# Не root: кэш браузера уезжает из /root в путь, доступный appuser
# (Microsoft.Playwright читает PLAYWRIGHT_BROWSERS_PATH)
RUN useradd --create-home --uid 1001 appuser
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
COPY --from=build --chown=appuser /root/.cache/ms-playwright /ms-playwright
COPY --from=build /publish .

USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Awake.API.dll"]
