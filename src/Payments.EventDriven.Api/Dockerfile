FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj files first for layer-cache optimised restore
COPY ["src/Payments.EventDriven.Api/Payments.EventDriven.Api.csproj", "src/Payments.EventDriven.Api/"]
COPY ["src/Payments.EventDriven.Application/Payments.EventDriven.Application.csproj", "src/Payments.EventDriven.Application/"]
COPY ["src/Payments.EventDriven.Domain/Payments.EventDriven.Domain.csproj", "src/Payments.EventDriven.Domain/"]
COPY ["src/Payments.EventDriven.Infrastructure/Payments.EventDriven.Infrastructure.csproj", "src/Payments.EventDriven.Infrastructure/"]

RUN dotnet restore "src/Payments.EventDriven.Api/Payments.EventDriven.Api.csproj"

# Copy full source
COPY . .

RUN dotnet build "src/Payments.EventDriven.Api/Payments.EventDriven.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "src/Payments.EventDriven.Api/Payments.EventDriven.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Payments.EventDriven.Api.dll"]
