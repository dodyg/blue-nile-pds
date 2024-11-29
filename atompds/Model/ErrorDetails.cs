namespace atompds.Model;

public class ErrorDetailException(FishyFlip.Models.ErrorDetail errorDetail, int statusCode = 400, Exception? innerException = null)
    : Exception(errorDetail.Message, innerException)
{
    public FishyFlip.Models.ErrorDetail ErrorDetail { get; } = errorDetail;
    public int StatusCode { get; } = statusCode;
}

public record InvalidRequestErrorDetail(string Message) : FishyFlip.Models.ErrorDetail("InvalidRequest", Message);
public record ExpiredTokenErrorDetail(string Message) : FishyFlip.Models.ErrorDetail("ExpiredToken", Message);
public record InvalidTokenErrorDetail(string Message) : FishyFlip.Models.ErrorDetail("InvalidToken", Message);
public record InvalidInviteCodeErrorDetail(string Message) : FishyFlip.Models.ErrorDetail("InvalidInviteCode", Message);
public record IncompatibleDidDocErrorDetail(string Message) : FishyFlip.Models.ErrorDetail("IncompatibleDidDoc", Message);
public record InvalidHandleErrorDetail(string Message) : FishyFlip.Models.ErrorDetail("InvalidHandle", Message);
public record UnsupportedDomainErrorDetail(string Message) : FishyFlip.Models.ErrorDetail("UnsupportedDomain", Message);
public record InvalidPasswordErrorDetail(string Message) : FishyFlip.Models.ErrorDetail("InvalidPassword", Message);
public record HandleNotAvailableErrorDetail(string Message) : FishyFlip.Models.ErrorDetail("HandleNotAvailable", Message);