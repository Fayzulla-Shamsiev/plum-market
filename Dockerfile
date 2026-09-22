# Demo image: builds the Vue app into the API's wwwroot, then runs the .NET API that serves everything.
FROM node:22-alpine AS web
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src/backend/PlumMarket.Api
COPY backend/PlumMarket.Api/PlumMarket.Api.csproj ./
RUN dotnet restore
COPY backend/PlumMarket.Api/ ./
COPY --from=web /src/backend/PlumMarket.Api/wwwroot ./wwwroot
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /app ./
# Render (and most hosts) inject PORT; default to 8080 locally.
ENV PORT=8080
CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT} dotnet PlumMarket.Api.dll"]
