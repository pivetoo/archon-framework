using Archon.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Archon.Infrastructure.Persistence.EF.Configurations
{
    public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.ToTable("notifications");

            builder.HasKey(entity => entity.Id);

            builder.Property(entity => entity.Title)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(entity => entity.Message)
                .IsRequired()
                .HasMaxLength(2000);

            builder.Property(entity => entity.TenantId).HasMaxLength(100);
            builder.Property(entity => entity.Link).HasMaxLength(500);
            builder.Property(entity => entity.Source).HasMaxLength(100);
            builder.Property(entity => entity.ReferenceEntityName).HasMaxLength(200);
            builder.Property(entity => entity.ReferenceEntityId).HasMaxLength(100);

            builder.HasIndex(entity => new { entity.UserId, entity.IsRead, entity.CreatedAt })
                .HasDatabaseName("ix_notifications_userid_isread_createdat");

            builder.HasIndex(entity => new { entity.TenantId, entity.UserId })
                .HasDatabaseName("ix_notifications_tenantid_userid");
        }
    }
}
