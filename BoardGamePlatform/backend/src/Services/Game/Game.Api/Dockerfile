FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/Services/Game/Game.Api/Game.Api.csproj", "src/Services/Game/Game.Api/"]
COPY ["src/Services/Game/Game.Infrastructure/Game.Infrastructure.csproj", "src/Services/Game/Game.Infrastructure/"]
COPY ["src/Services/Game/Game.Application/Game.Application.csproj", "src/Services/Game/Game.Application/"]
COPY ["src/Services/Game/Game.Domain/Game.Domain.csproj", "src/Services/Game/Game.Domain/"]
COPY ["src/Games/UNO/UNO.csproj", "src/Games/UNO/"]
COPY ["src/GameEngine/GameEngine.Core/GameEngine.Core.csproj", "src/GameEngine/GameEngine.Core/"]
COPY ["src/BuildingBlocks/BuildingBlocks.Infrastructure/BuildingBlocks.Infrastructure.csproj", "src/BuildingBlocks/BuildingBlocks.Infrastructure/"]
COPY ["src/BuildingBlocks/BuildingBlocks.Application/BuildingBlocks.Application.csproj", "src/BuildingBlocks/BuildingBlocks.Application/"]
COPY ["src/BuildingBlocks/BuildingBlocks.Domain/BuildingBlocks.Domain.csproj", "src/BuildingBlocks/BuildingBlocks.Domain/"]
COPY ["src/BuildingBlocks/BuildingBlocks.Contracts/BuildingBlocks.Contracts.csproj", "src/BuildingBlocks/BuildingBlocks.Contracts/"]
RUN dotnet restore "src/Services/Game/Game.Api/Game.Api.csproj"

COPY src/ src/
RUN dotnet publish "src/Services/Game/Game.Api/Game.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80
ENTRYPOINT ["dotnet", "Game.Api.dll"]