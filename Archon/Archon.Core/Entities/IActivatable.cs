namespace Archon.Core.Entities
{
    /// <summary>
    /// Entidade que pode ser ativada e inativada sem passar pelo update completo. As regras do
    /// proprio registro (ex.: registro do sistema que nao pode mudar) moram em Activate/Deactivate e
    /// sao lancadas com chave i18n crua, como qualquer regra de dominio.
    /// </summary>
    public interface IActivatable
    {
        bool IsActive { get; }

        void Activate();

        void Deactivate();
    }
}
