/**
 * ATO API Client Service (FR-049)
 *
 * HTTP client for communicating with the ATO Copilot MCP Server.
 * - 300s timeout
 * - User-Agent: ATO-Copilot-M365-Extension/1.0.0
 * - Generates m365-{timestamp}-{random9} conversationIds
 */

import axios, { AxiosInstance, AxiosError } from "axios";

export interface McpRequest {
  conversationId: string;
  message: string;
  conversationHistory: Array<{ role: string; content: string }>;
  context: {
    source: string;
    platform: string;
    userId?: string;
    userName?: string;
  };
}

export interface McpResponse {
  response: string;
  agentUsed?: string;
  intentType?: string;
  data?: {
    complianceScore?: number;
    passedControls?: number;
    warningControls?: number;
    failedControls?: number;
    resourceId?: string;
    estimatedCost?: number;
    resources?: Array<{ name: string; type: string; status: string }>;
    deploymentStatus?: string;
  };
  requiresFollowUp?: boolean;
  followUpPrompt?: string;
  missingFields?: string[];
}

export class ATOApiClient {
  private client: AxiosInstance;

  constructor(baseUrl: string, apiKey?: string) {
    const headers: Record<string, string> = {
      "Content-Type": "application/json",
      "User-Agent": "ATO-Copilot-M365-Extension/1.0.0",
    };

    if (apiKey) {
      headers["Authorization"] = `Bearer ${apiKey}`;
    }

    this.client = axios.create({
      baseURL: baseUrl,
      timeout: 300_000, // 300 seconds
      headers,
    });
  }

  /**
   * Generate a unique conversation ID in m365-{timestamp}-{random9} format.
   */
  static generateConversationId(): string {
    const timestamp = Date.now();
    const random = Math.random().toString(36).substring(2, 11).padEnd(9, "0");
    return `m365-${timestamp}-${random}`;
  }

  /**
   * Send a message to the MCP server and get a response.
   */
  async sendMessage(
    text: string,
    conversationId: string,
    userId: string,
    userName?: string
  ): Promise<McpResponse> {
    const request: McpRequest = {
      conversationId,
      message: text,
      conversationHistory: [], // No multi-turn for v1
      context: {
        source: "m365-copilot",
        platform: "M365",
        userId,
        userName,
      },
    };

    const response = await this.client.post<McpResponse>(
      "/mcp/chat",
      request
    );
    return response.data;
  }

  /**
   * Check MCP server health.
   */
  async checkHealth(): Promise<boolean> {
    try {
      const response = await this.client.get("/health");
      return response.status === 200;
    } catch {
      return false;
    }
  }
}
