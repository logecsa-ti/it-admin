namespace TIAdmin.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TIAdmin.Domain.Entities;

public class SystemConfigurationConfiguration : IEntityTypeConfiguration<SystemConfiguration>
{
    public void Configure(EntityTypeBuilder<SystemConfiguration> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SystemConfigurations");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Key).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Value).HasMaxLength(4000);
        builder.Property(c => c.DefaultValue).HasMaxLength(4000);
        builder.Property(c => c.Group).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.DataType).HasConversion<int>();

        builder.HasIndex(c => c.Key).IsUnique().HasDatabaseName("IX_SystemConfigurations_Key");
        builder.HasIndex(c => c.Group).HasDatabaseName("IX_SystemConfigurations_Group");
    }
}
