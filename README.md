<div align="center">

# 💳 Payments Event-Driven API

### Sistema de Processamento de Pagamentos com Arquitetura Orientada a Eventos

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791?style=for-the-badge&logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Apache Kafka](https://img.shields.io/badge/Apache%20Kafka-7.5.0-231F20?style=for-the-badge&logo=apache-kafka)](https://kafka.apache.org/)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?style=for-the-badge&logo=docker&logoColor=white)](https://www.docker.com/)
[![CircleCI](https://img.shields.io/badge/CircleCI-CI%2FCD-343434?style=for-the-badge&logo=circleci)](https://circleci.com/)
[![Playwright](https://img.shields.io/badge/Playwright-E2E-2EAD33?style=for-the-badge&logo=playwright&logoColor=white)](https://playwright.dev/)

**[Visão Geral](#-visão-geral) • 
[Arquitetura](#-arquitetura-e-padrões) • 
[Quick Start](#-quick-start) • 
[API](#-api-endpoints) • 
[Testes](#-testes-e2e) • 
[CI/CD](#-cicd---circleci)**

</div>

---

## 📖 Visão Geral

**Payments Event-Driven** é uma solução robusta de processamento de pagamentos desenvolvida em **.NET 10**, projetada com arquitetura orientada a eventos para garantir **confiabilidade**, **escalabilidade** e **extensibilidade**.

### 🎯 Principais Características

- **🔄 Event-Driven Architecture**: Processamento assíncrono com Apache Kafka
- **💾 Outbox Pattern**: Garantia de entrega de eventos (transactional outbox)
- **🔁 Circuit Breaker**: Resiliência em falhas com recuperação automática
- **🎭 Factory Pattern**: Extensível para novos tipos de pagamento sem deployment
- **🔑 Idempotência**: Tratamento seguro de requisições duplicadas
- **📊 Observabilidade**: Health checks, métricas e rastreamento distribuído
- **🧪 Testes E2E**: Suite completa com Playwright + dashboard CircleCI
- **🐳 Docker**: Deploy simplificado com Docker Compose

### 💼 Casos de Uso

- ✅ Pagamentos genéricos (Default)
- ✅ DARF (Documento de Arrecadação de Receitas Federais)
- ✅ DARJ (Documento de Arrecadação do Estado do Rio de Janeiro)
- 🔄 Extensível para PIX, boletos, cartões, etc.

---

## 🏗️ Arquitetura e Padrões

### Stack Tecnológica

```yaml
Backend:
  Runtime: .NET 10 (C# 13)
  Framework: ASP.NET Core Web API
  ORM: Entity Framework Core 10.0.3

Database:
  Primary: PostgreSQL 16
  Precision: Decimal(19,2) para valores monetários

Messaging:
  Broker: Apache Kafka 7.5.0
  Coordinator: Zookeeper 7.5.0
  UI: Kafka UI (porta 8081)

Infrastructure:
  Containerization: Docker + Docker Compose
  Management: Portainer CE (porta 9000)

Testing:
  E2E: Playwright 1.58.2 (Node.js 22)
  Runtime: Node.js E2E tests

CI/CD:
  Platform: CircleCI
  Machine: Ubuntu 2204 (large resource class)
```

### Padrões Arquiteturais Implementados

#### 🏛️ Domain-Driven Design (DDD)

```
📁 Payments.EventDriven
├── 📦 Domain                    → Entidades, Value Objects, Business Rules
│   ├── Entities                 → Payment, OutboxMessage
│   ├── Enums                    → PaymentStatus, PaymentType
│   ├── Exceptions               → Domain-specific exceptions
│   └── Abstractions             → IEntity interface
│
├── 📦 Application               → Use Cases, DTOs, Handlers
│   ├── UseCases                 → CreatePayment, ProcessPayment, GetPayment, DeletePayment
│   ├── EventHandlers            → DefaultPayment, DarfPayment, DarjPayment
│   ├── DTOs                     → CreatePaymentRequest, GetPaymentResponse
│   └── Interfaces               → Repository contracts, IEventHandler
│
├── 📦 Infrastructure            → Persistência, Messaging, External Services
│   ├── Persistence              → DbContext, Repositories, Migrations
│   ├── Messaging                → KafkaProducer, ResilientKafkaProducer
│   ├── HealthChecks             → OutboxHealthCheck, ClockSkewHealthCheck
│   └── Observability            → LogBasedMetricsService
│
└── 📦 API                       → Apresentação, Controllers, Middlewares
    ├── Controllers              → PaymentsController
    ├── Filters                  → ValidationFilter
    ├── Middlewares              → ExceptionMiddleware
    └── Extensions               → ApplicationBuilderExtensions
```

#### ⚡ Outbox Pattern - Garantia de Entrega

```
┌──────────────────────────────────────────────────────────────┐
│ ATOMIC TRANSACTION (PostgreSQL)                              │
│                                                              │
│  BEGIN TRANSACTION                                           │
│  ├─ INSERT INTO Payments (Status=Pending)                   │
│  ├─ INSERT INTO OutboxMessages (Status=Pending)             │
│  └─ COMMIT                                                   │
│                                                              │
│  ✅ Sucesso: Ambos persistidos                              │
│  ❌ Falha: Nenhum persistido (rollback)                     │
└──────────────────────────────────────────────────────────────┘
              ▼
┌──────────────────────────────────────────────────────────────┐
│ BACKGROUND WORKER (OutboxProcessor)                          │
│                                                              │
│  Poll: 500ms                                                 │
│  └─ SELECT OutboxMessages WHERE Status=Pending              │
│     FOR UPDATE SKIP LOCKED (evita duplicatas)               │
│                                                              │
│  Publish to Kafka ─┬─ ✅ Success: MarkAsProcessed()         │
│                    └─ ❌ Failure: Retry (max 10x)           │
│                       └─ DLQ após 10 falhas                 │
└──────────────────────────────────────────────────────────────┘
```

#### 🔌 Circuit Breaker - Resiliência

```csharp
CLOSED (Normal) ──5 falhas consecutivas──> OPEN (Fail-fast por 1min)
      ▲                                            │
      │                                            │
      └──────────── HALF-OPEN ◄────────────────────┘
                   (Testa recuperação)
```

#### 🏭 Factory Pattern - Extensibilidade

```csharp
// Adicionar novo tipo de pagamento:
// 1. Criar handler
public class PixPaymentHandler : IEventHandler
{
    public string EventType => "pix-payment-created";
    public async Task HandleAsync(string payload, string? correlationId, CancellationToken ct)
    {
        // Lógica específica PIX
    }
}

// 2. Registrar no DI
services.AddScoped<IEventHandler, PixPaymentHandler>();

// ✅ PRONTO! Factory automaticamente roteia eventos
```

### Fluxo de Dados Completo

```
┌──────────────┐  POST /api/payments      ┌─────────────────┐
│   Client     │─────────────────────────>│  API Gateway    │
└──────────────┘  {amount, currency}      └────────┬────────┘
                                                   │
                  201 Created                      │
                  {id: "uuid"}                     │
                                                   ▼
                                          ┌─────────────────┐
                                          │ Validation      │
                                          │ Filter          │
                                          └────────┬────────┘
                                                   │
                                                   ▼
                                          ┌─────────────────────────┐
                                          │ CreatePaymentUseCase    │
                                          ├─────────────────────────┤
                                          │ BEGIN TRANSACTION       │
                                          │ ├─ INSERT Payment       │
                                          │ ├─ INSERT OutboxMessage │
                                          │ └─ COMMIT               │
                                          └────────┬────────────────┘
                                                   │
                  ┌────────────────────────────────┴─────────────┐
                  │                                              │
                  ▼                                              ▼
     ┌────────────────────┐                         ┌──────────────────┐
     │ PostgreSQL         │                         │ Outbox Table     │
     │ ├─ Payments        │                         │ ├─ Messages      │
     │ │  └─ Status:      │                         │ │  └─ Status:    │
     │ │     Pending      │                         │ │     Pending    │
     │ └─ ...             │                         │ └─ ...           │
     └────────────────────┘                         └─────┬────────────┘
              ▲                                           │
              │                                           │
              │                                    ┌──────▼─────────────┐
              │                                    │ OutboxProcessor    │
              │                                    │ Worker             │
              │                                    │ (Background)       │
              │                                    └──────┬─────────────┘
              │                                           │
              │                                           │ PUBLISH
              │                                           ▼
              │                                    ┌────────────────────┐
              │                                    │ Apache Kafka       │
              │                                    │ Topic:             │
              │                                    │ payment-created    │
              │                                    └──────┬─────────────┘
              │                                           │
              │                                    CONSUME│
              │                                           ▼
              │                                    ┌────────────────────┐
              │                                    │ EventRouter        │
              │                                    │ Worker             │
              │                                    ├────────────────────┤
              │                                    │ Route by eventType │
              │                                    │ ├─ Default Handler │
              │                                    │ ├─ DARF Handler    │
              │                                    │ └─ DARJ Handler    │
              │                                    └──────┬─────────────┘
              │                                           │
              │                                           ▼
              │                                    ┌────────────────────┐
              │                                    │ ProcessPayment     │
              │                                    │ UseCase            │
              │                                    ├────────────────────┤
              │                                    │ MarkAsProcessed()  │
              └────────────────────────────────────│ UPDATE Payment     │
                                                   │ Status: Processed  │
                                                   └────────────────────┘
```

---

## 🚀 Quick Start

### Pré-requisitos

```bash
# Verificar instalações
dotnet --version          # .NET 10.0+
docker --version          # Docker 20.10+
docker-compose --version  # Docker Compose 2.x+
node --version            # Node.js 22.x (para testes E2E)
```

### Opção 1: Docker Compose (Recomendado)

```bash
# 1. Clonar repositório
git clone <repository-url>
cd Payments.EventDriven

# 2. Iniciar todos os serviços
docker-compose up -d

# 3. Verificar saúde dos serviços
curl http://localhost:8080/health

# 4. Acessar dashboards
# API Swagger: http://localhost:8080/swagger
# Kafka UI: http://localhost:8081
# Portainer: http://localhost:9000

# 5. Criar primeiro pagamento
curl -X POST http://localhost:8080/api/payments \
  -H "Content-Type: application/json" \
  -d '{"amount": 150.50, "currency": "BRL", "type": 0}'

# 6. Verificar logs de processamento
docker-compose logs -f workers

# 7. Parar serviços
docker-compose down -v
```

### Opção 2: Execução Local (Desenvolvimento)

```bash
# 1. Instalar EF Core CLI
dotnet tool install --global dotnet-ef

# 2. Subir apenas dependências (PostgreSQL + Kafka)
docker-compose up -d postgres kafka zookeeper

# 3. Criar e aplicar migrations
dotnet ef migrations add InitialCreate \
  --project src/Payments.EventDriven.Infrastructure \
  --startup-project src/Payments.EventDriven.Api

dotnet ef database update \
  --project src/Payments.EventDriven.Infrastructure \
  --startup-project src/Payments.EventDriven.Api

# 4. Executar API
dotnet run --project src/Payments.EventDriven.Api

# 5. (Novo terminal) Executar Workers
dotnet run --project src/Payments.EventDriven.ProcessPayment

# 6. Testar API
curl http://localhost:8080/health
```

### Scripts PowerShell Utilitários

```powershell
# Criar nova migration
.\create-migration.ps1 -MigrationName "AddPaymentType"

# Atualizar banco de dados
.\update-database.ps1
```

---

## 📡 API Endpoints

### Base URL: `http://localhost:8080/api/payments`

#### 1️⃣ Criar Pagamento

```http
POST /api/payments
Content-Type: application/json
X-Correlation-Id: <optional-uuid>

{
  "amount": 1500.50,
  "currency": "BRL",
  "type": 0,                    // 0=Default, 1=DARF, 2=DARJ
  "idempotencyKey": "req-12345" // Opcional (evita duplicatas)
}
```

**Responses:**
```json
// 201 Created
{
  "id": "550e8400-e29b-41d4-a716-446655440000"
}

// 400 Bad Request (validação)
{
  "errors": {
    "Amount": ["Amount must be greater than 0."],
    "Currency": ["Currency must be a valid ISO 4217 code."]
  }
}
```

**Validações:**
- ✅ `amount` > 0 e ≤ 2 casas decimais
- ✅ `currency` ISO 4217 (ex: BRL, USD, EUR)
- ✅ `type` ∈ {0, 1, 2}
- ✅ `idempotencyKey` único (opcional)

---

#### 2️⃣ Consultar Pagamento

```http
GET /api/payments/{id}
```

**Response 200 OK:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "amount": 1500.50,
  "currency": "BRL",
  "type": 0,
  "status": "Processed",        // Pending | Processed | Failed
  "createdAt": "2026-03-08T10:30:00Z",
  "failureReason": null
}
```

**Responses:**
- `200 OK` → Pagamento encontrado
- `404 Not Found` → ID inexistente

---

#### 3️⃣ Deletar Pagamento

```http
DELETE /api/payments/{id}
```

**Responses:**
- `204 No Content` → Deletado com sucesso
- `404 Not Found` → ID inexistente

---

#### 4️⃣ Health Check

```http
GET /health
```

**Response 200 OK:**
```json
{
  "status": "Healthy",
  "checks": {
    "database": {
      "status": "Healthy"
    },
    "outbox": {
      "status": "Healthy",
      "data": {
        "PendingMessages": 3,
        "FailedMessages": 0
      }
    },
    "clock_sync": {
      "status": "Healthy"
    }
  }
}
```

---

## 🧪 Testes E2E

### Suite de Testes Playwright

```
📁 tests/payments/
├── ✅ create-payment.spec.ts       → Criação e validações
├── ✅ get-payment.spec.ts          → Consultas
├── ✅ delete-payment.spec.ts       → Deleções
├── ✅ idempotency.spec.ts          → Requisições duplicadas
├── ✅ payment-types.spec.ts        → Default, DARF, DARJ
├── ✅ correlation.spec.ts          → X-Correlation-Id propagation
└── ✅ state-transitions.spec.ts    → Pending → Processed → Failed
```

### Configuração

```typescript
// playwright.config.ts
{
  workers: process.env.CI ? 2 : undefined,  // Paralelismo no CI
  retries: process.env.CI ? 2 : 0,          // Retry automático
  
  reporter: [
    'list',                                 // Console output
    'junit',                                // CircleCI metrics
    'html',                                 // Dashboard interativo
    'json'                                  // Raw results
  ],
  
  trace: 'retain-on-failure',               // Debug traces
  baseURL: 'http://localhost:8080'
}
```

### Executar Testes

```bash
cd src/Payments.EventDriven.E2E

# Instalar dependências
npm install

# Executar testes (headless)
npm test

# Modo interativo (UI)
npm run test:ui

# Debug mode (step-by-step)
npm run test:debug

# Modo headed (com browser visível)
npm run test:headed

# Ver relatório HTML
npx playwright show-report
```

### Exemplo de Teste

```typescript
test('should create payment and auto-process', async ({ request }) => {
    // 1. Criar pagamento
    const response = await createPayment(request, {
        amount: 1500.50,
        currency: 'BRL',
        type: 0
    });
    
    expect(response.status()).toBe(201);
    const { id } = await response.json();
    
    // 2. Aguardar processamento assíncrono
    const payment = await waitForProcessing(request, id, 30000);
    
    // 3. Validar transição de status
    expect(payment.status).toBe('Processed');
    expect(payment.amount).toBe(1500.50);
});

test('should handle idempotency correctly', async ({ request }) => {
    const idempotencyKey = 'unique-req-001';
    
    // Request 1
    const res1 = await createPayment(request, {
        amount: 100,
        currency: 'BRL',
        idempotencyKey
    });
    const { id: id1 } = await res1.json();
    
    // Request 2 (duplicado)
    const res2 = await createPayment(request, {
        amount: 100,
        currency: 'BRL',
        idempotencyKey
    });
    const { id: id2 } = await res2.json();
    
    // Mesmo ID retornado
    expect(id1).toBe(id2);
});
```

### Cobertura de Testes

#### ✅ Implementados (29 cenários)

| Categoria | Cenários | Status |
|-----------|----------|--------|
| **Criação** | Validações, tipos de pagamento | ✅ 8/8 |
| **Consulta** | GET existente, inexistente | ✅ 6/6 |
| **Deleção** | DELETE, verificações | ✅ 4/4 |
| **Idempotência** | Duplicatas, conflitos | ✅ 3/3 |
| **Correlação** | Headers X-Correlation-Id | ✅ 3/3 |
| **Transições** | Pending→Processed→Failed | ✅ 5/5 |

#### 🔄 Pendentes (18 cenários)

- Event-driven (outbox, DLQ, retries)
- Performance (load testing)
- Error handling (formatos de erro)

---

## 🔄 CI/CD - CircleCI

### Pipeline Completo

```yaml
Workflow: build-and-test (triggered on every push)

├─ 1. Setup .NET SDK 10
│  └─ Download & install dotnet 10.0
│
├─ 2. Restore Dependencies (Cached)
│  ├─ Cache key: nuget-packages-v1-{{ checksum *.csproj }}
│  └─ dotnet restore (all projects)
│
├─ 3. Build Solution
│  └─ Configuration: Release
│     ├─ Payments.EventDriven.Domain
│     ├─ Payments.EventDriven.Application
│     ├─ Payments.EventDriven.Infrastructure
│     ├─ Payments.EventDriven.Api
│     └─ Payments.EventDriven.ProcessPayment
│
├─ 4. Setup Node.js 22 + Playwright
│  ├─ npm install
│  └─ Cache: node_modules + Playwright browsers
│
├─ 5. Start Docker Compose
│  └─ Services: postgres, kafka, zookeeper, api, workers
│
├─ 6. Wait for Services
│  ├─ PostgreSQL: pg_isready
│  ├─ Kafka: broker-api-versions
│  └─ API: curl /health (timeout 120s)
│
├─ 7. Run E2E Tests (Playwright)
│  ├─ Workers: 2 (parallel)
│  ├─ Retries: 2 (auto-retry on failure)
│  └─ Reporters: list, junit, html, json
│
├─ 8. Store Test Results
│  ├─ JUnit XML → CircleCI metrics (aba "Tests")
│  ├─ HTML report → Artifacts (dashboard interativo)
│  └─ Test artifacts → Screenshots, videos, traces
│
└─ 9. Cleanup
   └─ docker-compose down -v
```

### Dashboards no CircleCI

1. **Aba "Tests"** → Métricas agregadas (passed/failed/skipped)
2. **Aba "Artifacts"** → `playwright-report/index.html` (dashboard interativo)
3. **Aba "Artifacts"** → `test-artifacts/` (screenshots, vídeos, traces)

### Cache Strategy

```yaml
# NuGet packages (~500MB)
- restore_cache:
    keys:
      - nuget-packages-v1-{{ checksum "*.csproj" }}

# NPM + Playwright browsers (~300MB)
- restore_cache:
    keys:
      - npm-packages-v1-{{ checksum "package.json" }}
    paths:
      - node_modules
      - ~/.cache/ms-playwright
```

---

## 📊 Observabilidade e Monitoramento

### Health Checks

#### 1. Database Health Check
```json
{
  "status": "Healthy | Degraded | Unhealthy",
  "description": "PostgreSQL connection + pending migrations"
}
```

#### 2. Outbox Health Check
```json
{
  "status": "Healthy | Degraded | Unhealthy",
  "data": {
    "PendingMessages": 150,  // < 100: Healthy, 100-499: Degraded, >= 500: Unhealthy
    "FailedMessages": 5
  }
}
```

#### 3. Clock Skew Health Check
```json
{
  "status": "Healthy | Unhealthy",
  "description": "Detecta dessincronia de relógio entre serviços (importante para eventos)"
}
```

### Rastreamento Distribuído

**Header X-Correlation-Id:**
```http
# Cliente envia (opcional)
X-Correlation-Id: req-12345-abc

# API retorna (sempre)
X-Correlation-Id: req-12345-abc  # (ou UUID gerado)

# Propagado em:
✅ Response headers
✅ Logs da aplicação
✅ OutboxMessages
✅ Eventos Kafka (headers)
```

**Exemplo de rastreamento:**
```
2026-03-08 10:30:00 [INFO] Payment created. PaymentId=550e8400 CorrelationId=req-001
2026-03-08 10:30:01 [INFO] Publishing to Kafka. PaymentId=550e8400 CorrelationId=req-001
2026-03-08 10:30:02 [INFO] Event consumed. PaymentId=550e8400 CorrelationId=req-001
2026-03-08 10:30:03 [INFO] Payment processed. PaymentId=550e8400 CorrelationId=req-001
```

### Dashboards Disponíveis

| Dashboard | URL | Descrição |
|-----------|-----|-----------|
| **API Swagger** | `http://localhost:8080/swagger` | Documentação interativa da API |
| **Kafka UI** | `http://localhost:8081` | Monitoramento de topics, consumers, messages |
| **Portainer** | `http://localhost:9000` | Gerenciamento de containers Docker |
| **Health Checks** | `http://localhost:8080/health` | Status dos serviços |

### Logs Estruturados

```csharp
// Levels configurados em appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.EntityFrameworkCore": "Warning",
      "Confluent.Kafka": "Warning"
    }
  }
}
```

---

## 🔧 Configuração

### Variáveis de Ambiente

```bash
# Database
ConnectionStrings__DefaultConnection="Host=localhost;Port=5431;Database=paymentsdb;Username=postgres;Password=postgres"
Database__RunMigrationsOnStartup=true

# Kafka
Kafka__BootstrapServers=localhost:9092
Kafka__Topic=payment-created
Kafka__GroupId=payment-processor-group

# ASP.NET Core
ASPNETCORE_URLS=http://+:8080
ASPNETCORE_ENVIRONMENT=Development

# Worker Service
DOTNET_ENVIRONMENT=Production
```

### appsettings.json (Produção)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres;Port=5432;Database=paymentsdb;Username=postgres;Password=postgres"
  },
  "Kafka": {
    "BootstrapServers": "kafka:29092",
    "Topic": "payment-created",
    "GroupId": "payment-processor-group"
  },
  "Database": {
    "RunMigrationsOnStartup": true
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

### appsettings.Development.json (Desenvolvimento)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5431;Database=paymentsdb;Username=postgres;Password=postgres"
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "Topic": "payment-created",
    "GroupId": "payment-processor-group"
  }
}
```

---

## 🐳 Docker Compose - Serviços

```yaml
# Serviços disponíveis

postgres:16                        # Banco de dados principal
├─ Port: 5431 → 5432
├─ Database: paymentsdb
├─ Volume: postgres-data (persistente)
└─ Health check: pg_isready

kafka:7.5.0                        # Message broker
├─ Port: 9092 (external), 29092 (internal)
├─ Depends: zookeeper
└─ Health check: broker-api-versions

zookeeper:7.5.0                    # Kafka coordination
└─ Port: 2181

kafka-ui:latest                    # Dashboard visual
├─ Port: 8081
└─ Cluster: local → kafka:29092

portainer:ce-latest                # Container management
└─ Ports: 9000, 9443

api (Payments.EventDriven.Api)     # REST API
├─ Build: Dockerfile multi-stage
├─ Port: 8080
├─ Env: Development
├─ Depends: postgres, kafka (healthy)
└─ Network: payments-network

workers (Payments.ProcessPayment)  # Background workers
├─ Build: Dockerfile multi-stage
├─ Env: Production
├─ Depends: postgres, kafka (healthy)
├─ Services: OutboxProcessor, EventRouter, DlqMonitor
└─ Network: payments-network
```

---

## 🔍 Troubleshooting

### Erro: "Cannot connect to PostgreSQL"

```bash
# Verificar se PostgreSQL está rodando
docker-compose ps postgres

# Ver logs
docker-compose logs postgres

# Testar conexão manual
docker exec payments-postgres pg_isready -U postgres

# Restart PostgreSQL
docker-compose restart postgres
```

### Erro: "Cannot connect to Kafka"

```bash
# Verificar se Kafka está rodando
docker-compose ps kafka zookeeper

# Ver logs
docker-compose logs kafka

# Testar Kafka internamente
docker exec payments-kafka kafka-broker-api-versions --bootstrap-server localhost:9092

# Restart Kafka + Zookeeper
docker-compose restart zookeeper kafka
```

### Migrations não aplicadas

```bash
# Opção 1: Rebuild containers
docker-compose down -v
docker-compose up --build -d

# Opção 2: Aplicar manualmente
docker exec payments-api dotnet ef database update \
  --project src/Payments.EventDriven.Infrastructure

# Verificar logs da API
docker-compose logs -f api
# Aguardar: "Migrations applied successfully"
```

### Testes E2E falhando

```bash
# Verificar se API está saudável
curl http://localhost:8080/health

# Verificar logs da API
docker-compose logs -f api

# Limpar cache do Playwright
cd src/Payments.EventDriven.E2E
npx playwright cache clear
npm install
npx playwright install chromium --with-deps

# Executar testes com debug
npm run test:debug
```

### Workers não processando

```bash
# Verificar logs dos workers
docker-compose logs -f workers

# Verificar outbox table
docker exec payments-postgres psql -U postgres -d paymentsdb -c "SELECT * FROM outbox_messages LIMIT 10;"

# Verificar Kafka UI
# Acesse: http://localhost:8081
# Verifique: Topics > payment-created > Messages

# Restart workers
docker-compose restart workers
```

---

## 📚 Recursos Adicionais

### Documentação

- [.NET 10 Documentation](https://learn.microsoft.com/en-us/dotnet/)
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- [Apache Kafka](https://kafka.apache.org/documentation/)
- [Playwright](https://playwright.dev/docs/intro)
- [CircleCI](https://circleci.com/docs/)

### Padrões e Práticas

- [Outbox Pattern (Microsoft)](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/subscribe-events#designing-atomicity-and-resiliency)
- [Domain-Driven Design](https://martinfowler.com/bliki/DomainDrivenDesign.html)
- [Circuit Breaker Pattern](https://martinfowler.com/bliki/CircuitBreaker.html)
- [Event-Driven Architecture](https://martinfowler.com/articles/201701-event-driven.html)

### Scripts Utilitários

```powershell
# PowerShell Scripts

# Criar nova migration
.\create-migration.ps1 -MigrationName "AddNewField"

# Atualizar banco de dados
.\update-database.ps1

# Rollback última migration
.\create-migration.ps1 -MigrationName "Rollback" -Revert
```

---

## 👥 Contribuindo

```bash
# 1. Fork o repositório

# 2. Clone seu fork
git clone https://github.com/seu-usuario/Payments.EventDriven.git

# 3. Crie uma branch
git checkout -b feature/nova-funcionalidade

# 4. Faça suas alterações e commit
git commit -m "feat: adicionar nova funcionalidade"

# 5. Push para seu fork
git push origin feature/nova-funcionalidade

# 6. Abra um Pull Request
```

### Convenções de Commit

```
feat: Nova funcionalidade
fix: Correção de bug
docs: Atualização de documentação
test: Adição ou correção de testes
refactor: Refatoração de código
chore: Tarefas de manutenção
```

---

## 📄 Licença

Este projeto está sob a licença MIT. Veja o arquivo [LICENSE](LICENSE) para mais detalhes.

---

## 🎥 Demonstração em Vídeo

> **Veja a demonstração completa deste projeto:**
> 
> 🔗 **[Link do vídeo no LinkedIn](_ADICIONE_SEU_LINK_AQUI_)**
>
> 📺 No vídeo você verá:
> - Arquitetura Event-Driven em ação
> - Processamento assíncrono com Kafka
> - Outbox Pattern garantindo entrega
> - Dashboard de testes E2E no CircleCI
> - Monitoramento e observabilidade
> - Deploy completo com Docker

---

<div align="center">

⭐ **Se este projeto foi útil, deixe uma estrela no repositório!**

</div>
