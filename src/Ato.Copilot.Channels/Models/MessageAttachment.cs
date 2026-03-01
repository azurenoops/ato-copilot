namespace Ato.Copilot.Channels.Models;

/// <summary>
/// File attachment on an incoming message.
/// </summary>
public class MessageAttachment
{
    /// <summary>File name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>MIME type.</summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>URL to retrieve attachment (null if inline).</summary>
    public string? Url { get; set; }

    /// <summary>Inline binary content (null if URL-based).</summary>
    public byte[]? Data { get; set; }

    /// <summary>File size in bytes.</summary>
    public long Size { get; set; }
}
