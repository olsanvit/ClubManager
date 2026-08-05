using ClubManager.Data;
using ClubManager.Models;
using MercenariesAndBeasts.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClubManager.Services;

public class InvitationService(
    IDbContextFactory<AppDbContextClubManager> factory,
    UserManager<AppUser> userManager,
    ClubNotificationService notifier)
{
    public async Task<Invitation> CreateInvitationAsync(string email, int clubId, OrgRole role, string invitedByUserId)
    {
        await using var db = factory.CreateDbContext();
        var inv = new Invitation
        {
            Email = email,
            ClubId = clubId,
            Role = role,
            InvitedByUserId = invitedByUserId,
            Token = Guid.NewGuid().ToString()
        };
        db.Invitations.Add(inv);
        await db.SaveChangesAsync();

        var club = await db.Clubs.Include(c => c.Organization).FirstOrDefaultAsync(c => c.Id == clubId);
        if (club is not null)
        {
            var link = $"/accept-invite?token={inv.Token}";
            await notifier.SendEmailAsync(email, email, $"Pozvánka do oddílu {club.Name}",
                $"<p>Byl(a) jste pozván(a) do oddílu <strong>{club.Name}</strong> ({club.Organization.Name}).</p>" +
                $"<p><a href='{link}'>Přijmout pozvánku</a></p><p>Platnost: 7 dní.</p>");
        }

        return inv;
    }

    public async Task<Invitation?> GetValidByTokenAsync(string token)
    {
        await using var db = factory.CreateDbContext();
        return await db.Invitations
            .Include(i => i.Club).ThenInclude(c => c.Organization)
            .FirstOrDefaultAsync(i => i.Token == token && i.AcceptedAt == null && i.ExpiresAt > DateTime.UtcNow);
    }

    public async Task<bool> AcceptAsync(string token, string userId)
    {
        await using var db = factory.CreateDbContext();
        var inv = await db.Invitations
            .Include(i => i.Club)
            .FirstOrDefaultAsync(i => i.Token == token && i.AcceptedAt == null && i.ExpiresAt > DateTime.UtcNow);
        if (inv is null) return false;

        inv.AcceptedAt = DateTime.UtcNow;

        // Přidej do OrganizationMembers pokud ještě není
        var orgMember = await db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == inv.Club.OrganizationId);

        if (orgMember is null)
        {
            orgMember = new OrganizationMember
            {
                UserId = userId,
                OrganizationId = inv.Club.OrganizationId,
                Role = inv.Role
            };
            db.OrganizationMembers.Add(orgMember);
            await db.SaveChangesAsync();
        }

        // Přidej do ClubMembers pokud ještě není
        var clubMember = await db.ClubMembers
            .FirstOrDefaultAsync(m => m.OrganizationMemberId == orgMember.Id && m.ClubId == inv.ClubId);
        if (clubMember is null)
        {
            db.ClubMembers.Add(new ClubMember { ClubId = inv.ClubId, OrganizationMemberId = orgMember.Id });
        }

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> JoinByCodeAsync(string joinCode, string userId)
    {
        await using var db = factory.CreateDbContext();
        var club = await db.Clubs
            .Include(c => c.Organization)
            .FirstOrDefaultAsync(c => c.JoinCode == joinCode.ToUpper() && c.IsActive);
        if (club is null) return false;

        var orgMember = await db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == club.OrganizationId);
        if (orgMember is null)
        {
            orgMember = new OrganizationMember { UserId = userId, OrganizationId = club.OrganizationId, Role = OrgRole.Member };
            db.OrganizationMembers.Add(orgMember);
            await db.SaveChangesAsync();
        }

        var exists = await db.ClubMembers
            .AnyAsync(m => m.OrganizationMemberId == orgMember.Id && m.ClubId == club.Id);
        if (!exists)
        {
            db.ClubMembers.Add(new ClubMember { ClubId = club.Id, OrganizationMemberId = orgMember.Id });
            await db.SaveChangesAsync();
        }

        return true;
    }
}
