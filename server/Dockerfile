FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY GiphyServer.Api/GiphyServer.Api.csproj GiphyServer.Api/
RUN dotnet restore GiphyServer.Api/GiphyServer.Api.csproj

COPY . .
RUN dotnet publish GiphyServer.Api/GiphyServer.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "GiphyServer.Api.dll"]
