using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Profiles.Infrastructure.Persistence.EFC.Configuration;

public static class ProfileConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Profile>(entity =>
        {
            entity.ToTable("profiles");
            entity.HasKey(profile => profile.Id);
            entity.HasIndex(profile => profile.UserId).IsUnique();
            entity.Property(profile => profile.Name).HasMaxLength(200).IsRequired();
            entity.Property(profile => profile.Username).HasMaxLength(100).IsRequired();
            entity.Property(profile => profile.PhoneNumber).HasMaxLength(50);
            entity.Property(profile => profile.Address).HasMaxLength(255);
            entity.Property(profile => profile.SecondEmail).HasMaxLength(150);
            entity.Property(profile => profile.Age);
            entity.Property(profile => profile.YearsInBusiness);
            entity.Property(profile => profile.PhotoReference).HasMaxLength(128);
            entity.Property(profile => profile.CloudinaryReference).HasColumnType("longtext");
            entity.Property(profile => profile.PhotoUrl).HasColumnType("longtext");
        });
    }
}
