using System.Net.Mail;
using AccountManager;
using AccountManager.Db;
using CarpaNet;
using ComAtproto.Temp;
using Handle;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Temp;

public static class CheckHandleAvailabilityEndpoints
{
    public static RouteGroupBuilder MapCheckHandleAvailabilityEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.temp.checkHandleAvailability", HandleAsync);
        return group;
    }

    private static async Task<IResult> HandleAsync(
        string handle,
        string? email,
        string? birthDate,
        AccountRepository accountRepository,
        HandleManager handleManager)
    {
        if (!string.IsNullOrWhiteSpace(email) && !IsValidEmail(email))
            throw new XRPCError(new InvalidRequestErrorDetail("InvalidEmail", "Invalid email."));

        var normalizedHandle = handleManager.NormalizeAndEnsureValidHandle(handle);
        var existing = await accountRepository.GetAccountAsync(normalizedHandle, new AvailabilityFlags(true, true));
        if (existing == null)
        {
            return Results.Ok(new CheckHandleAvailabilityOutput
            {
                Handle = new ATHandle(normalizedHandle),
                Result = new CheckHandleAvailabilityResultAvailable()
            });
        }

        var suggestions = await BuildSuggestionsAsync(normalizedHandle, email, handleManager, accountRepository);
        return Results.Ok(new CheckHandleAvailabilityOutput
        {
            Handle = new ATHandle(normalizedHandle),
            Result = new CheckHandleAvailabilityResultUnavailable
            {
                Suggestions = suggestions
            }
        });
    }

    private static async Task<List<CheckHandleAvailabilitySuggestion>> BuildSuggestionsAsync(
        string handle, string? email, HandleManager handleManager, AccountRepository accountRepository)
    {
        var dotIndex = handle.IndexOf('.');
        if (dotIndex <= 0 || dotIndex == handle.Length - 1)
            return [];

        var stem = handle[..dotIndex];
        var domain = handle[(dotIndex + 1)..];
        var sanitizedEmailPrefix = SanitizeEmailPrefix(email);

        var candidates = new List<(string Handle, string Method)>();
        for (var i = 1; i <= 5; i++)
            candidates.Add(($"{stem}{i}.{domain}", "suffix-number"));

        if (!string.IsNullOrWhiteSpace(sanitizedEmailPrefix))
        {
            candidates.Add(($"{sanitizedEmailPrefix}.{domain}", "email-prefix"));
            candidates.Add(($"{sanitizedEmailPrefix}{stem[..Math.Min(stem.Length, 3)]}.{domain}", "email-prefix-stem"));
        }

        var suggestions = new List<CheckHandleAvailabilitySuggestion>();
        foreach (var candidate in candidates)
        {
            string normalized;
            try
            {
                normalized = handleManager.NormalizeAndEnsureValidHandle(candidate.Handle);
            }
            catch
            {
                continue;
            }

            var existing = await accountRepository.GetAccountAsync(normalized, new AvailabilityFlags(true, true));
            if (existing != null)
                continue;

            suggestions.Add(new CheckHandleAvailabilitySuggestion
            {
                Handle = new ATHandle(normalized),
                Method = candidate.Method
            });

            if (suggestions.Count >= 5)
                break;
        }

        return suggestions;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var address = new MailAddress(email);
            return address.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private static string? SanitizeEmailPrefix(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return null;

        var prefix = email[..email.IndexOf('@')].ToLowerInvariant();
        var filtered = new string(prefix.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        if (filtered.Length < 3)
            return null;

        return filtered[..Math.Min(filtered.Length, 18)];
    }
}
