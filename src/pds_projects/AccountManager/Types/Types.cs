namespace AccountManager.Types;

public record AuthToken(string Scope, string Sub, long Exp);
public record RefreshToken(string Sub, long Exp, string Jti) : AuthToken(Auth.REFRESH_TOKEN_SCOPE, Sub, Exp);
public record AccessToken(string Sub, long Exp, string Jti) : AuthToken(Auth.ACCESS_TOKEN_SCOPE, Sub, Exp);