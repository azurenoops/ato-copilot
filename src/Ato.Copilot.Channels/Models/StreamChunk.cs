namespace Ato.Copilot.Channels.Models;

/// <summary>
/// A typed chunk in a streaming response.
/// </summary>
public class StreamChunk
{
    /// <summary>Incrementing sequence number.</summary>
    public long SequenceNumber { get; set; }

    /// <summary>Chunk text content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Content type classification.</summary>
    public StreamContentType ContentType { get; set; } = StreamContentType.Text;
}
