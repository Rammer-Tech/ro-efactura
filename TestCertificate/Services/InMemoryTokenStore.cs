using System.Collections.Concurrent;

namespace TestCertificate.Services;

public interface ITokenStore
{
    void SaveToken(string key, EFacturaToken token);
    EFacturaToken? GetToken(string key);
    void ClearToken(string key);
    void SaveState(string state);
    bool ValidateState(string state);
}

public class InMemoryTokenStore : ITokenStore
{
    // Using ConcurrentDictionary for thread safety
    private readonly ConcurrentDictionary<string, EFacturaToken> _tokens = new();
    private readonly ConcurrentDictionary<string, DateTime> _states = new();
    
    public void SaveToken(string key, EFacturaToken token)
    {
        _tokens[key] = token;
    }
    
    public EFacturaToken? GetToken(string key)
    {
        _tokens.TryGetValue(key, out var token);
        
        // Check if token is expired
        if (token != null && token.ExpiresAt < DateTime.UtcNow)
        {
            // Token expired, clear it
            ClearToken(key);
            return null;
        }
        
        return token;
    }
    
    public void ClearToken(string key)
    {
        _tokens.TryRemove(key, out _);
    }
    
    public void SaveState(string state)
    {
        // Store state with expiration (15 minutes)
        _states[state] = DateTime.UtcNow.AddMinutes(15);
        
        // Clean up old states
        CleanupExpiredStates();
    }
    
    public bool ValidateState(string state)
    {
        if (_states.TryRemove(state, out var expiresAt))
        {
            return expiresAt > DateTime.UtcNow;
        }
        
        return false;
    }
    
    private void CleanupExpiredStates()
    {
        var expiredStates = _states.Where(s => s.Value < DateTime.UtcNow).Select(s => s.Key).ToList();
        foreach (var state in expiredStates)
        {
            _states.TryRemove(state, out _);
        }
    }
}

public class EFacturaToken
{
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string TokenType { get; set; } = "Bearer";
    public string? Scope { get; set; }
    
    // Additional debug info
    public Dictionary<string, object>? DebugInfo { get; set; }
}