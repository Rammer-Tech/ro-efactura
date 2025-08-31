namespace RoEFactura.Models;

public enum TokenExchangeErrorType
{
    NetworkError,
    Timeout,
    AuthenticationFailed,
    InvalidRequest,
    InvalidResponse,
    RateLimited,
    ServiceUnavailable,
    ServerError,
    UnknownError
}