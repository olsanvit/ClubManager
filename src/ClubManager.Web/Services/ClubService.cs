using ClubManager.Data;
using ClubManager.Models;
using Microsoft.EntityFrameworkCore;

namespace ClubManager.Services;

public class ClubService
{
    private readonly IDbContextFactory<AppDbContextClubManager> _factory;

    public ClubService(IDbContextFactory<AppDbContextClubManager> factory) => _factory = factory;

    // AUDIT:OK
    public async Task<List<Organization>> GetOrganizationsAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Organizations.Where(o => o.IsActive).OrderBy(o => o.Name).ToListAsync();
    }

    // AUDIT:OK
    public async Task<List<Club>> GetClubsAsync(int organizationId)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Clubs
            .Where(c => c.OrganizationId == organizationId && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    // AUDIT:OK
    public async Task<List<OrganizationMember>> GetMembersAsync(int organizationId, int? clubId = null)
    {
        await using var db = _factory.CreateDbContext();
        if (clubId.HasValue)
        {
            return await db.ClubMembers
                .Include(cm => cm.OrganizationMember).ThenInclude(om => om.User)
                .Where(cm => cm.ClubId == clubId && cm.IsActive)
                .Select(cm => cm.OrganizationMember)
                .OrderBy(m => m.DisplayName ?? m.User.UserName)
                .ToListAsync();
        }

        return await db.OrganizationMembers
            .Include(m => m.User)
            .Where(m => m.OrganizationId == organizationId && m.IsActive)
            .OrderBy(m => m.DisplayName ?? m.User.UserName)
            .ToListAsync();
    }

    // AUDIT:CRITICAL|Kritický|Chybí duplicity check – uživatel přidatelný vícekrát do organizace
    public async Task<OrganizationMember> AddMemberAsync(int organizationId, string userId, OrgRole role = OrgRole.Member, string? displayName = null)
    {
        await using var db = _factory.CreateDbContext();
        var member = new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = role,
            DisplayName = displayName
        };
        db.OrganizationMembers.Add(member);
        await db.SaveChangesAsync();
        return member;
    }

    // AUDIT:OK
    public async Task AddToClubAsync(int orgMemberId, int clubId)
    {
        await using var db = _factory.CreateDbContext();
        if (!await db.ClubMembers.AnyAsync(cm => cm.OrganizationMemberId == orgMemberId && cm.ClubId == clubId))
        {
            db.ClubMembers.Add(new ClubMember { ClubId = clubId, OrganizationMemberId = orgMemberId });
            await db.SaveChangesAsync();
        }
    }

    // AUDIT:OK
    public async Task LinkFamilyAsync(int organizationId, string parentUserId, string childUserId)
    {
        await using var db = _factory.CreateDbContext();
        if (!await db.FamilyLinks.AnyAsync(f => f.OrganizationId == organizationId && f.ParentUserId == parentUserId && f.ChildUserId == childUserId))
        {
            db.FamilyLinks.Add(new FamilyLink { OrganizationId = organizationId, ParentUserId = parentUserId, ChildUserId = childUserId });
            await db.SaveChangesAsync();
        }
    }

    // AUDIT:PENDING|Nízký|3 round-tripy do DB místo Task.WhenAll
    public async Task<(int Orgs, int Clubs, int Members)> GetSummaryAsync()
    {
        await using var db = _factory.CreateDbContext();
        var orgs = await db.Organizations.CountAsync(o => o.IsActive);
        var clubs = await db.Clubs.CountAsync(c => c.IsActive);
        var members = await db.OrganizationMembers.CountAsync(m => m.IsActive);
        return (orgs, clubs, members);
    }
}
