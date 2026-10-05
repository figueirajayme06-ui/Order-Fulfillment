# Stage 1: Build frontend
FROM node:22-alpine AS frontend-build
WORKDIR /app/frontend
COPY src/OF.Frontend/package.json src/OF.Frontend/package-lock.json ./
RUN npm ci
COPY src/OF.Frontend/ ./
RUN npm run build

# Stage 2: Build backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src
COPY src/OF.sln ./
COPY src/OF.WebApp/OF.WebApp.csproj OF.WebApp/
COPY src/OF.Common/OF.Common.csproj OF.Common/
COPY src/OF.Data/OF.Data.csproj OF.Data/
COPY src/OF.UI.Shared/OF.UI.Shared.csproj OF.UI.Shared/
RUN dotnet restore OF.WebApp/OF.WebApp.csproj
COPY src/OF.WebApp/ OF.WebApp/
COPY src/OF.Common/ OF.Common/
COPY src/OF.Data/ OF.Data/
COPY src/OF.UI.Shared/ OF.UI.Shared/
RUN dotnet publish OF.WebApp/OF.WebApp.csproj -c Release -o /app/publish --no-restore

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=backend-build /app/publish .
COPY --from=frontend-build /app/frontend/dist ./wwwroot/
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "OF.WebApp.dll"]
