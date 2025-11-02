FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["CryptoTgShop/CryptoTgShop.csproj", "CryptoTgShop/"]
RUN dotnet restore "CryptoTgShop/CryptoTgShop.csproj"
COPY . .
WORKDIR "/src/CryptoTgShop"
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CryptoTgShop.dll"]


