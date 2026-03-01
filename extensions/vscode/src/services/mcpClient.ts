import axios, { AxiosInstance, AxiosError } from "axios";
import * as vscode from "vscode";

/**
 * Tool execution record from MCP response (FR-002).
 */
export interface ToolExecution {
  toolName: string;
  success: boolean;
  executionTimeMs: number;
  resultSummary?: string;
}

/**
 * Structured error detail (FR-007, Constitution VII).
 */
export interface ErrorDetail {
  errorCode: string;
  message: string;
  suggestion?: string;
}

/**
 * Request payload for POST /mcp/chat
 */
export interface McpChatRequest {
  conversationId: string;
  message: string;
  conversationHistory: Array<{
    role: "user" | "assistant";
    content: string;
  }>;
  context: {
    source: "vscode-copilot";
    platform: "VSCode";
    targetAgent?: string;
    metadata: {
      routingHint?: string;
      fileName?: string;
      language?: string;
      analysisType?: string;
    };
  };
  /** Action identifier for button-initiated requests (FR-014b) */
  action?: string;
  /** Contextual data for actions (FR-014b) */
  actionContext?: Record<string, unknown>;
}

/**
 * Enriched response from MCP Server (FR-001 through FR-007, FR-026).
 */
export interface McpChatResponse {
  success?: boolean;
  response: string;
  conversationId?: string;
  agentUsed?: string;
  intentType?: string;
  processingTimeMs?: number;
  templates?: Array<{
    name: string;
    type: string;
    content: string;
    language: string;
  }>;
  toolsExecuted?: ToolExecution[];
  errors?: ErrorDetail[];
  suggestions?: string[];
  requiresFollowUp?: boolean;
  followUpPrompt?: string;
  missingFields?: string[];
  data?: Record<string, unknown>;
}

/**
 * Health check response
 */
export interface HealthResponse {
  status: string;
  timestamp?: string;
}

/**
 * Mapped error for user-facing messages
 */
export interface McpError {
  message: string;
  actionButton: string;
  code: string;
}

/**
 * HTTP client service for communicating with the MCP Server.
 * Handles request construction, error mapping, and health checks.
 */
export class McpClient {
  private client: AxiosInstance;
  private outputChannel?: vscode.OutputChannel;

  constructor(outputChannel?: vscode.OutputChannel) {
    this.outputChannel = outputChannel;
    this.client = this.createClient();
  }

  private getConfig() {
    const config = vscode.workspace.getConfiguration("ato-copilot");
    return {
      apiUrl: config.get<string>("apiUrl", "http://localhost:3001"),
      apiKey: config.get<string>("apiKey", ""),
      timeout: config.get<number>("timeout", 30000),
      enableLogging: config.get<boolean>("enableLogging", false),
    };
  }

  private createClient(): AxiosInstance {
    const config = this.getConfig();
    const headers: Record<string, string> = {
      "Content-Type": "application/json",
    };

    if (config.apiKey) {
      headers["Authorization"] = `Bearer ${config.apiKey}`;
    }

    return axios.create({
      baseURL: config.apiUrl,
      timeout: config.timeout,
      headers,
    });
  }

  /**
   * Refresh the HTTP client with current settings.
   * Call when settings may have changed.
   */
  public refreshClient(): void {
    this.client = this.createClient();
  }

  private log(message: string): void {
    const config = this.getConfig();
    if (config.enableLogging && this.outputChannel) {
      this.outputChannel.appendLine(
        `[${new Date().toISOString()}] ${message}`
      );
    }
  }

  /**
   * Log error-level messages (FR-028). Always logged regardless of enableLogging.
   */
  private logError(message: string): void {
    if (this.outputChannel) {
      this.outputChannel.appendLine(
        `[${new Date().toISOString()}] [ERROR] ${message}`
      );
    }
  }

  /**
   * Send a chat request to the MCP Server.
   * Logs info-level details (FR-027) and error-level errors (FR-028).
   */
  public async sendMessage(request: McpChatRequest): Promise<McpChatResponse> {
    const startTime = Date.now();
    this.log(
      `POST /mcp/chat — conversationId=${request.conversationId} messageLen=${request.message.length}`
    );

    try {
      const response = await this.client.post<McpChatResponse>(
        "/mcp/chat",
        request
      );
      const elapsed = Date.now() - startTime;
      this.log(
        `Response: ${response.status} — intentType=${response.data.intentType ?? "n/a"} agentUsed=${response.data.agentUsed ?? "n/a"} responseTimeMs=${elapsed}`
      );

      // Log tool execution details (FR-029)
      if (response.data.toolsExecuted?.length) {
        const toolNames = response.data.toolsExecuted
          .map((t) => t.toolName)
          .join(", ");
        this.log(
          `Tools executed: [${toolNames}] — intentType=${response.data.intentType ?? "n/a"}`
        );
      }

      // Log errors at error level (FR-028)
      if (response.data.errors?.length) {
        for (const err of response.data.errors) {
          this.logError(
            `Error: ${err.errorCode} — ${err.message}${err.suggestion ? ` (suggestion: ${err.suggestion})` : ""} — conversationId=${request.conversationId}`
          );
        }
      }

      return response.data;
    } catch (error) {
      const elapsed = Date.now() - startTime;
      this.logError(
        `Request failed after ${elapsed}ms — conversationId=${request.conversationId}`
      );
      throw this.mapError(error);
    }
  }

  /**
   * Send an action-initiated request to the MCP Server (FR-014b).
   */
  public async sendAction(
    conversationId: string,
    action: string,
    actionContext: Record<string, unknown>,
    message?: string
  ): Promise<McpChatResponse> {
    const request: McpChatRequest = {
      conversationId,
      message: message ?? "",
      conversationHistory: [],
      context: {
        source: "vscode-copilot",
        platform: "VSCode",
        metadata: {},
      },
      action,
      actionContext,
    };
    return this.sendMessage(request);
  }

  /**
   * Check MCP Server health.
   */
  public async checkHealth(): Promise<HealthResponse> {
    this.log("GET /health");

    try {
      const response = await this.client.get<HealthResponse>("/health");
      this.log(`Health: ${response.status}`);
      return response.data;
    } catch (error) {
      throw this.mapError(error);
    }
  }

  /**
   * Map HTTP/network errors to user-facing messages per FR-033.
   */
  public mapError(error: unknown): McpError {
    const config = this.getConfig();

    if (axios.isAxiosError(error)) {
      const axiosError = error as AxiosError;
      const code = axiosError.code ?? "";

      if (code === "ECONNREFUSED") {
        return {
          message: `Cannot connect to ATO Copilot API at ${config.apiUrl}`,
          actionButton: "Configure Connection",
          code: "ECONNREFUSED",
        };
      }

      if (code === "ETIMEDOUT" || code === "ECONNABORTED") {
        return {
          message: "ATO Copilot API request timed out",
          actionButton: "Configure Connection",
          code: "ETIMEDOUT",
        };
      }

      const status = axiosError.response?.status;
      if (status === 401) {
        return {
          message: "ATO Copilot API authentication failed",
          actionButton: "Configure Connection",
          code: "HTTP_401",
        };
      }

      if (status === 500) {
        return {
          message: "ATO Copilot API encountered an error",
          actionButton: "Retry",
          code: "HTTP_500",
        };
      }

      return {
        message: `An unexpected error occurred: ${axiosError.message}`,
        actionButton: "Configure Connection",
        code: "UNKNOWN",
      };
    }

    if (error instanceof Error) {
      return {
        message: `An unexpected error occurred: ${error.message}`,
        actionButton: "Configure Connection",
        code: "UNKNOWN",
      };
    }

    return {
      message: "An unexpected error occurred",
      actionButton: "Configure Connection",
      code: "UNKNOWN",
    };
  }
}
