using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SyncBoard.Entities;

namespace SyncBoard.Database.Configurations;

public class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.ToTable("Cards");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Title)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasMaxLength(1000);

        builder.Property(c => c.Position)
            .IsRequired();
        
        builder.HasOne(c => c.Column)
            .WithMany(col => col.Cards)
            .HasForeignKey(c => c.ColumnId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(c => c.CreatedBy)       
            .WithMany(u => u.Cards)            
            .HasForeignKey(c => c.CreatedById) 
            .OnDelete(DeleteBehavior.SetNull);
    }
}