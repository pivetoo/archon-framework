# Archon Framework

Framework backend em .NET 10 para construir APIs multi-tenant. Ele concentra o que toda API do
ecossistema teria que reescrever: resolução de tenant por conexão, autenticação JWT contra um
provedor de identidade externo, autorização por convenção, envelope único de resposta, paginação,
auditoria automática, mensagens de erro localizadas, eventos de domínio e migrations por tenant.

O framework é consumido como código-fonte via `ProjectReference`. Não há pacote NuGet publicado.

## Stack

| Área | Tecnologia |
| --- | --- |
| Runtime | .NET 10 |
| ORM | Entity Framework Core 10 |
| Bancos | PostgreSQL, SQL Server, MySQL |
| Migrations | FluentMigrator 8 |
| Registro por convenção | Scrutor 6 |
| Observabilidade | OpenTelemetry (opcional) |
| Testes | NUnit 4, Moq, EF Core InMemory/SQLite, `Microsoft.AspNetCore.TestHost` |

## Estrutura da solução

A solução fica em `Archon/Archon.slnx` e é dividida em cinco projetos.

```
Archon/
├── Archon.Core/            entidades base, auditoria, paginação, envelope, exceptions, templating
├── Archon.Application/     contratos (interfaces) de tenant, persistência, serviços e eventos
├── Archon.Infrastructure/  EF Core, multi-tenant, FluentMigrator, cliente HTTP, implementações
├── Archon.Api/             pipeline HTTP, controllers base, atributos, middlewares, segurança
└── Archon.Testing/         testes unitários e de integração do próprio framework
```

A dependência vai sempre de fora para dentro: `Api` -> `Infrastructure` -> `Application` -> `Core`.
`Core` não referencia nada.

### Archon.Core

Tipos sem dependência de infraestrutura.

- `Entity`: classe base de toda entidade. Expõe `Id` (`long`), `CreatedAt` (`DateTimeOffset`),
  `UpdatedAt` (`DateTimeOffset?`) e a coleção de eventos de domínio pendentes. Igualdade é por
  identidade: duas entidades só são iguais se forem do mesmo tipo e tiverem o mesmo `Id` não default.
- `ApiResponse<T>`: envelope de resposta.
- `PagedRequest`, `PagedResult<T>`, `PaginationMetadata`: contrato de paginação.
- `AuditEntry`, `AuditPropertyChange`, `AuditAction`: modelo de auditoria.
- `Notification`, `NotificationType`: modelo de notificação in-app.
- Exceptions de domínio: `DomainException` e derivadas.
- `TemplateInterpolator`: interpolação de `{{variavel}}` em strings.
- `DatabaseProvider`: `PostgreSql`, `SqlServer`, `MySql`.

### Archon.Application

Só interfaces e DTOs. É o contrato que a infraestrutura implementa.

- `ITenantContext`, `ITenantResolver`, `TenantInfo`
- `ICurrentUser`, `ISessionValidator`, `IIdentityManagementUserSyncService`
- `ICrudService<T>`, `IAuditService`, `INotificationService`, `IIntegrationService`
- `IDomainEventDispatcher`, `IDomainEventHandler<TEvent>`
- `Integration`, `IntegrationParameter`, `TenantBootstrapRequest`, `TenantBootstrapResult`

### Archon.Infrastructure

- `ArchonDbContext` com convenções automáticas, auditoria e disparo de eventos de domínio.
- Resolução de tenant: `ConfigurationTenantResolver` (via `appsettings`) e
  `IdentityCatalogTenantResolver` (via catálogo remoto).
- `DatabaseMigrator` e `TenantMigrationRunner` sobre FluentMigrator.
- `RestApi`: cliente HTTP tipado e enxuto.
- `IdentityManagementClient` e `IdentityUsersClient`: comunicação com o provedor de identidade.
- Implementações: `CrudService<T>`, `AuditService`, `NotificationService`, `IntegrationService`,
  `TenantBootstrapService`, `DomainEventDispatcher`.

### Archon.Api

- `ApiControllerBase`, `ReadOnlyController<T>`, `ApiControllerCrud<T>`.
- Atributos: `GetEndpoint`, `PostEndpoint`, `PutEndpoint`, `DeleteEndpoint`, `PatchEndpoint`,
  `RequireAccess`, `RequireRoot`, `AccessArea`.
- Middlewares: tratamento de exceptions, resolução de tenant, validação de sessão, sync de usuário.
- Autenticação JWT dinâmica (`DynamicJwtBearerHandler`, `DynamicJwtValidator`).
- Localização das mensagens do framework em pt-BR, en-US e es-AR.
- Controllers prontos: `Audit`, `Notifications`, `UsersManagement`, `Tenants`, `Localization`, `Health`.
- Sync automático de recursos de acesso com o provedor de identidade.
- Extensão opcional de OpenTelemetry.

## Como consumir

### 1. Referenciar os projetos

O caminho do framework é resolvido por propriedade MSBuild, para que a aplicação consumidora não
fique presa a um layout de diretório específico. Em um `Directory.Build.props` na raiz da aplicação:

```xml
<Project>
  <!-- Ordem de resolução: -p:ArchonFrameworkPath=... > variável ARCHON_FRAMEWORK_PATH > caminho padrão -->
  <PropertyGroup Condition="'$(ArchonFrameworkPath)' == '' AND '$(ARCHON_FRAMEWORK_PATH)' != ''">
    <ArchonFrameworkPath>$(ARCHON_FRAMEWORK_PATH)</ArchonFrameworkPath>
  </PropertyGroup>

  <PropertyGroup Condition="'$(ArchonFrameworkPath)' == ''">
    <ArchonFrameworkPath>$(MSBuildThisFileDirectory)..\caminho\para\archon-framework</ArchonFrameworkPath>
  </PropertyGroup>

  <PropertyGroup>
    <ArchonFrameworkPath>$([System.IO.Path]::GetFullPath('$(ArchonFrameworkPath)'))</ArchonFrameworkPath>
    <ArchonProjectsPath>$(ArchonFrameworkPath)/Archon</ArchonProjectsPath>
  </PropertyGroup>
</Project>
```

E, em cada `.csproj` da aplicação:

```xml
<ProjectReference Include="$(ArchonProjectsPath)/Archon.Api/Archon.Api.csproj" />
```

Evite symlink no caminho resolvido: o MSBuild passa a enxergar o mesmo projeto por dois caminhos
diferentes e o build quebra com erro de referência duplicada.

### 2. Bootstrap

A ordem do pipeline importa e não é intercambiável.

```csharp
using Archon.Api.DependencyInjection;
using Archon.Api.MultiTenancy;
using Archon.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddAuthorization();

// O segundo argumento registra os resources de localização DA APLICAÇÃO no catálogo.
builder.Services.AddArchonApi(builder.Configuration, typeof(MinhaAplicacaoResource));
builder.Services.AddArchonPersistence(builder.Configuration, typeof(Program).Assembly);
builder.Services.AddArchonAuthentication(builder.Configuration);
builder.Services.AddServicesFromAssembly(typeof(Program).Assembly);

var app = builder.Build();

app.UseArchonApi();          // localização de request + tratamento de exceptions
app.UseAuthentication();
app.UseArchonTenantResolution();  // DEPOIS da autenticação, de propósito
app.UseAuthorization();

app.MapControllers();

await app.UseArchonAccessSyncAsync();

app.Run();
```

Pontos que quebram silenciosamente se invertidos:

- `UseArchonTenantResolution()` **precisa** vir depois de `UseAuthentication()`. O tenant é lido de
  uma claim já validada; antes da autenticação o middleware seria obrigado a confiar em um JWT com
  assinatura não verificada.
- `AddArchonApi` sem os tipos de resource da aplicação faz o catálogo de tradução subir só com as
  mensagens do framework — os textos da aplicação somem sem erro de build.

### Extensões disponíveis

| Extensão | O que faz |
| --- | --- |
| `AddArchonApi(config, params Type[] resources)` | Localização, multi-tenant, `ICurrentUser`, filtro de validação, OpenAPI |
| `AddArchonPersistence(config, params Assembly[] models)` | `ArchonDbContext`, serviços base, handlers de eventos de domínio |
| `AddArchonAuthentication(config, scheme?)` | Esquema JWT dinâmico + clientes do provedor de identidade |
| `AddArchonMultiTenancy(config)` | Só a resolução de tenant (útil isolado) |
| `AddArchonIdentityManagement(config)` | Só os clientes do provedor de identidade |
| `AddArchonRestApi()` | Cliente HTTP `RestApi` |
| `AddServicesFromAssembly(assembly)` | Registro automático de serviços por convenção |
| `AddArchonOpenTelemetry(config, serviceName, version?)` | Traces e métricas via OTLP |
| `RunMigrations(config, schema, params Assembly[])` | Registra o runner e roda migrations no startup |
| `UseArchonApi()` | Localização de request e middleware de exceptions |
| `UseArchonTenantResolution()` | Resolve o tenant a partir da claim validada |
| `UseSessionValidation()` | Valida sessão a cada request |
| `UseIdentityManagementUserSync()` | Sincroniza o usuário autenticado com a base local |
| `UseArchonAccessSyncAsync()` | Publica os recursos de acesso no provedor de identidade |

## Multi-tenant

O modelo é **um banco por tenant**. Cada request resolve um tenant, e o tenant determina a connection
string usada pelo `DbContext` daquele escopo.

Fluxo:

1. A request é autenticada.
2. `TenantResolutionMiddleware` lê o identificador do tenant de uma claim já validada.
3. `ITenantResolver` traduz o identificador em `TenantInfo` (connection string, provider, schema).
4. `ITenantContext` é preenchido no escopo da request.
5. O `ArchonDbContext` daquele escopo é construído com a conexão do tenant resolvido.

Existem dois resolvers, escolhidos automaticamente no startup:

- **`ConfigurationTenantResolver`** (padrão): os tenants vêm de `TenantDatabases` no `appsettings`.
- **`IdentityCatalogTenantResolver`**: usado quando a seção `IdentityCatalog` está preenchida
  (`BaseUrl` + chave de API). Busca o tenant em um catálogo remoto, com cache positivo e negativo.

### Ausência de fallback silencioso

Se nenhum tenant for resolvido e houver **dois ou mais** tenants configurados, o framework lança
exceção em vez de assumir o primeiro. Escolher um tenant arbitrário significaria ler e gravar dado
no tenant errado, que é uma falha invisível. Com exatamente um tenant configurado (single-tenant),
ele é assumido sem erro.

## Persistência

Toda a persistência é `Entity Framework Core`. Cada tenant recebe seu `DbContext` com a conexão
resolvida no escopo da request.

### Convenções automáticas do `ArchonDbContext`

Aplicadas a todas as entidades mapeadas:

- `Id` com geração automática pelo banco (ignorado no insert).
- `CreatedAt` obrigatório, `UpdatedAt` opcional.
- Todo `DateTimeOffset` é convertido para UTC na ida e na volta.
- `string` sem `MaxLength` explícito vira `varchar(255)`.
- `decimal` sem precisão explícita vira `(18, 6)`.
- Toda foreign key que não seja de ownership usa `DeleteBehavior.Restrict`.
- Nomes de tabela e coluna em minúsculas.

Configurações manuais em `IEntityTypeConfiguration<T>` têm precedência sobre as convenções.

Como as entidades usam `DateTimeOffset`, colunas de data em PostgreSQL precisam ser `TIMESTAMPTZ`.
Criar a coluna como `TIMESTAMP` compila, mas estoura em runtime na primeira leitura.

### CrudService

`ICrudService<T>` é a base de escrita: `Insert`, `Update`, `Delete`, `Validate`, `CustomValidate` e
`ExecuteInTransaction`. Erros de validação ficam acumulados em `Messages` em vez de virar exceção.

`Insert` e `Update` limpam o `ChangeTracker` ao final. Operações com vários passos encadeados sobre a
mesma entidade devem usar o `DbContext` diretamente dentro de `ExecuteInTransaction`, e não uma
sequência de chamadas ao `CrudService`.

### Registro de serviços por convenção

```csharp
builder.Services.AddServicesFromAssembly(typeof(Program).Assembly);
```

Registra automaticamente apenas classes cujo **nome termina em `Service`** e cujo **namespace contém
`Services`**. Qualquer outra coisa (resolvers, gateways, storages, factories) precisa de `AddScoped`
manual. O esquecimento não aparece no build: a aplicação sobe e retorna 500 no primeiro uso.

## Envelope de resposta

Toda resposta usa o mesmo contrato. Campos nulos são omitidos na serialização.

```json
{
  "message": "",
  "data": {},
  "errors": null,
  "pagination": null
}
```

Lista paginada:

```json
{
  "message": "",
  "data": [{ "id": 1, "name": "Customer A" }],
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

`ApiControllerBase` expõe os helpers que montam esse envelope: `Http200`, `Http201`, `Http202`,
`Http204`, `Http400`, `Http401`, `Http403`, `Http404`, `Http409`, `Http412`, `Http422`, `Http500`,
além de `SendFile`, `SendPdf`, `SendExcel` e `SendCsv` para respostas binárias.

A serialização de corpo usa `JsonSerializerDefaults.Web` (camelCase), então DTOs normalmente não
precisam de `[JsonPropertyName]`.

## Rotas

`ApiControllerBase` é anotado com `[ApiController]` e `[Route("api/[controller]")]`. Os atributos de
endpoint usam `[action]` como template padrão:

```csharp
[GetEndpoint]                                  // GET  api/user/Get
[PostEndpoint]                                 // POST api/user/Create
[GetEndpoint("{id:long}")]                     // GET  api/user/GetById/{id}
[GetEndpoint("entity/{name}/{entityId}")]      // GET  api/user/entity/{name}/{entityId}
```

A regra de normalização: um template que **começa com `{`** recebe `[action]/` na frente; qualquer
outro template é usado literalmente, substituindo a action na rota. Isso costuma surpreender ao
consumir a API pelo frontend — vale conferir o atributo antes de assumir que uma rota está quebrada.

## Autorização

O nome do acesso é derivado por convenção de `controller.action`, em camelCase:

- `UserController.Create` -> `user.create`
- `AuditController.GetByEntity` -> `audit.getByEntity`

```csharp
[AccessArea("user.area")]
public sealed class UserController : ApiControllerBase
{
    [RequireAccess("user.create.description")]
    [PostEndpoint]
    public IActionResult Create([FromBody] CreateUserRequest request) => Http200();
}
```

`RequireAccess` autoriza por dois caminhos:

1. **Usuário autenticado**: passa se tiver a claim `root=true`, ou a claim `permission` com o valor
   do acesso calculado.
2. **Chamada máquina-a-máquina**: sem usuário autenticado, o atributo tenta resolver um tenant por
   `Authorization: Basic <base64(tenantId:apiKey)>` ou pelo header `X-Api-Key`. Resolvendo, o tenant
   é fixado no contexto e a request segue.

Isso permite que um mesmo endpoint atenda o frontend (Bearer) e uma integração servidor-a-servidor
(chave de API), sem atributo separado.

`[RequireRoot]` exige a claim `root=true` e ignora o catálogo de permissões.

`[AccessArea("chave")]` agrupa os acessos do controller por área ao publicá-los no provedor de
identidade. Os argumentos de `RequireAccess` e `AccessArea` são chaves de tradução, não texto final.

## Erros e localização

Erros de domínio são lançados como exceptions tipadas cuja `Message` é uma **chave de tradução**. O
middleware mapeia o tipo para o status HTTP e resolve a chave no catálogo de resources.

| Exception | Status |
| --- | --- |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| `ForbiddenException` | 403 |
| `BusinessRuleException` | 400 |
| `IntegrityException` | 409 |
| qualquer outra | 500 com mensagem genérica |

```csharp
throw new BusinessRuleException("opportunity.pipeline.notConfigured");
throw new NotFoundException("customer.notFound", customerId);   // args interpolam na mensagem
```

Uma chave sem entrada nos arquivos `.resx` não vira erro de build: ela cai no caminho de 500
genérico e a causa real some. Toda chave nova precisa existir nos três idiomas suportados (pt-BR,
en-US, es-AR).

O framework traz 104 mensagens próprias em `Archon.Api/Resources/Localization/`. A aplicação
registra os resources dela pelo segundo argumento de `AddArchonApi`.

O catálogo é servido para o frontend por `GET /api/localization/catalog?lang=pt-BR`, que é anônimo
de propósito: a tela de login precisa dos textos antes de existir token.

## Auditoria

O `ArchonDbContext` gera auditoria automaticamente no `SaveChanges` para as entidades rastreadas.
Cada evento registra ação (`Insert`, `Update`, `Delete`), propriedades alteradas com valor anterior
e novo, usuário, tenant, `TraceId` como correlação e a relação pai/filho quando aplicável.

A gravação da auditoria acontece na **mesma transação** da mudança: ou os dois persistem, ou nenhum.
Não existe janela em que o dado mudou e o log não registrou.

Endpoints:

- `GET /api/audit/entity/{entityName}/{entityId}` — eventos da entidade, paginado
- `GET /api/audit/GetById/{auditEntryId}` — detalhe do evento com as propriedades alteradas
- `GET /api/audit/Recent` — eventos recentes
- `GET /api/audit/Stats` — estatísticas agregadas

## Eventos de domínio

Entidades acumulam eventos e o `DbContext` os despacha **após** o commit bem-sucedido.

```csharp
public sealed record OpportunityWon(long OpportunityId, DateTimeOffset OccurredAt) : IDomainEvent;

// na entidade
AddDomainEvent(new OpportunityWon(Id, DateTimeOffset.UtcNow));

// handler: registrado automaticamente por AddArchonPersistence
public sealed class NotifyOnOpportunityWon : IDomainEventHandler<OpportunityWon>
{
    public Task HandleAsync(OpportunityWon domainEvent, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
```

Os handlers são descobertos por varredura dos assemblies passados em `AddArchonPersistence`.

## Notificações

Notificação in-app persistida por tenant, com `INotificationService` e controller pronto em
`/api/notifications`: listagem paginada, contagem de não lidas, criação, marcar como lida, marcar
todas, excluir e limpar tudo.

## Integrações e bootstrap de tenant

`IIntegrationService` lê configuração de integração (nome + parâmetros) da base do tenant, com cache.
`POST /api/tenants/bootstrap` provisiona um tenant novo: roda as migrations na base dele e semeia as
integrações iniciais. Isso substitui migração sob demanda por request, que criava corrida de schema.

## Cliente HTTP

`RestApi` é um wrapper tipado sobre `HttpClient`, com `Fetch<T>` e `FetchString`. Registrado por
`AddArchonRestApi()` e usado internamente pelos clientes do provedor de identidade.

`TemplateInterpolator` resolve `{{variavel}}` em strings, com suporte a filtro (`{{campo | json}}`) e
acesso a índice de array. Serve para montar payloads a partir de dados de execução.

## Configuração

```json
{
  "RunMigrations": false,

  "TenantDatabases": {
    "default": {
      "CompanyName": "Archon",
      "ApplicationId": "archon-app",
      "ConnectionString": "Host=localhost;Database=archon;Username=user;Password=senha",
      "DatabaseType": "PostgreSql",
      "Schema": "public",
      "ApiKey": ""
    }
  },

  "IdentityCatalog": {
    "BaseUrl": "",
    "IdMApiKey": "",
    "ApplicationId": "",
    "CacheTtl": "00:05:00",
    "NegativeCacheTtl": "00:01:00",
    "RequestTimeout": "00:00:10"
  },

  "Jwt": {
    "Issuer": "",
    "Audience": "",
    "Authority": ""
  },

  "Integration": {
    "CacheTtl": "00:05:00"
  },

  "Archon": {
    "Localization": {
      "DefaultCulture": "pt-BR",
      "SupportedCultures": ["pt-BR", "en-US", "es-AR"]
    }
  },

  "OpenTelemetry": {
    "Endpoint": ""
  }
}
```

`DatabaseType` aceita `PostgreSql`/`Postgres`, `SqlServer`/`MSSQL` e `MySql`. Valor desconhecido cai
em PostgreSQL.

`Jwt:Authority` precisa vir de configuração global, nunca da base do tenant: validar a assinatura de
um token não pode depender de já saber a qual tenant ele pertence. Vazio, o validador usa
`IdentityCatalog:BaseUrl`.

Preencher `IdentityCatalog` troca o resolver de tenant de configuração para catálogo remoto. Deixar
em branco mantém a resolução por `appsettings`.

Nunca comite credenciais reais nos arquivos de configuração.

## Migrations

FluentMigrator, com as migrations rodando por tenant.

```csharp
builder.Services.RunMigrations(builder.Configuration, "public", typeof(Program).Assembly);
```

Comportamento:

- O runner é sempre registrado no container, mesmo com `RunMigrations: false` — ele também é
  dependência do bootstrap de tenant. A flag controla apenas se as migrations rodam no startup.
- Com a flag ligada, o laço percorre **todos** os tenants configurados e não para no primeiro erro,
  para que o operador veja todas as falhas de uma vez.
- Se qualquer tenant falhar, o startup é abortado com exceção. Subir a aplicação contra um schema
  parcialmente migrado corrompe dado em silêncio; ficar fora do ar é visível na hora.

O framework traz quatro migrations próprias: tabelas de auditoria, tabelas de integração, tabela de
notificações e índice de mudanças de propriedade.

## Sync de recursos de acesso

No startup, o framework varre os endpoints anotados com `[RequireAccess]` e publica os recursos
encontrados no provedor de identidade, cada um com `Name`, `Controller`, `Action`, `HttpMethod`,
`Route` e a área declarada em `[AccessArea]`.

```csharp
await app.UseArchonAccessSyncAsync();
```

O sync roda em background após a aplicação subir, e uma falha é registrada em log sem derrubar o
processo. O endpoint esperado no provedor é `POST /api/access-resources/sync`.

## Observabilidade

```csharp
builder.Services.AddArchonOpenTelemetry(builder.Configuration, "minha-api", "1.0.0");
```

Registra traces (ASP.NET Core, HttpClient, source `Archon`) e métricas, exportando via OTLP. Se
`OpenTelemetry:Endpoint` estiver vazio, a extensão não registra nada e sai sem erro. Rotas de
health e de documentação são filtradas dos traces.

## Health check

`GET /health` responde anonimamente, sem tocar em banco. Serve para readiness probe de container.

## Testes

```bash
dotnet test Archon/Archon.Testing/Archon.Testing.csproj
```

- `Unit/`: entidades, paginação, envelope, atributos de acesso, middlewares de exception e sessão,
  multi-tenant, auditoria, `CrudService`, dispatcher de eventos e registro de migrations.
- `Integration/`: pipeline HTTP real via `TestHost`, com controller e host de apoio.

## Princípio de projeto

O framework prefere falhar alto a adivinhar. Tenant não resolvido com múltiplos tenants configurados,
migration que falhou no startup, `UseSessionValidation()` sem validador registrado: todos derrubam a
aplicação em vez de seguir em um estado ambíguo. A regra é que um erro visível no boot custa menos
que dado gravado no lugar errado.
