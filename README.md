# Payments Event-Driven API

Sistema de pagamentos com arquitetura orientada a eventos usando .NET 10, PostgreSQL e Apache Kafka.

## 📋 Pré-requisitos

- .NET 10 SDK
- Docker e Docker Compose
- (Opcional) EF Core CLI Tools: `dotnet tool install --global dotnet-ef`

## 🗄️ Gerenciamento de Banco de Dados

### Criar uma nova Migration

**Opção 1: Usando o script PowerShell**
```powershell
.\create-migration.ps1 -MigrationName "InitialCreate"
```

**Opção 2: Usando dotnet CLI diretamente**
```bash
dotnet ef migrations add InitialCreate \
  --project Payments.EventDriven.Infrastructure\Payments.EventDriven.Infrastructure.csproj \
  --startup-project Payments.EventDriven.Api.csproj \
  --output-dir Migrations \
  --context PaymentDbContext
```

### Aplicar Migrations Localmente

**Opção 1: Usando o script PowerShell**
```powershell
.\update-database.ps1
```

**Opção 2: Usando dotnet CLI**
```bash
dotnet ef database update \
  --project Payments.EventDriven.Infrastructure\Payments.EventDriven.Infrastructure.csproj \
  --startup-project Payments.EventDriven.Api.csproj \
  --context PaymentDbContext
```

### Migrations Automáticas no Docker

Quando você executar o container Docker, as migrations serão aplicadas **automaticamente** na inicialização da API. Veja os logs:

```bash
docker-compose logs -f api
```

Você verá mensagens como:
- `Applying pending migrations...` - Migrations estão sendo aplicadas
- `Migrations applied successfully!` - Sucesso
- `No pending migrations.` - Banco de dados já está atualizado

## 🚀 Executar com Docker

### Iniciar todos os serviços
```bash
docker-compose up -d
```

### Verificar logs da API
```bash
docker-compose logs -f api
```

### Verificar logs do Processor
```bash
docker-compose logs -f processor
```

### Parar todos os serviços
```bash
docker-compose down
```

### Parar e remover volumes (apaga o banco de dados)
```bash
docker-compose down -v
```

## 🔧 Desenvolvimento Local

### 1. Subir apenas as dependências (PostgreSQL e Kafka)
```bash
docker-compose up -d postgres kafka
```

### 2. Aplicar migrations
```bash
.\update-database.ps1
```

### 3. Executar a API localmente
```bash
dotnet run --project Payments.EventDriven.Api.csproj
```

### 4. Executar o Processor localmente
```bash
dotnet run --project Payments.EventDriven.Processor\Payments.EventDriven.Processor.csproj
```

## 📝 Configuração

### appsettings.json (Produção/Docker)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres;Port=5432;Database=paymentsdb;Username=postgres;Password=postgres"
  },
  "Kafka": {
    "BootstrapServers": "kafka:9092",
    "Topic": "payment-created"
  }
}
```

### appsettings.Development.json (Desenvolvimento Local)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=paymentsdb;Username=postgres;Password=postgres"
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "Topic": "payment-created"
  }
}
```

## 🏗️ Arquitetura

```
┌─────────────────┐
│   Client/UI     │
└────────┬────────┘
         │ HTTP
         ▼
┌─────────────────┐      ┌──────────────┐
│  API (Web API)  │─────▶│  PostgreSQL  │
└────────┬────────┘      └──────────────┘
         │
         │ Kafka Event
         ▼
┌─────────────────┐
│    Processor    │
│ (Worker Service)│
└────────┬────────┘
         │
         ▼
┌──────────────────┐
│    PostgreSQL    │
└──────────────────┘
```

## 🧪 Testando a API

### 1. Swagger UI
Acesse: http://localhost:8080/swagger

### 2. Criar um pagamento
```bash
curl -X POST http://localhost:8080/api/payments \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 100.50,
    "currency": "BRL"
  }'
```

### 3. Verificar os logs do Processor
```bash
docker-compose logs -f processor
```

Você verá:
- `Processing payment {PaymentId}` - Pagamento sendo processado
- `Payment {PaymentId} processed` - Pagamento processado com sucesso

## 📦 Estrutura do Projeto

```
Payments.EventDriven/
├── Payments.EventDriven.Api/           # Camada de apresentação (Web API)
├── Payments.EventDriven.Application/   # Casos de uso e DTOs
├── Payments.EventDriven.Domain/        # Entidades e regras de negócio
├── Payments.EventDriven.Infrastructure/# Persistência, Kafka, Migrations
└── Payments.EventDriven.Processor/     # Worker Service (consumidor Kafka)
```

## 🔍 Troubleshooting

### Erro: "Cannot connect to PostgreSQL"
```bash
# Verifique se o PostgreSQL está rodando
docker-compose ps postgres

# Verifique os logs
docker-compose logs postgres
```

### Erro: "Cannot connect to Kafka"
```bash
# Verifique se o Kafka está rodando
docker-compose ps kafka

# Verifique os logs
docker-compose logs kafka
```

### Migrations não aplicadas
```bash
# Reconstrua os containers
docker-compose down
docker-compose up --build -d

# Verifique os logs da API
docker-compose logs -f api
```
