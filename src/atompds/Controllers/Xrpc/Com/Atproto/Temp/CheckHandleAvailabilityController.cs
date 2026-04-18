using System.Net.Mail;
using AccountManager;
using AccountManager.Db;
using Handle;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Temp;

[ApiController]
[Route("xrpc")]
public class CheckHandleAvailabilityController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly HandleManager _handleManager;

    public CheckHandleAvailabilityController(AccountRepository accountRepository, HandleManager handleManager)
    {
        _accountRepository = accountRepository;
        _handleManager = handleManager;
    }

    [HttpGet("com.atproto.temp.checkHandleAvailability")]
    public async Task<IActionResult> CheckHandleAvailabilityAsync(
        [FromQuery] string handle,
        [FromQuery] string? email,
        [FromQuery] string? birthDate)
    {
        if (!string.IsNullOrWhiteSpace(email) && !IsValidEmail(email))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("InvalidEmail", "Invalid email."));
        }

        var normalizedHandle = _handleManager.NormalizeAndEnsureValidHandle(handle);
        var existing = await _accountRepository.GetAccountAsync(normalizedHandle, new AvailabilityFlags(true, true));
        if (existing == null)
        {
            return Ok(new
            {
                handle = normalizedHandle,
                result = new { }
            });
        }

        var suggestions = await BuildSuggestionsAsync(normalizedHandle, email);
        return Ok(new
        {
            handle = normalizedHandle,
            result = new
            {
                suggestions
            }
        });
    }

    private async Task<List<object>> BuildSuggestionsAsync(string handle, string? email)
    {
        var dotIndex = handle.IndexOf('.');
        if (dotIndex <= 0 || dotIndex == handle.Length - 1)
        {
            return [];
        }

        var stem = handle[..dotIndex];
        var domain = handle[(dotIndex + 1)..];
        var sanitizedEmailPrefix = SanitizeEmailPrefix(email);

        var candidates = new List<(string Handle, string Method)>();
        for (var i = 1; i <= 5; i++)
        {
            candidates.Add(($"{stem}{i}.{domain}", "suffix-number"));
        }

        if (!string.IsNullOrWhiteSpace(sanitizedEmailPrefix))
        {
            candidates.Add(($"{sanitizedEmailPrefix}.{domain}", "email-prefix"));
            candidates.Add(($"{sanitizedEmailPrefix}{stem[..Math.Min(stem.Length, 3)]}.{domain}", "email-prefix-stem"));
        }

        var suggestions = new List<object>();
        foreach (var candidate in candidates)
        {
            string normalized;
            try
            {
                normalized = _handleManager.NormalizeAndEnsureValidHandle(candidate.Handle);
            }
            catch
            {
                continue;
            }

            var existing = await _accountRepository.GetAccountAsync(normalized, new AvailabilityFlags(true, true));
            if (existing != null)
            {
                continue;
            }

            suggestions.Add(new
            {
                handle = normalized,
                method = candidate.Method
            });

            if (suggestions.Count >= 5)
            {
                break;
            }
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
        {
            return null;
        }

        var prefix = email[..email.IndexOf('@')].ToLowerInvariant();
        var filtered = new string(prefix.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        if (filtered.Length < 3)
        {
            return null;
        }

        return filtered[..Math.Min(filtered.Length, 18)];
    }
}
