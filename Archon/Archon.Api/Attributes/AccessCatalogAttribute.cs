namespace Archon.Api.Attributes
{
    /// <summary>
    /// Declarado uma vez no assembly da API para ordenar o catalogo de capacidades exibido ao
    /// administrador e para marcar as capacidades basicas, que todo perfil recebe automaticamente.
    /// Modulos e verbos fora das listas vao para o fim, em ordem alfabetica.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class AccessCatalogAttribute : Attribute
    {
        public string[] Modules { get; set; } = [];

        public string[] Verbs { get; set; } = [];

        public string[] Baseline { get; set; } = [];
    }
}
