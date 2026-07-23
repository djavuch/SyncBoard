using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SyncBoard.Entities;

namespace SyncBoard.Database.Configurations;

public class BoardConfiguration :IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> builder)
    {
        builder.ToTable("Boards");
        
        builder.HasKey(b => b.Id);
        
        builder.Property(b => b.Title)
            .IsRequired()
            .HasMaxLength(256);
        
        builder.HasOne(b => b.Owner)         
            .WithMany(u => u.Boards)         
            .HasForeignKey(b => b.OwnerId)   
            .OnDelete(DeleteBehavior.SetNull); 
    }
}