FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copiar todo o código fonte mantendo a estrutura
COPY ["src/", "."]

# Restore packages
RUN dotnet restore "Payments.EventDriven.Processor/Payments.EventDriven.Processor.csproj"

# Build
WORKDIR "/src/Payments.EventDriven.Processor"
RUN dotnet build "Payments.EventDriven.Processor.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Payments.EventDriven.Processor.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Payments.EventDriven.Processor.dll"]
