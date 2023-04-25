FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["FolderWatcherService.csproj", "."]
RUN dotnet restore "./FolderWatcherService.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "FolderWatcherService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FolderWatcherService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
ENTRYPOINT ["dotnet", "FolderWatcherService.dll"]