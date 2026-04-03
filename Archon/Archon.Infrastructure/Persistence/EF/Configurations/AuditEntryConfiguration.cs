using Archon.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Archon.Infrastructure.Persistence.EF.Configurations
{
    public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
    {
        public void Configure(EntityTypeBuilder<AuditEntry> builder)
        {
            builder.ToTable("AuditEntries");

            builder.HasKey(entry => entry.Id);

            builder.Property(entry => entry.EntityName)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(entry => entry.EntityId)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(entry => entry.TenantId)
                .HasMaxLength(100);

            builder.Property(entry => entry.ChangedBy)
                .HasMaxLength(100);

            builder.Property(entry => entry.CorrelationId)
                .HasMaxLength(100);

            builder.Property(entry => entry.ParentEntityName)
                .HasMaxLength(200);

            builder.Property(entry => entry.ParentEntityId)
                .HasMaxLength(100);

            builder.Property(entry => entry.Source)
                .HasMaxLength(100);

            builder.HasMany(entry => entry.PropertyChanges)
                .WithOne(change => change.AuditEntry)
                .HasForeignKey(change => change.AuditEntryId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(entry => new { entry.EntityName, entry.EntityId, entry.ChangedAt });
            builder.HasIndex(entry => entry.CorrelationId);
        }
    }
}
