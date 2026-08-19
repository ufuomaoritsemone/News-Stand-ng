FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/NewsApi/NewsApi.csproj", "NewsApi/"]
COPY ["src/NigerianNewsGrid.Client/NigerianNewsGrid.Client.csproj", "NigerianNewsGrid.Client/"]
RUN dotnet restore "NewsApi/NewsApi.csproj"

COPY src/ .
WORKDIR "/src/NewsApi"
RUN dotnet publish "NewsApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "NewsApi.dll"]
