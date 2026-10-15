using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TecNM.Residency.OfficialTemplates;

public class OfficialTemplateVersionConfiguration : IEntityTypeConfiguration<OfficialTemplateVersion>
{
    public void Configure(EntityTypeBuilder<OfficialTemplateVersion> builder)
    {
        builder.ToTable("official_template_versions");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(v => v.OfficialTemplateId).HasColumnName("official_template_id").IsRequired();
        builder.Property(v => v.VersionNumber).HasColumnName("version_number").IsRequired();
        builder.Property(v => v.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
        builder.Property(v => v.FilePath).HasColumnName("file_path").HasMaxLength(500).IsRequired();
        builder.Property(v => v.FileSize).HasColumnName("file_size").IsRequired();
        builder.Property(v => v.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
        builder.Property(v => v.ChangeNotes).HasColumnName("change_notes");
        builder.Property(v => v.UploadedAt).HasColumnName("uploaded_at").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();
        builder.Property(v => v.UploadedBy).HasColumnName("uploaded_by");

        builder.Property(v => v.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(v => v.IsVisible).HasColumnName("is_visible").HasDefaultValue(true).IsRequired();
        builder.Property(v => v.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0).IsRequired();
        builder.Property(v => v.CreatedBy).HasColumnName("created_by");
        builder.Property(v => v.UpdatedBy).HasColumnName("updated_by");
        builder.Property(v => v.DeletedBy).HasColumnName("deleted_by");
        builder.Property(v => v.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();
        builder.Property(v => v.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(v => v.OfficialTemplateId).HasDatabaseName("ix_official_template_versions_template_id");
        builder.HasIndex(v => new { v.OfficialTemplateId, v.VersionNumber }).HasDatabaseName("ix_official_template_versions_version");

        builder.HasOne(v => v.OfficialTemplate)
            .WithMany(t => t.Versions)
            .HasForeignKey(v => v.OfficialTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
