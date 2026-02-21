using System.Text.Json.Serialization;

namespace Ato.Copilot.Mcp.Models;

public class McpRequest
{
    [JsonPropertyName("jsonrpc")] public string JsonRpc { get; set; } = "2.0";
    [JsonPropertyName("id")] public object Id { get; set; } = 0;
    [JsonPropertyName("method")] public string Method { get; set; } = string.Empty;
    [JsonPropertyName("params")] public object? Params { get; set; }
}

public class McpResponse
{
    [JsonPropertyName("jsonrpc")] public string JsonRpc { get; set; } = "2.0";
    [JsonPropertyName("id")] public object Id { get; set; } = 0;
    [JsonPropertyName("result")] public object? Result { get; set; }
    [JsonPropertyName("error")] public McpError? Error { get; set; }
}

public class McpError
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
    [JsonPropertyName("data")] public object? Data { get; set; }
}

public class McpTool
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("inputSchema")] public object? InputSchema { get; set; }
}

public class McpToolCall
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("arguments")] public Dictionary<string, object>? Arguments { get; set; }
}

public class McpToolResult
{
    [JsonPropertyName("content")] public List<McpContent> Content { get; set; } = new();
    [JsonPropertyName("isError")] public bool IsError { get; set; }

    public static McpToolResult Success(string text) => new()
    {
        Content = new List<McpContent> { new() { Type = "text", Text = text } },
        IsError = false
    };

    public static McpToolResult Error(string errorMessage) => new()
    {
        Content = new List<McpContent> { new() { Type = "text", Text = errorMessage } },
        IsError = true
    };
}

public class McpContent
{
    [JsonPropertyName("type")] public string Type { get; set; } = "text";
    [JsonPropertyName("text")] public string? Text { get; set; }
    [JsonPropertyName("mimeType")] public string? MimeType { get; set; }
    [JsonPropertyName("data")] public string? Data { get; set; }
}

public class McpChatResponse
{
    public bool Success { get; set; }
    public string Response { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string? AgentName { get; set; }
    public double ProcessingTimeMs { get; set; }
    public List<ToolExecution> ToolsExecuted { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();
    public bool RequiresFollowUp { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class ToolExecution
{
    public string ToolName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public double ExecutionTimeMs { get; set; }
}
