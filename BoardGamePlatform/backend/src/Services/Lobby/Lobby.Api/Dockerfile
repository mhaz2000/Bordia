FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/Services/Lobby/Lobby.Api/Lobby.Api.csproj", "src/Services/Lobby/Lobby.Api/"]
COPY ["src/Services/Lobby/Lobby.Infrastructure/Lobby.Infrastructure.csproj", "src/Services/Lobby/Lobby.Infrastructure/"]
COPY ["src/Services/Lobby/Lobby.Application/Lobby.Application.csproj", "src/Services/Lobby/Lobby.Application/"]
COPY ["src/Services/Lobby/Lobby.Domain/Lobby.Domain.csproj", "src/Services/Lobby/Lobby.Domain/"]
COPY ["src/BuildingBlocks/BuildingBlocks.Infrastructure/BuildingBlocks.Infrastructure.csproj", "src/BuildingBlocks/BuildingBlocks.Infrastructure/"]
COPY ["src/BuildingBlocks/BuildingBlocks.Application/BuildingBlocks.Application.csproj", "src/BuildingBlocks/BuildingBlocks.Application/"]
COPY ["src/BuildingBlocks/BuildingBlocks.Domain/BuildingBlocks.Domain.csproj", "src/BuildingBlocks/BuildingBlocks.Domain/"]
COPY ["src/BuildingBlocks/BuildingBlocks.Contracts/BuildingBlocks.Contracts.csproj", "src/BuildingBlocks/BuildingBlocks.Contracts/"]
RUN dotnet restore "src/Services/Lobby/Lobby.Api/Lobby.Api.csproj"

COPY src/ src/
RUN dotnet publish "src/Services/Lobby/Lobby.Api/Lobby.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80
ENTRYPOINT ["dotnet", "Lobby.Api.dll"]