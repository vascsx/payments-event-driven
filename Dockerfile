FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copiar projeto e arquivos fonte da API
COPY ["Payments.EventDriven.Api.csproj", "."]
COPY ["Controllers/", "Controllers/"]
COPY ["Extensions/", "Extensions/"]
COPY ["Filters/", "Filters/"]
COPY ["Middlewares/", "Middlewares/"]
COPY ["Properties/", "Properties/"]
COPY ["Program.cs", "."]
COPY ["appsettings*.json", "."]

# Copiar projetos referenciados
COPY ["src/Payments.EventDriven.Application/", "src/Payments.EventDriven.Application/"]
COPY ["src/Payments.EventDriven.Domain/", "src/Payments.EventDriven.Domain/"]
COPY ["src/Payments.EventDriven.Infrastructure/", "src/Payments.EventDriven.Infrastructure/"]

# Restore packages
RUN dotnet restore "Payments.EventDriven.Api.csproj"

# Build
RUN dotnet build "Payments.EventDriven.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Payments.EventDriven.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Payments.EventDriven.Api.dll"]