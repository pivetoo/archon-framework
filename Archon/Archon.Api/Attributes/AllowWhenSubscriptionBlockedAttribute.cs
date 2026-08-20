namespace Archon.Api.Attributes
{
    /// <summary>
    /// Libera o endpoint para usuario cujo token traz <c>subscription_blocked</c>.
    ///
    /// Existe para o caso do cliente inadimplente: ele precisa entrar para VER e PAGAR a
    /// assinatura, e nada mais. Sem esta marcacao o <see cref="RequireAccessAttribute"/> recusa
    /// tudo enquanto a claim estiver presente — o padrao e negar, e a excecao e explicita.
    ///
    /// Marcar apenas o que participa do pagamento. Marcar uma tela de produto aqui equivale a
    /// entregar o produto de graca a quem nao pagou.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class AllowWhenSubscriptionBlockedAttribute : Attribute
    {
    }
}
