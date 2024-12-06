// ReSharper disable InconsistentNaming
namespace Xrpc;

public class XRPCError : Exception
{
    public ResponseType Status { get; }
    public string Error { get; }
    
    public ErrorDetail Detail => new(Error, Message);

    public XRPCError(int statusCode, string? error = null, string? message = null, Exception? innerException = null) : base(message ?? error ?? ResponseTypes.HttpResponseCodeToString(statusCode), innerException)
    {
        Status = ResponseTypes.HttpResponseCodeToEnum(statusCode);
        Error = error ?? ResponseTypes.HttpResponseCodeToName(statusCode);
    }
    
    public XRPCError(ResponseType status, string? error = null, string? message = null, Exception? innerException = null) : base(message, innerException)
    {
        Status = status;
        Error = error ?? ResponseTypeNames.Map.GetValueOrDefault(status, ResponseTypeNames.Unknown);
    }
    
    public XRPCError(ErrorDetail detail, Exception? innerException = null) : base(detail.Message, innerException)
    {
        Status = ResponseTypeNames.ReverseMap.GetValueOrDefault(detail.Error, ResponseType.Unknown);
        Error = detail.Error;
    }
    
    public XRPCError(ResponseType status, ErrorDetail detail, Exception? innerException = null) : base(detail.Message, innerException)
    {
        Status = status;
        Error = detail.Error;
    }
}

public record ErrorDetail
{
    public string Error { get; }
    public string Message { get; }
    
    public ErrorDetail(string error, string message)
    {
        Error = error;
        Message = message;
    }
    
    public ErrorDetail(ResponseType status, string message) : this(ResponseTypeNames.Map.GetValueOrDefault(status, "Unknown"), message) { }
}
public record InvalidRequestErrorDetail(string Message) : ErrorDetail(ResponseType.InvalidRequest, Message);
public record ExpiredTokenErrorDetail(string Message) : ErrorDetail("ExpiredToken", Message);
public record InvalidTokenErrorDetail(string Message) : ErrorDetail("InvalidToken", Message);
public record InvalidInviteCodeErrorDetail(string Message) : ErrorDetail("InvalidInviteCode", Message);
public record IncompatibleDidDocErrorDetail(string Message) : ErrorDetail("IncompatibleDidDoc", Message);
public record InvalidHandleErrorDetail(string Message) : ErrorDetail("InvalidHandle", Message);
public record UnsupportedDomainErrorDetail(string Message) : ErrorDetail("UnsupportedDomain", Message);
public record InvalidPasswordErrorDetail(string Message) : ErrorDetail("InvalidPassword", Message);
public record HandleNotAvailableErrorDetail(string Message) : ErrorDetail("HandleNotAvailable", Message);
public record AuthRequiredErrorDetail(string Message) : ErrorDetail(ResponseType.AuthRequired, Message);
public record AccountTakenDownErrorDetail(string Message) : ErrorDetail("AccountTakedown", Message);