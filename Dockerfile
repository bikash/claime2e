FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app
COPY src/JbAutoAi/ .
RUN dotnet publish -c Release -o /out

# ponytail: Debian base kept over alpine — InvariantGlobalization=false needs ICU, which this image ships
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:8080
# Uploads must live on a mounted volume — the container filesystem is ephemeral.
ENV UPLOADS_DIR=/data/uploads
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "JbAutoAi.dll"]
