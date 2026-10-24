# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY BouvetBike/BouvetBike.csproj BouvetBike/
RUN dotnet restore BouvetBike/BouvetBike.csproj
COPY BouvetBike/ BouvetBike/
RUN dotnet publish BouvetBike/BouvetBike.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
# The SQLite database and data protection keys live in /data (a volume in docker-compose.yml).
RUN mkdir /data && chown app:app /data
COPY --from=build /app .
USER app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__Default="Data Source=/data/bouvetbike.db" \
    DataProtection__KeysPath=/data/keys
EXPOSE 8080
ENTRYPOINT ["dotnet", "BouvetBike.dll"]
