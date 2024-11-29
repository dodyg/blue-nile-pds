using System.ComponentModel.DataAnnotations;

namespace atompds.OauthProvider.Account;


public record Account(Sub Sub, string[] Aud, string? PreferredUsername, string? Email, bool? EmailVerified, string? Picture, string? Name);
public record Sub([MinLength(1)] string Id);