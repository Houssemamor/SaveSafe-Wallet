# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY AuthService.API/AuthService.API.csproj ./AuthService.API/
RUN cd AuthService.API && dotnet restore
COPY AuthService.API/ ./AuthService.API/
RUN cd AuthService.API && dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime (smaller image)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AuthService.API.dll"]
