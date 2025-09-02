namespace Inductobot.Models.Authentication;

/// <summary>
/// Authentication result for UAS-WAND authentication
/// </summary>
public class AuthResult
{
    /// <summary>
    /// Whether authentication was successful
    /// </summary>
    public bool Authenticated { get; set; }
    
    /// <summary>
    /// Authentication token if successful
    /// </summary>
    public string? Token { get; set; }
    
    /// <summary>
    /// Token expiration time in seconds
    /// </summary>
    public int ExpiresIn { get; set; }
    
    /// <summary>
    /// Authentication message or error description
    /// </summary>
    public string? Message { get; set; }
}