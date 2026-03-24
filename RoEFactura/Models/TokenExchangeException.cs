using System.Net;

namespace RoEFactura.Models;

public class TokenExchangeException : Exception
{
    public TokenExchangeErrorType ErrorType { get; }
    public HttpStatusCode? StatusCode { get; }
    public string? ServerResponse { get; }
    
    public TokenExchangeException(
        string message, 
        TokenExchangeErrorType errorType,
        Exception? innerException = null,
        HttpStatusCode? statusCode = null,
        string? serverResponse = null) 
        : base(message, innerException)
    {
        ErrorType = errorType;
        StatusCode = statusCode;
        ServerResponse = serverResponse;
    }
}