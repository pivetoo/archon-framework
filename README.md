# Archon Framework

Framework backend em `.NET 10` para APIs multi-tenant com `Entity Framework Core`, `Dapper`, autenticação integrada ao `IdentityManagement`, auditoria automática e infraestrutura reutilizável para aplicações internas.

## Estrutura

O núcleo da solução fica em [`Archon/Archon.slnx`](/mnt/c/development/web-projects/frameworks/archon-framework/Archon/Archon.slnx) e hoje está dividido assim:

- `Archon.Core`
  Contém entidades base, auditoria, paginação, responses e tipos fundamentais.
- `Archon.Application`
  Contém contratos de tenant, autenticação, persistência e services.
- `Archon.Infrastructure`
  Contém multi-tenant, persistência `EF Core` e `Dapper`, `IdentityManagement`, migrations, Hangfire e implementações base.
- `Archon.Api`
  Contém a camada HTTP do framework: controllers base, middlewares, autenticação, autorização e sync automático de acessos.
- `Archon.Testing`
  Contém a base de testes do framework, com separação entre `Unit` e `Integration`.

## Funcionalidades

- Multi-tenant por resolução de tenant e connection string.
- Persistência híbrida com `EF Core` e `Dapper`.
- `FluentMigrator` para migrations.
- Integração com `IdentityManagement`.
- Autenticação JWT dinâmica por `client_id`.
- Validação opcional de sessão.
- Sync opcional de usuário autenticado.
- `CrudService` base.
- `ApiControllerBase` e `ApiControllerCrud`.
- Autorização com `RequireAccess` e `RequireRoot`.
- Envelope padrão de resposta para APIs.
- Paginação padrão.
- Auditoria automática de `Insert`, `Update` e `Delete`.
- Endpoint de auditoria por entidade e detalhe por evento.
- Integração base com Hangfire.
- Sync automático de acessos com o `IdentityManagement`.

## Convenções do framework

- Toda entidade deve herdar de `Entity`.
- Toda entidade tem `Id`, `CreatedAt` e `UpdatedAt`.
- O `ArchonDbContext` aplica convenções automáticas para:
  - `Id` com geração automática.
  - `CreatedAt` obrigatório.
  - `UpdatedAt` opcional.
  - `string` com `MaxLength(255)` por padrão.
  - `decimal` com precisão `(18, 6)` por padrão.
  - relacionamentos com `DeleteBehavior.Restrict`.
- Configurações manuais em `IEntityTypeConfiguration<T>` continuam tendo precedência.

## Multi-tenant

O modelo principal do `Archon` é tenant por conexão, seguindo a linha do `dNET`.

Fluxo:

1. A request chega.
2. O middleware resolve o tenant via `client_id`.
3. O `ITenantContext` é preenchido.
4. `EF Core` e `Dapper` usam a conexão do tenant resolvido.

Exemplo de configuração:

```json
{
  "TenantDatabases": {
    "default": {
      "CompanyName": "Archon",
      "ApplicationId": "archon-app",
      "ConnectionString": "Host=localhost;Database=archon;Username=user;Password=password",
      "DatabaseType": "PostgreSql",
      "Schema": "public"
    }
  }
}
```

## Persistência

O `Archon` usa dois caminhos complementares:

- `EF Core`
  Para escrita, domínio, tracking, transações e fluxo padrão de aplicação.
- `Dapper`
  Para leitura especializada, relatórios e consultas críticas.

Registro base:

```csharp
builder.Services.AddArchonPersistence(builder.Configuration, typeof(Program).Assembly);
```

Também existe auto-registro de services por convenção:

```csharp
builder.Services.AddServicesFromAssembly(typeof(Program).Assembly);
```

## API base

O `Archon.Api` oferece:

- `ApiControllerBase`
- `ApiControllerCrud<T>`
- middlewares de exception handling, tenant, sessão e sync de usuário
- autenticação dinâmica
- atributos HTTP customizados
- atributos de autorização

Exemplo de bootstrap:

```csharp
using Archon.Api.DependencyInjection;
using Archon.Api.MultiTenancy;
using Archon.Infrastructure.DependencyInjection;

builder.Services.AddControllers();
builder.Services.AddArchonApi(builder.Configuration);
builder.Services.AddArchonPersistence(builder.Configuration, typeof(Program).Assembly);
builder.Services.AddArchonAuthentication(builder.Configuration);
builder.Services.AddServicesFromAssembly(typeof(Program).Assembly);

var app = builder.Build();

app.UseArchonApi();
app.UseAuthentication();
app.UseAuthorization();
app.UseSessionValidation();
app.UseIdentityManagementUserSync();

app.MapControllers();

await app.UseArchonAccessSyncAsync();

app.Run();
```

## Autorização

O acesso é calculado automaticamente por convenção:

- `controller.action`

Exemplos:

- `UserController.Create` -> `user.create`
- `AuditController.GetByEntity` -> `audit.getByEntity`

Uso:

```csharp
[RequireAccess]
[PostEndpoint]
public IActionResult Create([FromBody] CreateUserRequest request)
{
    return Http200();
}
```

Para acesso global:

```csharp
[RequireRoot]
```

O claim esperado é:

- `permission`
- `root=true`

## Envelope padrão das respostas

O frontend sempre recebe o mesmo contrato base:

```json
{
  "message": "",
  "data": {},
  "errors": null,
  "pagination": null
}
```

Campos nulos são omitidos na serialização quando não forem necessários.

Exemplo de lista paginada:

```json
{
  "message": "",
  "data": [
    {
      "id": 1,
      "name": "Customer A"
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalCount": 100,
    "totalPages": 5,
    "hasPreviousPage": false,
    "hasNextPage": true
  }
}
```

## Auditoria

A auditoria é gerada automaticamente no `ArchonDbContext` para entidades rastreadas pelo `EF Core`.

Hoje o framework registra:

- `Insert`
- `Update`
- `Delete`
- propriedades alteradas
- usuário
- tenant
- `TraceId` como `CorrelationId`
- relação pai e filho quando aplicável

Endpoints disponíveis:

- `GET /api/audit/entity/{entityName}/{entityId}`
  Lista paginada dos eventos de auditoria da entidade.
- `GET /api/audit/{auditEntryId}`
  Detalhe de um evento específico com as propriedades alteradas.

## Migrations

O framework usa `FluentMigrator`.

Exemplo:

```csharp
builder.Services.RunMigrations(builder.Configuration, "public", typeof(Program).Assembly);
```

As migrations são executadas para os tenants configurados quando `RunMigrations` estiver habilitado.

## Background jobs

O `Archon` oferece integração base com Hangfire.

Registro:

```csharp
builder.Services.AddArchonHangfire(builder.Configuration);
```

Pipeline:

```csharp
app.UseArchonHangfire();
```

Atualmente o storage suportado no framework é:

- PostgreSQL
- SQL Server

## Sync automático de acessos

O framework consegue descobrir os endpoints com `[RequireAccess]` e enviar os recursos para o `IdentityManagement`.

Cada recurso contém:

- `Name`
- `Controller`
- `Action`
- `HttpMethod`
- `Route`

O sync é disparado manualmente no startup:

```csharp
await app.UseArchonAccessSyncAsync();
```

O endpoint esperado no `IdentityManagement` é:

- `POST /api/access-resources/sync`

## Testes

O projeto [`Archon.Testing`](/mnt/c/development/web-projects/frameworks/archon-framework/Archon/Archon.Testing) está separado em:

- `Unit`
  Testes de entidades, paginação, responses e multi-tenant.
- `Integration`
  Estrutura inicial para testes do pipeline HTTP.