namespace Inductobot.Models.Authentication;

/// <summary>
/// Challenge request for UAS-WAND authentication
/// </summary>
public class ChallengeRequest
{
    /// <summary>
    /// Challenge byte array
    /// </summary>
    public byte[]? Challenge { get; set; }
    
    /// <summary>
    /// Unique challenge identifier
    /// </summary>
    public string? ChallengeId { get; set; }
    
    /// <summary>
    /// Unix timestamp of challenge generation
    /// </summary>
    public int Timestamp { get; set; }
}