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
    UnknownError,

    /// <summary>
    /// The authorization code or refresh token is invalid, expired or revoked; re-authorization is required.
    /// </summary>
    InvalidGrant
}