using Crypto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xrpc;

namespace AccountManager.Db;

public class EmailTokenStore
{
    private readonly AccountManagerDb _db;
    private readonly ILogger<EmailTokenStore> _logger;
    public EmailTokenStore(AccountManagerDb db, ILogger<EmailTokenStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<string> CreateEmailToken(string did, EmailToken.EmailTokenPurpose purpose)
    {
        var token = Utils.RandomHexString(32).ToUpper();
        var now = DateTime.UtcNow;


        // check for conflict on {purpose, did}, we only want one token per purpose per did at a time
        var existingToken = await _db.EmailTokens
            .Where(x => x.Did == did && x.Purpose == purpose)
            .FirstOrDefaultAsync();

        if (existingToken != null)
        {
            // update
            existingToken.Token = token;
            existingToken.RequestedAt = now;
            _db.EmailTokens.Update(existingToken);
        }
        else
        {
            var emailToken = new EmailToken
            {
                Did = did,
                Token = token,
                Purpose = purpose,
                RequestedAt = now
            };
            _db.EmailTokens.Add(emailToken);
        }
        await _db.SaveChangesAsync();
        return token;
    }
    public async Task AssertValidToken(string did, string token, EmailToken.EmailTokenPurpose purpose)
    {
        var emailToken = await _db.EmailTokens
            .Where(x => x.Did == did && x.Token == token && x.Purpose == purpose)
            .FirstOrDefaultAsync();

        if (emailToken == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("InvalidToken", "Token is invalid."));
        }

        var expired = emailToken.RequestedAt + TimeSpan.FromMinutes(15);
        if (DateTime.UtcNow > expired)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("ExpiredToken", "Token has expired."));
        }
    }
}