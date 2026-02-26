import axios, { AxiosInstance, AxiosError } from "axios";
import * as vscode from "vscode";

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
}

/**
 * Response from MCP Server
 */
export interface McpChatResponse {
  response: string;
  agentUsed?: string;
  intentType?: string;
  templates?: Array<{
    name: string;
    type: string;
    content: string;
    language: string;
  }>;
  requiresFollowUp?: boolean;
  followUpPrompt?: string;
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
   * Send a chat request to the MCP Server.
   */
  public async sendMessage(request: McpChatRequest): Promise<McpChatResponse> {
    this.log(`POST /mcp/chat — ${JSON.stringify(request).substring(0, 200)}`);

    try {
      const response = await this.client.post<McpChatResponse>(
        "/mcp/chat",
        request
      );
      this.log(`Response: ${response.status}`);
      return response.data;
    } catch (error) {
      throw this.mapError(error);
    }
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
