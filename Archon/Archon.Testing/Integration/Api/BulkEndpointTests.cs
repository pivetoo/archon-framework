using Archon.Api.Contracts.Bulk;
using Archon.Application.Services;
using Archon.Infrastructure.Services;
using Archon.Testing.Integration.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Archon.Testing.Integration.Api
{
    public sealed class BulkEndpointTests
    {
        private static Task<WebApplication> CreateHostAsync()
        {
            // O host de teste nao registra persistencia; o executor de lote so precisa do DbContext para
            // limpar o rastreador entre registros, entao um contexto vazio em memoria basta.
            return TestApiHost.CreateAsync(services =>
            {
                services.AddScoped<DbContext>(_ => new DbContext(new DbContextOptionsBuilder().UseSqlite("DataSource=:memory:").Options));
                services.AddScoped<IBulkOperationRunner, BulkOperationRunner>();
            });
        }

        private static async Task<(HttpStatusCode Status, JsonResultModel Body)> DeleteManyAsync(HttpClient client, IEnumerable<long> ids)
        {
            using HttpRequestMessage request = new(HttpMethod.Delete, "/api/testapi/bulk")
            {
                Content = JsonContent.Create(new { ids })
            };
            request.Headers.AcceptLanguage.ParseAdd("pt-BR");

            HttpResponseMessage response = await client.SendAsync(request);
            JsonResultModel? body = await response.Content.ReadFromJsonAsync<JsonResultModel>();
            return (response.StatusCode, body!);
        }

        [Test]
        public async Task Bulk_ShouldReturnPerRecordResult_WithLocalizedFailures()
        {
            await using WebApplication app = await CreateHostAsync();
            HttpClient client = app.GetTestClient();

            (HttpStatusCode status, JsonResultModel body) = await DeleteManyAsync(client, [1, 2, 3, 1]);

            Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body.Message, Is.EqualTo("1 de 3 registro(s) processado(s). 2 com erro."));

            JsonElement data = body.Data!.Value;
            Assert.That(data.GetProperty("total").GetInt32(), Is.EqualTo(3));
            Assert.That(data.GetProperty("succeeded").GetInt32(), Is.EqualTo(1));
            Assert.That(data.GetProperty("failed").GetInt32(), Is.EqualTo(2));
            Assert.That(data.GetProperty("succeededIds").EnumerateArray().Select(item => item.GetInt64()), Is.EqualTo(new long[] { 1 }));

            Dictionary<long, string> failures = data.GetProperty("failures").EnumerateArray()
                .ToDictionary(item => item.GetProperty("id").GetInt64(), item => item.GetProperty("message").GetString()!);
            Assert.That(failures[2], Is.EqualTo("Registro não encontrado."), "chave do catalogo vem traduzida");
            Assert.That(failures[3], Is.EqualTo("Erro inesperado."), "texto que nao e chave nunca vaza para o cliente");
        }

        [Test]
        public async Task Bulk_ShouldSummarizeFullSuccess()
        {
            await using WebApplication app = await CreateHostAsync();
            HttpClient client = app.GetTestClient();

            (HttpStatusCode status, JsonResultModel body) = await DeleteManyAsync(client, [1, 4]);

            Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body.Message, Is.EqualTo("2 registro(s) processado(s)."));
        }

        [TestCase(new long[0], "Informe ao menos um registro.")]
        [TestCase(new long[] { 1, 0 }, "A lista de registros contém um identificador inválido.")]
        public async Task Bulk_ShouldRejectInvalidIdLists(long[] ids, string expectedMessage)
        {
            await using WebApplication app = await CreateHostAsync();
            HttpClient client = app.GetTestClient();

            (HttpStatusCode status, JsonResultModel body) = await DeleteManyAsync(client, ids);

            Assert.That(status, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(body.Message, Is.EqualTo(expectedMessage));
        }

        [Test]
        public async Task Bulk_ShouldRejectMoreThanTheLimit()
        {
            await using WebApplication app = await CreateHostAsync();
            HttpClient client = app.GetTestClient();

            (HttpStatusCode status, JsonResultModel body) = await DeleteManyAsync(client, Enumerable.Range(1, BulkIdsRequest.MaxItems + 1).Select(id => (long)id));

            Assert.That(status, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(body.Message, Is.EqualTo($"Selecione no máximo {BulkIdsRequest.MaxItems} registros por vez."));
        }

        private sealed class JsonResultModel
        {
            public string Message { get; init; } = string.Empty;

            public JsonElement? Data { get; init; }
        }
    }
}
