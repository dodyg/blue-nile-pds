using System.Net;
using System.Security.Claims;
using System.Text;
using atompds.Database;
using atompds.Model;
using FishyFlip.Lexicon.Com.Atproto.Server;
using FishyFlip.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using JwtRegisteredClaimNames = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames;

namespace atompds;

public static class UserExtensions
{
    public static UserClaimRecord ToUserClaimRecord(this ClaimsPrincipal principal)
    {
        var issuer = principal.FindFirst(JwtRegisteredClaimNames.Iss)?.Value;
        var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var iat = principal.FindFirst(JwtRegisteredClaimNames.Iat)?.Value;
        var exp = principal.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        var scope = principal.FindFirst("scope")?.Value;
        return new UserClaimRecord(issuer, sub, iat, exp, scope);
    }
    
    public record UserClaimRecord(
        string Issuer,
        string Sub,
        string Iat,
        string Exp,
        string Scope
    );
}

public class JwtHandler
{
    private readonly ConfigRepository _configRepository;
    private readonly AccountRepository _accountRepository;
    private readonly JsonWebTokenHandler _handler;
    
    public JwtHandler(ConfigRepository configRepository, AccountRepository accountRepository)
    {
        _configRepository = configRepository;
        _accountRepository = accountRepository;
        _handler = new JsonWebTokenHandler();
    }

    public async Task<CreateSessionOutput> GenerateJwtToken(CreateSessionInput request)
    {
        var config = await _configRepository.GetConfigAsync();
        var (did, handle) = await _accountRepository.VerifyAccountLoginAsync(request.Identifier!, request.Password!);
        var now = DateTimeOffset.UtcNow;
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.JwtAccessSecret));
        
        return new CreateSessionOutput
        {
            AccessJwt = _handler.CreateToken(new SecurityTokenDescriptor
            {
                IssuedAt = now.DateTime,
                Expires = now.AddHours(24).DateTime,
                Issuer = config.PdsDid,
                Audience = config.PdsDid,
                Subject = new ClaimsIdentity(
                [
                    new Claim(JwtRegisteredClaimNames.Sub, did),
                    new Claim("scope", "com.atproto.access"),
                ]),
                SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256)
            }),
            RefreshJwt = _handler.CreateToken(new SecurityTokenDescriptor
            {
                IssuedAt = now.DateTime,
                Expires = now.AddDays(30).DateTime,
                Issuer = config.PdsDid,
                Audience = config.PdsDid,
                Subject = new ClaimsIdentity(
                [
                    new Claim(JwtRegisteredClaimNames.Sub, did),
                    new Claim("scope", "com.atproto.refresh"),
                ]),
                SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256)
            }),
            Did = new ATDid(did),
            Handle = new ATHandle(handle),
            Active = true
        };
    }
    
    public async Task<ClaimsIdentity> ValidateJwtToken(string token)
    {
        var config = await _configRepository.GetConfigAsync();
        var key = Encoding.UTF8.GetBytes(config.JwtAccessSecret);
    
        var result = await _handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config.PdsDid,
            ValidateAudience = true,
            ValidAudience = config.PdsDid,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
            IssuerSigningKey = new SymmetricSecurityKey(key)
        });

        if (result is not {IsValid: true})
        {
            throw new ErrorDetailException(new InvalidTokenErrorDetail("invalid token"), innerException: result.Exception);
        }
        
        // validate scope
        var scope = result.ClaimsIdentity.FindFirst("scope")?.Value;
        if (scope != "com.atproto.access")
        {
            throw new ErrorDetailException(new InvalidTokenErrorDetail("invalid scope"));
        }

        return result.ClaimsIdentity;
    }
}