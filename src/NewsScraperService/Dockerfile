FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/NewsScraperService/NewsScraperService.csproj", "NewsScraperService/"]
RUN dotnet restore "NewsScraperService/NewsScraperService.csproj"

COPY src/ .
WORKDIR "/src/NewsScraperService"
RUN dotnet publish "NewsScraperService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "NewsScraperService.dll"]
