# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY WalletService.API/WalletService.API.csproj ./WalletService.API/
RUN cd WalletService.API && dotnet restore
COPY WalletService.API/ ./WalletService.API/
RUN cd WalletService.API && dotnet publish -c Release -o /app/publish

# Stage 2: Runtime (smaller image)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WalletService.API.dll"]
