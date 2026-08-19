FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/AdminDashboard/AdminDashboard.csproj", "AdminDashboard/"]
COPY ["src/NigerianNewsGrid.Client/NigerianNewsGrid.Client.csproj", "NigerianNewsGrid.Client/"]
RUN dotnet restore "AdminDashboard/AdminDashboard.csproj"

COPY src/ .
WORKDIR "/src/AdminDashboard"
RUN dotnet publish "AdminDashboard.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "AdminDashboard.dll"]
