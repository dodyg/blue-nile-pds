namespace Xrpc;

public enum ResponseType
{
    Unknown = 1,
    InvalidResponse = 2,
    Success = 200,
    InvalidRequest = 400,
    AuthRequired = 401,
    Forbidden = 403,
    XRPCNotSupported = 404,
    NotAcceptable = 406,
    PayloadTooLarge = 413,
    UnsupportedMediaType = 415,
    RateLimitExceeded = 429,
    InternalServerError = 500,
    MethodNotImplemented = 501,
    UpstreamFailure = 502,
    NotEnoughResources = 503,
    UpstreamTimeout = 504,
}

public class ResponseTypeNames
{
    public const string Unknown = "Unknown";
    public const string InvalidResponse = "InvalidResponse";
    public const string Success = "Success";
    public const string InvalidRequest = "InvalidRequest";
    public const string AuthRequired = "AuthenticationRequired";
    public const string Forbidden = "Forbidden";
    public const string XRPCNotSupported = "XRPCNotSupported";
    public const string PayloadTooLarge = "PayloadTooLarge";
    public const string UnsupportedMediaType = "UnsupportedMediaType";
    public const string RateLimitExceeded = "RateLimitExceeded";
    public const string InternalServerError = "InternalServerError";
    public const string MethodNotImplemented = "MethodNotImplemented";
    public const string UpstreamFailure = "UpstreamFailure";
    public const string NotEnoughResources = "NotEnoughResources";
    public const string UpstreamTimeout = "UpstreamTimeout";
    
    public static readonly Dictionary<ResponseType, string> Map = new()
    {
        {ResponseType.Unknown, Unknown},
        {ResponseType.InvalidResponse, InvalidResponse},
        {ResponseType.Success, Success},
        {ResponseType.InvalidRequest, InvalidRequest},
        {ResponseType.AuthRequired, AuthRequired},
        {ResponseType.Forbidden, Forbidden},
        {ResponseType.XRPCNotSupported, XRPCNotSupported},
        {ResponseType.PayloadTooLarge, PayloadTooLarge},
        {ResponseType.UnsupportedMediaType, UnsupportedMediaType},
        {ResponseType.RateLimitExceeded, RateLimitExceeded},
        {ResponseType.InternalServerError, InternalServerError},
        {ResponseType.MethodNotImplemented, MethodNotImplemented},
        {ResponseType.UpstreamFailure, UpstreamFailure},
        {ResponseType.NotEnoughResources, NotEnoughResources},
        {ResponseType.UpstreamTimeout, UpstreamTimeout}
    };
    
    public static readonly Dictionary<string, ResponseType> ReverseMap = Map.ToDictionary(x => x.Value, x => x.Key);
}

public class ResponseTypeStrings
{
    public const string Unknown = "Unknown";
    public const string InvalidResponse = "Invalid Response";
    public const string Success = "Success";
    public const string InvalidRequest = "Invalid Request";
    public const string AuthRequired = "Authentication Required";
    public const string Forbidden = "Forbidden";
    public const string XRPCNotSupported = "XRPC Not Supported";
    public const string PayloadTooLarge = "Payload Too Large";
    public const string UnsupportedMediaType = "Unsupported Media Type";
    public const string RateLimitExceeded = "Rate Limit Exceeded";
    public const string InternalServerError = "Internal Server Error";
    public const string MethodNotImplemented = "Method Not Implemented";
    public const string UpstreamFailure = "Upstream Failure";
    public const string NotEnoughResources = "Not Enough Resources";
    public const string UpstreamTimeout = "Upstream Timeout";
    
    public static readonly Dictionary<ResponseType, string> Map = new()
    {
        {ResponseType.Unknown, Unknown},
        {ResponseType.InvalidResponse, InvalidResponse},
        {ResponseType.Success, Success},
        {ResponseType.InvalidRequest, InvalidRequest},
        {ResponseType.AuthRequired, AuthRequired},
        {ResponseType.Forbidden, Forbidden},
        {ResponseType.XRPCNotSupported, XRPCNotSupported},
        {ResponseType.PayloadTooLarge, PayloadTooLarge},
        {ResponseType.UnsupportedMediaType, UnsupportedMediaType},
        {ResponseType.RateLimitExceeded, RateLimitExceeded},
        {ResponseType.InternalServerError, InternalServerError},
        {ResponseType.MethodNotImplemented, MethodNotImplemented},
        {ResponseType.UpstreamFailure, UpstreamFailure},
        {ResponseType.NotEnoughResources, NotEnoughResources},
        {ResponseType.UpstreamTimeout, UpstreamTimeout}
    };
    
    public static readonly Dictionary<string, ResponseType> ReverseMap = Map.ToDictionary(x => x.Value, x => x.Key);
}

public class ResponseTypes
{
    public static ResponseType HttpResponseCodeToEnum(int status)
    {
        if (Enum.IsDefined(typeof(ResponseType), status))
        {
            return (ResponseType) status;
        }

        return status switch
        {
            >= 100 and < 200 => ResponseType.XRPCNotSupported,
            >= 200 and < 300 => ResponseType.Success,
            >= 300 and < 400 => ResponseType.XRPCNotSupported,
            >= 400 and < 500 => ResponseType.InvalidRequest,
            _ => ResponseType.InternalServerError
        };
    }
    
    public static string HttpResponseCodeToName(int status)
    {
        return ResponseTypeNames.Map.GetValueOrDefault(HttpResponseCodeToEnum(status), ResponseTypeNames.Unknown);
    }
    
    public static string HttpResponseCodeToString(int status)
    {
        return ResponseTypeStrings.Map.GetValueOrDefault(HttpResponseCodeToEnum(status), ResponseTypeStrings.Unknown);
    }
}