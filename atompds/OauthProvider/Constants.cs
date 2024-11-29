// ReSharper disable InconsistentNaming
namespace atompds.OauthProvider;

public class Constants
{
    public const string DEVICE_ID_PREFIX = "dev-";
    public const int DEVICE_ID_BYTES_LENGTH = 16; // 128 bits
    
    public const string SESSION_ID_PREFIX = "ses-";
    public const int SESSION_ID_BYTES_LENGTH = 16; // 128 bits - only valid if device id is valid
    
    public const string REFRESH_TOKEN_PREFIX = "ref-";
    public const int REFRESH_TOKEN_BYTES_LENGTH = 32; // 256 bits
    
    public const string TOKEN_ID_PREFIX = "tok-";
    public const int TOKEN_ID_BYTES_LENGTH = 16; // 128 bits - used as `jti` in JWTs (cannot be forged)
    
    public const string REQUEST_ID_PREFIX = "req-";
    public const int REQUEST_ID_BYTES_LENGTH = 16; // 128 bits
    
    public const string CODE_PREFIX = "cod-";
    public const int CODE_BYTES_LENGTH = 32;
    
    public const long SECOND = 1_000;
    public const long MINUTE = 60 * SECOND;
    public const long HOUR = 60 * MINUTE;
    public const long DAY = 24 * HOUR;
    public const long WEEK = 7 * DAY;
    public const long YEAR = 365 * DAY;
    public const long MONTH = YEAR / 12;
    
    public const long AUTHENTICATION_MAX_AGE = 7 * DAY;
    
    public const long TOKEN_MAX_AGE = 60 * MINUTE;
    
    public const long AUTHORIZATION_INACTIVITY_TIMEOUT = 5 * MINUTE;
    
    public const long AUTHENTICATED_REFRESH_INACTIVITY_TIMEOUT = 1 * MONTH;
    
    public const long UNAUTHENTICATED_REFRESH_INACTIVITY_TIMEOUT = 2 * DAY;
    
    public const long UNAUTHENTICATED_REFRESH_LIFETIME = 1 * WEEK;
    
    public const long AUTHENTICATED_REFRESH_LIFETIME = 1 * YEAR;
    
    public const long PAR_EXPIRES_IN = 5 * MINUTE;
    
    public const long JAR_MAX_AGE = 59 * SECOND;
    
    public const long CLIENT_ASSERTION_MAX_AGE = 1 * MINUTE;
    
    public const long DPOP_NONCE_MAX_AGE = 3 * MINUTE;
    
    public const long SESSION_FIXATION_MAX_AGE = 5 * SECOND;
    
    public const long CODE_CHALLENGE_REPLAY_TIMEFRAME = 1 * DAY;
}