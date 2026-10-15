using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TecNM.Residency.OfficialTemplates;

public class OfficialTemplateConfiguration : IEntityTypeConfiguration<OfficialTemplate>
{
    public void Configure(EntityTypeBuilder<OfficialTemplate> builder)
    {
        builder.ToTable("official_templates");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(t => t.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(t => t.Description).HasColumnName("description");
        builder.Property(t => t.Category).HasColumnName("category").HasMaxLength(100).HasDefaultValue("General").IsRequired();
        builder.Property(t => t.CurrentVersion).HasColumnName("current_version").HasDefaultValue(1).IsRequired();
        builder.Property(t => t.CurrentFileName).HasColumnName("current_file_name").HasMaxLength(255).IsRequired();
        builder.Property(t => t.CurrentFilePath).HasColumnName("current_file_path").HasMaxLength(500).IsRequired();
        builder.Property(t => t.CurrentFileSize).HasColumnName("current_file_size").HasDefaultValue(0L).IsRequired();
        builder.Property(t => t.CurrentContentType).HasColumnName("current_content_type").HasMaxLength(100).HasDefaultValue("application/pdf").IsRequired();

        builder.Property(t => t.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(t => t.IsVisible).HasColumnName("is_visible").HasDefaultValue(true).IsRequired();
        builder.Property(t => t.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0).IsRequired();
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");
        builder.Property(t => t.DeletedBy).HasColumnName("deleted_by");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();
        builder.Property(t => t.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(t => t.Code).HasDatabaseName("ix_official_templates_code");
        builder.HasIndex(t => t.IsActive).HasDatabaseName("ix_official_templates_is_active");
        builder.HasIndex(t => t.Category).HasDatabaseName("ix_official_templates_category");

        builder.HasMany(t => t.Versions)
            .WithOne(v => v.OfficialTemplate)
            .HasForeignKey(v => v.OfficialTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
