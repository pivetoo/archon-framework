using Archon.Core.Entities;

namespace Archon.Testing.Unit.Core.Entities
{
    public sealed class EntityTests
    {
        [Test]
        public void Equals_ShouldReturnTrue_ForSameTypeAndSameId()
        {
            TestEntity left = new TestEntity(10);
            TestEntity right = new TestEntity(10);

            Assert.That(left.Equals(right), Is.True);
            Assert.That(left == right, Is.True);
        }

        [Test]
        public void Equals_ShouldReturnFalse_WhenIdIsDefault()
        {
            TestEntity left = new TestEntity();
            TestEntity right = new TestEntity();

            Assert.That(left.Equals(right), Is.False);
            Assert.That(left == right, Is.False);
        }

        [Test]
        public void SetCreatedAt_ShouldAlsoSetUpdatedAt()
        {
            TestEntity entity = new TestEntity();
            DateTimeOffset createdAt = DateTimeOffset.UtcNow;

            entity.SetCreatedAt(createdAt);

            Assert.That(entity.CreatedAt, Is.EqualTo(createdAt));
            Assert.That(entity.UpdatedAt, Is.EqualTo(createdAt));
        }

        private sealed class TestEntity : Entity
        {
            public TestEntity()
            {
            }

            public TestEntity(long id)
            {
                Id = id;
            }
        }
    }
}
