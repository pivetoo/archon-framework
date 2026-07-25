namespace Archon.Infrastructure.IdentityManagement
{
    public sealed class JwtOptions
    {
        public string Issuer { get; init; } = string.Empty;

        public string Audience { get; init; } = string.Empty;

        /// <summary>
        /// URL do emissor OIDC usada para descobrir o JWKS. Precisa vir de configuracao GLOBAL, nunca do
        /// banco do tenant: validar um token nao pode depender de ja saber de qual tenant ele e, senao a
        /// aplicacao e obrigada a escolher um tenant a partir de um token que ainda nao verificou.
        /// Quando vazio, o validador usa IdentityCatalog:BaseUrl.
        /// </summary>
        public string Authority { get; init; } = string.Empty;
    }
}
