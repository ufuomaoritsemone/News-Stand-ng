FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/TtsWorker/TtsWorker.csproj", "TtsWorker/"]
COPY ["src/NigerianNewsGrid.Client/NigerianNewsGrid.Client.csproj", "NigerianNewsGrid.Client/"]
RUN dotnet restore "TtsWorker/TtsWorker.csproj"

COPY src/ .
WORKDIR "/src/TtsWorker"
RUN dotnet publish "TtsWorker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "TtsWorker.dll"]
