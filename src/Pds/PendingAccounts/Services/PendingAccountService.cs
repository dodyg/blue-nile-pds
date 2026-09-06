using BlueNilePds.Pds.AccountManager;
using BlueNilePds.Pds.AccountManager.Db;
using Microsoft.EntityFrameworkCore;
using BlueNilePds.Pds.PendingAccounts.Models;
using Scrypt;

namespace BlueNilePds.Pds.PendingAccounts.Services;

public class PendingAccountService
{
    private readonly PendingAccountsDb _pendingDb;
    private readonly AccountManagerDb _accountDb;
    private readonly AccountRepository _accountRepository;
    private readonly InviteStore _inviteStore;
    private readonly PendingJwtService _jwtService;
    private readonly PendingEmailTokenStore _emailTokenStore;
    private readonly string _serviceDid;

    public PendingAccountService(
        PendingAccountsDb pendingDb,
        AccountManagerDb accountDb,
        AccountRepository accountRepository,
        InviteStore inviteStore,
        PendingJwtService jwtService,
        PendingEmailTokenStore emailTokenStore,
        string serviceDid)
    {
        _pendingDb = pendingDb;
        _accountDb = accountDb;
        _accountRepository = accountRepository;
        _inviteStore = inviteStore;
        _jwtService = jwtService;
        _emailTokenStore = emailTokenStore;
        _serviceDid = serviceDid;
    }

    public async Task<RegisterResult> RegisterAsync(string email, string handle, string password, string? inviteCode, string? location = null, string? accountType = null)
    {
        var emailLower = email.ToLower();

        if (await _accountDb.Accounts.AnyAsync(a => a.Email == emailLower))
            return new RegisterResult(false, "Email already in use", null, null);

        if (await _accountDb.Actors.AnyAsync(a => a.Handle == handle))
            return new RegisterResult(false, "Handle not available", null, null);

        if (await _pendingDb.PendingRegistrations.AnyAsync(r => r.Email == emailLower && r.Status == PendingRegistrationStatus.Pending))
            return new RegisterResult(false, "Email already registered", null, null);

        if (await _pendingDb.PendingRegistrations.AnyAsync(r => r.Handle == handle && r.Status == PendingRegistrationStatus.Pending))
            return new RegisterResult(false, "Handle not available", null, null);

        if (inviteCode != null)
        {
            try
            {
                await _inviteStore.EnsureInviteIsAvailableAsync(inviteCode);
            }
            catch
            {
                return new RegisterResult(false, "Invalid invite code", null, null);
            }
        }

        var enc = new ScryptEncoder();
        var passwordHash = enc.Encode(password);
        var now = DateTime.UtcNow;

        var registration = new PendingRegistration
        {
            Email = emailLower,
            Handle = handle,
            PasswordScrypt = passwordHash,
            InviteCode = inviteCode,
            Status = PendingRegistrationStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
        _pendingDb.PendingRegistrations.Add(registration);
        await _pendingDb.SaveChangesAsync();

        var profile = new PendingProfile
        {
            PendingRegistrationId = registration.Id,
            Location = location,
            AccountType = accountType,
            CreatedAt = now,
            UpdatedAt = now
        };
        _pendingDb.PendingProfiles.Add(profile);
        await _pendingDb.SaveChangesAsync();

        var tokens = _jwtService.CreateTokens(registration.Id);
        return new RegisterResult(true, null, tokens.AccessToken, tokens.RefreshToken);
    }

    public async Task<LoginResult> LoginAsync(string identifier, string password)
    {
        var identifierLower = identifier.ToLower();

        var registration = await _pendingDb.PendingRegistrations
            .FirstOrDefaultAsync(r =>
                (r.Email == identifierLower || r.Handle == identifierLower) &&
                r.Status == PendingRegistrationStatus.Pending);

        if (registration == null)
            return new LoginResult(false, "Account not found", null, null, null);

        var enc = new ScryptEncoder();
        if (!enc.Compare(password, registration.PasswordScrypt))
            return new LoginResult(false, "Invalid password", null, null, null);

        var tokens = _jwtService.CreateTokens(registration.Id);
        return new LoginResult(true, null, tokens.AccessToken, tokens.RefreshToken, "pending");
    }

    public async Task<(string AccessToken, string RefreshToken)?> RefreshAsync(string refreshToken)
    {
        var data = _jwtService.ValidateRefreshToken(refreshToken);
        if (data == null) return null;

        var registration = await _pendingDb.PendingRegistrations.FindAsync(data.PendingRegistrationId);
        if (registration == null || registration.Status != PendingRegistrationStatus.Pending)
            return null;

        return _jwtService.CreateTokens(registration.Id);
    }

    public async Task<PendingProfileData?> GetProfileAsync(int pendingRegistrationId)
    {
        var profile = await _pendingDb.PendingProfiles
            .Include(p => p.PendingRegistration)
            .FirstOrDefaultAsync(p => p.PendingRegistrationId == pendingRegistrationId);

        if (profile == null) return null;

        return new PendingProfileData
        {
            Email = profile.PendingRegistration.Email,
            Handle = profile.PendingRegistration.Handle,
            DisplayName = profile.DisplayName,
            Description = profile.Description,
            AvatarBlobRef = profile.AvatarBlobRef,
            Location = profile.Location,
            AccountType = profile.AccountType,
            Status = profile.PendingRegistration.Status.ToString(),
            CreatedAt = profile.PendingRegistration.CreatedAt,
            EmailConfirmed = profile.PendingRegistration.EmailConfirmedAt != null
        };
    }

    public async Task<bool> UpdateProfileAsync(int pendingRegistrationId, UpdatePendingProfileRequest request)
    {
        var profile = await _pendingDb.PendingProfiles
            .FirstOrDefaultAsync(p => p.PendingRegistrationId == pendingRegistrationId);

        if (profile == null) return false;

        if (request.DisplayName != null) profile.DisplayName = request.DisplayName;
        if (request.Description != null) profile.Description = request.Description;
        if (request.AvatarBlobRef != null) profile.AvatarBlobRef = request.AvatarBlobRef;
        if (request.Location != null) profile.Location = request.Location;
        if (request.AccountType != null) profile.AccountType = request.AccountType;
        profile.UpdatedAt = DateTime.UtcNow;

        await _pendingDb.SaveChangesAsync();
        return true;
    }

    public async Task<AdminListResult> ListPendingAsync(string? cursor, int limit = 20)
    {
        IQueryable<PendingRegistration> query = _pendingDb.PendingRegistrations
            .Where(r => r.Status == PendingRegistrationStatus.Pending)
            .OrderBy(r => r.Id);

        if (cursor != null && int.TryParse(cursor, out var cursorId))
            query = query.Where(r => r.Id > cursorId);

        var registrations = await query.Take(limit + 1).ToListAsync();
        var hasMore = registrations.Count > limit;
        if (hasMore) registrations.RemoveAt(registrations.Count - 1);

        var profiles = await _pendingDb.PendingProfiles
            .Where(p => registrations.Select(r => r.Id).Contains(p.PendingRegistrationId))
            .ToListAsync();

        var items = registrations.Select(r =>
        {
            var p = profiles.FirstOrDefault(x => x.PendingRegistrationId == r.Id);
            return new PendingAdminViewItem
            {
                Id = r.Id,
                Email = r.Email,
                Handle = r.Handle,
                Location = p?.Location,
                AccountType = p?.AccountType,
                DisplayName = p?.DisplayName,
                Description = p?.Description,
                InviteCode = r.InviteCode,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                EmailConfirmed = r.EmailConfirmedAt != null
            };
        }).ToList();

        return new AdminListResult(items, hasMore ? registrations.Last().Id.ToString() : null);
    }

    public async Task<PendingAdminDetailView?> GetAdminDetailViewAsync(int id)
    {
        var registration = await _pendingDb.PendingRegistrations.FindAsync(id);
        if (registration == null) return null;

        var profile = await _pendingDb.PendingProfiles
            .FirstOrDefaultAsync(p => p.PendingRegistrationId == id);

        return new PendingAdminDetailView
        {
            Id = registration.Id,
            Email = registration.Email,
            Handle = registration.Handle,
            Status = registration.Status.ToString(),
            DisplayName = profile?.DisplayName,
            Description = profile?.Description,
            Location = profile?.Location,
            AccountType = profile?.AccountType,
            InviteCode = registration.InviteCode,
            EmailConfirmed = registration.EmailConfirmedAt != null,
            CreatedAt = registration.CreatedAt,
            UpdatedAt = registration.UpdatedAt,
        };
    }

    public async Task<ApproveResult> ApproveAsync(int pendingRegistrationId)
    {
        var registration = await _pendingDb.PendingRegistrations
            .FirstOrDefaultAsync(r => r.Id == pendingRegistrationId);

        if (registration == null)
            return new ApproveResult(false, "Pending registration not found");

        if (registration.Status != PendingRegistrationStatus.Pending)
            return new ApproveResult(false, "Registration already processed");

        registration.Status = PendingRegistrationStatus.Approved;
        registration.EmailConfirmedAt = DateTime.UtcNow;
        registration.ReviewedAt = DateTime.UtcNow;
        registration.UpdatedAt = DateTime.UtcNow;
        await _pendingDb.SaveChangesAsync();

        return new ApproveResult(true, null);
    }

    public async Task<RejectResult> RejectAsync(int pendingRegistrationId, string? reason)
    {
        var registration = await _pendingDb.PendingRegistrations
            .FirstOrDefaultAsync(r => r.Id == pendingRegistrationId);

        if (registration == null)
            return new RejectResult(false, "Pending registration not found");

        if (registration.Status != PendingRegistrationStatus.Pending)
            return new RejectResult(false, "Registration already processed");

        var profile = await _pendingDb.PendingProfiles
            .FirstOrDefaultAsync(p => p.PendingRegistrationId == pendingRegistrationId);

        if (profile != null)
            _pendingDb.PendingProfiles.Remove(profile);

        _pendingDb.PendingRegistrations.Remove(registration);
        await _pendingDb.SaveChangesAsync();

        return new RejectResult(true, null);
    }

    public async Task<PendingRegistration?> GetRegistrationAsync(int id)
    {
        return await _pendingDb.PendingRegistrations.FindAsync(id);
    }

    public async Task DeleteRegistrationAsync(int id)
    {
        var reg = await _pendingDb.PendingRegistrations.FindAsync(id);
        if (reg == null) return;

        var profile = await _pendingDb.PendingProfiles
            .FirstOrDefaultAsync(p => p.PendingRegistrationId == id);
        if (profile != null) _pendingDb.PendingProfiles.Remove(profile);

        _pendingDb.PendingRegistrations.Remove(reg);
        await _pendingDb.SaveChangesAsync();
    }

    public async Task<InviteValidationResult> ValidateInviteCodeAsync(string code)
    {
        try
        {
            await _inviteStore.EnsureInviteIsAvailableAsync(code);
            return new InviteValidationResult(true, null);
        }
        catch (Exception ex)
        {
            return new InviteValidationResult(false, ex.Message);
        }
    }

    public async Task<string?> RequestEmailConfirmationAsync(int pendingRegistrationId)
    {
        var registration = await _pendingDb.PendingRegistrations.FindAsync(pendingRegistrationId);
        if (registration == null || registration.Status != PendingRegistrationStatus.Pending)
            return null;

        if (string.IsNullOrWhiteSpace(registration.Email))
            return null;

        var token = await _emailTokenStore.CreateTokenAsync(pendingRegistrationId, PendingEmailTokenPurpose.confirm_email);
        return token;
    }

    public async Task<bool> ConfirmEmailAsync(int pendingRegistrationId, string token)
    {
        var registration = await _pendingDb.PendingRegistrations.FindAsync(pendingRegistrationId);
        if (registration == null || registration.Status != PendingRegistrationStatus.Pending)
            return false;

        await _emailTokenStore.AssertValidTokenAsync(pendingRegistrationId, token, PendingEmailTokenPurpose.confirm_email);
        registration.EmailConfirmedAt = DateTime.UtcNow;
        await _pendingDb.SaveChangesAsync();
        return true;
    }

    public async Task<string?> RequestEmailUpdateAsync(int pendingRegistrationId)
    {
        var registration = await _pendingDb.PendingRegistrations.FindAsync(pendingRegistrationId);
        if (registration == null || registration.Status != PendingRegistrationStatus.Pending)
            return null;

        if (string.IsNullOrWhiteSpace(registration.Email))
            return null;

        var token = await _emailTokenStore.CreateTokenAsync(pendingRegistrationId, PendingEmailTokenPurpose.update_email);
        return token;
    }

    public Task AssertValidEmailUpdateTokenAsync(int pendingRegistrationId, string token)
        => _emailTokenStore.AssertValidTokenAsync(pendingRegistrationId, token, PendingEmailTokenPurpose.update_email);

    public async Task<bool> UpdateEmailAsync(int pendingRegistrationId, string newEmail)
    {
        var registration = await _pendingDb.PendingRegistrations.FindAsync(pendingRegistrationId);
        if (registration == null || registration.Status != PendingRegistrationStatus.Pending)
            return false;

        var emailLower = newEmail.ToLower();

        if (await _accountDb.Accounts.AnyAsync(a => a.Email == emailLower))
            return false;

        if (await _pendingDb.PendingRegistrations.AnyAsync(r => r.Email == emailLower && r.Status == PendingRegistrationStatus.Pending && r.Id != pendingRegistrationId))
            return false;

        registration.Email = emailLower;
        registration.EmailConfirmedAt = null;
        await _pendingDb.SaveChangesAsync();
        return true;
    }
}

public record RegisterResult(bool Success, string? Error, string? AccessToken, string? RefreshToken);
public record LoginResult(bool Success, string? Error, string? AccessToken, string? RefreshToken, string? Status);
public record ApproveResult(bool Success, string? Error);
public record RejectResult(bool Success, string? Error);
public record InviteValidationResult(bool Valid, string? Error);

public class PendingProfileData
{
    public string Email { get; set; } = "";
    public string Handle { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? AvatarBlobRef { get; set; }
    public string? Location { get; set; }
    public string? AccountType { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool EmailConfirmed { get; set; }
}

public class UpdatePendingProfileRequest
{
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? AvatarBlobRef { get; set; }
    public string? Location { get; set; }
    public string? AccountType { get; set; }
}

public class AdminListResult(List<PendingAdminViewItem> Items, string? Cursor)
{
    public List<PendingAdminViewItem> Items { get; } = Items;
    public string? Cursor { get; } = Cursor;
}

public class PendingAdminViewItem
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string Handle { get; set; } = "";
    public string? Location { get; set; }
    public string? AccountType { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? InviteCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool EmailConfirmed { get; set; }
}

public class PendingAdminDetailView
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string Handle { get; set; } = "";
    public string Status { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? AccountType { get; set; }
    public string? InviteCode { get; set; }
    public bool EmailConfirmed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
