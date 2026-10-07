# Etapa 1: compilăm aplicația.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["document-service.csproj", "./"]
RUN dotnet restore "document-service.csproj"

COPY . .
RUN dotnet publish "document-service.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa 2: imaginea finală, mai mică, care doar rulează aplicația.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "document-service.dll"]