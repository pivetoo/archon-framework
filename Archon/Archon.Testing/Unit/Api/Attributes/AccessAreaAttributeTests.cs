using Archon.Api.Attributes;

namespace Archon.Testing.Unit.Api.Attributes
{
    [TestFixture]
    public sealed class AccessAreaAttributeTests
    {
        [Test]
        public void Constructor_should_store_description()
        {
            AccessAreaAttribute attribute = new AccessAreaAttribute("agencySettings.area");

            Assert.That(attribute.Description, Is.EqualTo("agencySettings.area"));
        }

        [Test]
        public void Constructor_should_accept_literal_text()
        {
            AccessAreaAttribute attribute = new AccessAreaAttribute("Configurações da agência");

            Assert.That(attribute.Description, Is.EqualTo("Configurações da agência"));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void Constructor_should_reject_blank_description(string description)
        {
            Assert.Throws<ArgumentException>(() => new AccessAreaAttribute(description));
        }

        [Test]
        public void Constructor_should_reject_null_description()
        {
            Assert.Throws<ArgumentNullException>(() => new AccessAreaAttribute(null!));
        }

        [Test]
        public void AttributeUsage_should_target_class_only_and_not_inherit()
        {
            AttributeUsageAttribute? usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(typeof(AccessAreaAttribute), typeof(AttributeUsageAttribute));

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage!.ValidOn, Is.EqualTo(AttributeTargets.Class));
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.Inherited, Is.False);
        }
    }
}
