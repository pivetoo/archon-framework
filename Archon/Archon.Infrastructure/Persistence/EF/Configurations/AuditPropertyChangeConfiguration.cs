using Archon.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Archon.Infrastructure.Persistence.EF.Configurations
{
    public sealed class AuditPropertyChangeConfiguration : IEntityTypeConfiguration<AuditPropertyChange>
    {
        public void Configure(EntityTypeBuilder<AuditPropertyChange> builder)
        {
            builder.ToTable("AuditPropertyChanges");

            builder.HasKey(change => change.Id);

            builder.Property(change => change.PropertyName)
                .IsRequired()
                .HasMaxLength(200);
        }
    }
}
