using MicroExercise.Core;
using MicroExercise.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MicroExercise.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        // Seed the MVP demo user (auto-signed-in; see spec §2 Authentication).
        builder.HasData(new User
        {
            Id = AppDefaults.DemoUserId,
            Email = AppDefaults.DemoUserEmail,
            DisplayName = AppDefaults.DemoUserDisplayName,
            CreatedAt = SeedDefaults.Timestamp
        });
    }
}
