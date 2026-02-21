using System.Text.Json.Serialization;

namespace Ato.Copilot.Core.Models;

/// <summary>
/// Standard MCP tool response envelope per Constitution Principle VII.
/// All tool responses MUST use this envelope schema.
/// </summary>
/// <typeparam name="T">The type of the data payload.</typeparam>
public class ToolResponse<T>
{
    /// <summary>
    /// Response status: "success", "error", or "partial".
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "success";

    /// <summary>
    /// Tool-specific data payload. Null on error.
    /// </summary>
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    /// <summary>
    /// Response metadata including tool name, execution time, and timestamp.
    /// </summary>
    [JsonPropertyName("metadata")]
    public ResponseMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Error details when status is "error". Null on success.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ErrorDetail? Error { get; set; }

    /// <summary>
    /// Multiple error details when multiple errors occur. Null when single error or success.
    /// </summary>
    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ErrorDetail>? Errors { get; set; }

    /// <summary>
    /// Pagination information for paginated responses.
    /// </summary>
    [JsonPropertyName("pagination")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PaginationInfo? Pagination { get; set; }

    /// <summary>
    /// Creates a successful response with the given data payload.
    /// </summary>
    /// <param name="data">The data payload.</param>
    /// <param name="toolName">Name of the tool that produced this response.</param>
    /// <param name="executionTimeMs">Execution time in milliseconds.</param>
    /// <returns>A success response envelope.</returns>
    public static ToolResponse<T> Success(T data, string toolName, long executionTimeMs) => new()
    {
        Status = "success",
        Data = data,
        Metadata = new ResponseMetadata
        {
            ToolName = toolName,
            ExecutionTimeMs = executionTimeMs,
            Timestamp = DateTime.UtcNow
        }
    };

    /// <summary>
    /// Creates a partial success response (e.g., one scan succeeded but another failed).
    /// </summary>
    /// <param name="data">The partial data payload.</param>
    /// <param name="toolName">Name of the tool.</param>
    /// <param name="executionTimeMs">Execution time in milliseconds.</param>
    /// <param name="warnings">Warning error details for failed sub-operations.</param>
    /// <returns>A partial success response envelope.</returns>
    public static ToolResponse<T> Partial(T data, string toolName, long executionTimeMs, List<ErrorDetail> warnings) => new()
    {
        Status = "partial",
        Data = data,
        Errors = warnings,
        Metadata = new ResponseMetadata
        {
            ToolName = toolName,
            ExecutionTimeMs = executionTimeMs,
            Timestamp = DateTime.UtcNow
        }
    };

    /// <summary>
    /// Creates an error response with a single error.
    /// </summary>
    /// <param name="errorCode">Machine-readable error code.</param>
    /// <param name="message">Human-readable error message.</param>
    /// <param name="suggestion">Actionable suggestion for the user.</param>
    /// <param name="toolName">Name of the tool.</param>
    /// <param name="executionTimeMs">Execution time in milliseconds.</param>
    /// <returns>An error response envelope.</returns>
    public static ToolResponse<T> Fail(string errorCode, string message, string suggestion, string toolName, long executionTimeMs) => new()
    {
        Status = "error",
        Data = default,
        Error = new ErrorDetail
        {
            ErrorCode = errorCode,
            Message = message,
            Suggestion = suggestion
        },
        Metadata = new ResponseMetadata
        {
            ToolName = toolName,
            ExecutionTimeMs = executionTimeMs,
            Timestamp = DateTime.UtcNow
        }
    };

    /// <summary>
    /// Creates an error response with multiple errors.
    /// </summary>
    /// <param name="errors">List of error details, ordered by severity (most critical first).</param>
    /// <param name="toolName">Name of the tool.</param>
    /// <param name="executionTimeMs">Execution time in milliseconds.</param>
    /// <returns>An error response with multiple error details.</returns>
    public static ToolResponse<T> FailMultiple(List<ErrorDetail> errors, string toolName, long executionTimeMs) => new()
    {
        Status = "error",
        Data = default,
        Errors = errors,
        Metadata = new ResponseMetadata
        {
            ToolName = toolName,
            ExecutionTimeMs = executionTimeMs,
            Timestamp = DateTime.UtcNow
        }
    };
}

/// <summary>
/// Metadata included in every tool response.
/// </summary>
public class ResponseMetadata
{
    /// <summary>Name of the MCP tool that produced this response.</summary>
    [JsonPropertyName("toolName")]
    public string ToolName { get; set; } = string.Empty;

    /// <summary>Execution time in milliseconds (integer).</summary>
    [JsonPropertyName("executionTimeMs")]
    public long ExecutionTimeMs { get; set; }

    /// <summary>UTC timestamp in ISO 8601 format.</summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Error detail with machine-readable code and actionable suggestion.
/// </summary>
public class ErrorDetail
{
    /// <summary>Human-readable error message for the compliance officer audience.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>Machine-readable error code (e.g., "SUBSCRIPTION_NOT_CONFIGURED").</summary>
    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>Actionable suggestion text to help the user resolve the error.</summary>
    [JsonPropertyName("suggestion")]
    public string Suggestion { get; set; } = string.Empty;
}

/// <summary>
/// Pagination information for paginated tool responses.
/// </summary>
public class PaginationInfo
{
    /// <summary>Current page number (1-based).</summary>
    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    /// <summary>Number of items per page.</summary>
    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 25;

    /// <summary>Total number of items across all pages.</summary>
    [JsonPropertyName("totalItems")]
    public int TotalItems { get; set; }

    /// <summary>Total number of pages.</summary>
    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    /// <summary>Whether more pages are available.</summary>
    [JsonPropertyName("hasNextPage")]
    public bool HasNextPage { get; set; }

    /// <summary>Opaque token for retrieving the next page.</summary>
    [JsonPropertyName("nextPageToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextPageToken { get; set; }
}

/// <summary>
/// Well-known MCP error codes used across all tools.
/// </summary>
public static class ErrorCodes
{
    /// <summary>No default subscription configured and none specified.</summary>
    public const string SubscriptionNotConfigured = "SUBSCRIPTION_NOT_CONFIGURED";

    /// <summary>Subscription ID does not exist or insufficient access.</summary>
    public const string SubscriptionNotFound = "SUBSCRIPTION_NOT_FOUND";

    /// <summary>Azure authentication failed.</summary>
    public const string AzureAuthFailed = "AZURE_AUTH_FAILED";

    /// <summary>Azure API call failed.</summary>
    public const string AzureApiError = "AZURE_API_ERROR";

    /// <summary>Azure Resource Graph query failed.</summary>
    public const string ResourceScanFailed = "RESOURCE_SCAN_FAILED";

    /// <summary>Azure Policy compliance query failed.</summary>
    public const string PolicyScanFailed = "POLICY_SCAN_FAILED";

    /// <summary>Defender for Cloud query failed.</summary>
    public const string DefenderScanFailed = "DEFENDER_SCAN_FAILED";

    /// <summary>Remediation execution failed.</summary>
    public const string RemediationFailed = "REMEDIATION_FAILED";

    /// <summary>User role cannot perform remediation.</summary>
    public const string RemediationDenied = "REMEDIATION_DENIED";

    /// <summary>Another remediation is already running on this subscription.</summary>
    public const string RemediationInProgress = "REMEDIATION_IN_PROGRESS";

    /// <summary>Remediation needs ComplianceOfficer approval.</summary>
    public const string ApprovalRequired = "APPROVAL_REQUIRED";

    /// <summary>Document generation failed.</summary>
    public const string DocumentGenerationFailed = "DOCUMENT_GENERATION_FAILED";

    /// <summary>No assessments found for the request.</summary>
    public const string NoAssessmentData = "NO_ASSESSMENT_DATA";

    /// <summary>Control ID format is invalid.</summary>
    public const string InvalidControlId = "INVALID_CONTROL_ID";

    /// <summary>Azure API rate limit hit.</summary>
    public const string RateLimited = "RATE_LIMITED";

    /// <summary>Assessment exceeded 60-second timeout.</summary>
    public const string AssessmentTimeout = "ASSESSMENT_TIMEOUT";

    /// <summary>Subscription ID is not a valid GUID format.</summary>
    public const string InvalidSubscriptionId = "INVALID_SUBSCRIPTION_ID";

    /// <summary>Framework value is not recognized.</summary>
    public const string InvalidFramework = "INVALID_FRAMEWORK";

    /// <summary>Baseline value is not recognized.</summary>
    public const string InvalidBaseline = "INVALID_BASELINE";

    /// <summary>Preference name is not recognized.</summary>
    public const string InvalidPreferenceName = "INVALID_PREFERENCE_NAME";

    /// <summary>Preference value is not valid for the given name.</summary>
    public const string InvalidPreferenceValue = "INVALID_PREFERENCE_VALUE";

    /// <summary>Required parameter for this action is missing.</summary>
    public const string MissingRequiredParam = "MISSING_REQUIRED_PARAM";
}
