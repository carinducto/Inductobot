namespace Inductobot.Models.Commands;

/// <summary>
/// Standard coded response from UAS-WAND device
/// </summary>
public class CodedResponse
{
    public int Code { get; set; }
    public string? Message { get; set; }
    public bool Success => Code == 0;
}