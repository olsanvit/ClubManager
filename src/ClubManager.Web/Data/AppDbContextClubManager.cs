using ClubManager.Models;
using MercenariesAndBeasts.Infrastructure;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ClubManager.Data;

public class AppDbContextClubManager : IdentityDbContext<AppUser>
{
    public AppDbContextClubManager(DbContextOptions<AppDbContextClubManager> options)
        : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<ClubMember> ClubMembers => Set<ClubMember>();
    public DbSet<FamilyLink> FamilyLinks => Set<FamilyLink>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageRecipient> MessageRecipients => Set<MessageRecipient>();
    public DbSet<Car> Cars => Set<Car>();
    public DbSet<CarReservation> CarReservations => Set<CarReservation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Organization>(e =>
        {
            e.HasIndex(x => x.Name);
        });

        builder.Entity<Club>(e =>
        {
            e.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
        });

        builder.Entity<OrganizationMember>(e =>
        {
            e.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique();
        });

        builder.Entity<ClubMember>(e =>
        {
            e.HasIndex(x => new { x.ClubId, x.OrganizationMemberId }).IsUnique();
        });

        builder.Entity<FamilyLink>(e =>
        {
            e.HasIndex(x => new { x.OrganizationId, x.ParentUserId, x.ChildUserId }).IsUnique();
            e.HasOne(x => x.ParentUser).WithMany().HasForeignKey(x => x.ParentUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ChildUser).WithMany().HasForeignKey(x => x.ChildUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MessageRecipient>(e =>
        {
            e.HasIndex(x => new { x.MessageId, x.UserId }).IsUnique();
        });

        builder.Entity<CarReservation>(e =>
        {
            e.Property(x => x.KmAtStart).HasPrecision(10, 2);
            e.Property(x => x.KmAtEnd).HasPrecision(10, 2);
        });
    }
}
