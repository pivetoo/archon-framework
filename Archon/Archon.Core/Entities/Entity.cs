namespace Archon.Core.Entities
{
    public abstract class Entity
    {
        public long Id { get; protected set; }

        public DateTimeOffset CreatedAt { get; protected set; }

        public DateTimeOffset? UpdatedAt { get; protected set; }

        public void SetCreatedAt(DateTimeOffset createdAt)
        {
            CreatedAt = createdAt;
            UpdatedAt = createdAt;
        }

        public void SetUpdatedAt(DateTimeOffset updatedAt)
        {
            UpdatedAt = updatedAt;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not Entity other)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (GetType() != other.GetType())
            {
                return false;
            }

            if (Id == default || other.Id == default)
            {
                return false;
            }

            return Id == other.Id;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(GetType(), Id);
        }

        public static bool operator ==(Entity? left, Entity? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Entity? left, Entity? right)
        {
            return !Equals(left, right);
        }
    }
}
