#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["OF.Data.ProductItems/OF.Data.ProductItems.csproj", "OF.Data.ProductItems/"]
RUN dotnet restore "OF.Data.ProductItems/OF.Data.ProductItems.csproj"
COPY . .
WORKDIR "/src/OF.Data.ProductItems"
RUN dotnet build "OF.Data.ProductItems.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OF.Data.ProductItems.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OF.Data.ProductItems.dll"]