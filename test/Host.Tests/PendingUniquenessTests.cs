using BlueNilePds.Pds.PendingAccounts;
using BlueNilePds.Pds.PendingAccounts.Models;
using Microsoft.EntityFrameworkCore;

namespace BlueNilePds.Host.Tests;

public class PendingUniquenessTests
{
    private static DbContextOptions<PendingAccountsDb> TempDbOptions(out string tempRoot)
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "pending-uniqueness-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        return new DbContextOptionsBuilder<PendingAccountsDb>()
            .UseSqlite($"Data Source={Path.Combine(tempRoot, "pending.sqlite")}")
            .Options;
    }

    private static PendingRegistration Registration(string email, string handle, PendingRegistrationStatus status)
    {
        return new PendingRegistration
        {
            Email = email,
            Handle = handle,
            PasswordScrypt = "scrypt-hash",
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    [Test]
    public async Task PendingRow_CanReuseEmailAndHandleOfApprovedRowAsync()
    {
        var options = TempDbOptions(out var tempRoot);
        try
        {
            using (var seed = new PendingAccountsDb(options))
            {
                await seed.Database.MigrateAsync();
                seed.PendingRegistrations.Add(Registration("user@test.test", "user.test", PendingRegistrationStatus.Approved));
                await seed.SaveChangesAsync();
            }

            // The reported 500: re-registering after approve+delete hit the
            // global unique index. Scoped indexes must allow this insert.
            using (var db = new PendingAccountsDb(options))
            {
                db.PendingRegistrations.Add(Registration("user@test.test", "user.test", PendingRegistrationStatus.Pending));
                await db.SaveChangesAsync();
            }

            using (var check = new PendingAccountsDb(options))
            {
                await Assert.That(await check.PendingRegistrations.CountAsync()).IsEqualTo(2);
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Test]
    public async Task DuplicatePendingEmail_StillRejectedAsync()
    {
        var options = TempDbOptions(out var tempRoot);
        try
        {
            using (var seed = new PendingAccountsDb(options))
            {
                await seed.Database.MigrateAsync();
                seed.PendingRegistrations.Add(Registration("dup@test.test", "dup-one.test", PendingRegistrationStatus.Pending));
                await seed.SaveChangesAsync();
            }

            using (var db = new PendingAccountsDb(options))
            {
                db.PendingRegistrations.Add(Registration("dup@test.test", "dup-two.test", PendingRegistrationStatus.Pending));
                var ex = await Assert.That(async () => await db.SaveChangesAsync()).Throws<DbUpdateException>();
                await Assert.That(ex.InnerException?.Message.Contains("UNIQUE constraint failed")).IsTrue();
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }
}
