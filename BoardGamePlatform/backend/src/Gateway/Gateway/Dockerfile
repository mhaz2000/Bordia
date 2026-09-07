FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/Gateway/Gateway/Gateway.csproj", "src/Gateway/Gateway/"]
RUN dotnet restore "src/Gateway/Gateway/Gateway.csproj"

COPY src/ src/
RUN dotnet publish "src/Gateway/Gateway/Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80
ENTRYPOINT ["dotnet", "Gateway.dll"]