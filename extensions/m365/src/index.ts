/**
 * ATO Copilot M365 Extension — Express Server (FR-037, FR-045, FR-046, FR-048, FR-050)
 *
 * Endpoints:
 * - POST /api/messages — Teams webhook handler with intent-based card routing
 * - GET /health — Health check
 * - GET /openapi.json — OpenAPI 3.0 spec
 * - GET /ai-plugin.json — M365 Copilot plugin descriptor
 *
 * Config validation on startup, graceful shutdown on SIGINT/SIGTERM.
 */

import express, { Request, Response } from "express";
import { ATOApiClient, McpResponse } from "./services/atoApiClient";
import {
  buildComplianceCard,
  buildInfrastructureCard,
  buildCostCard,
  buildDeploymentCard,
  buildResourceCard,
  buildGenericCard,
  buildErrorCard,
  buildFollowUpCard,
} from "./cards";

// --- Configuration validation (FR-048) ---

const ATO_API_URL = process.env.ATO_API_URL;
const ATO_API_KEY = process.env.ATO_API_KEY || "";
const PORT = parseInt(process.env.PORT || "3978", 10);
const BOT_ID = process.env.BOT_ID;
const BOT_PASSWORD = process.env.BOT_PASSWORD;

if (!ATO_API_URL) {
  console.error("FATAL: ATO_API_URL environment variable is required.");
  process.exit(1);
}

if (!BOT_ID) {
  console.warn("WARNING: BOT_ID not set — Bot Framework authentication disabled.");
}
if (!BOT_PASSWORD) {
  console.warn("WARNING: BOT_PASSWORD not set — Bot Framework authentication disabled.");
}

// --- Initialize services ---

const apiClient = new ATOApiClient(ATO_API_URL, ATO_API_KEY || undefined);
const app = express();
app.use(express.json());

// --- Intent-based card routing ---

function buildCardForResponse(mcpResponse: McpResponse): Record<string, unknown> {
  // Follow-up takes priority
  if (mcpResponse.requiresFollowUp && mcpResponse.followUpPrompt && mcpResponse.missingFields) {
    return buildFollowUpCard({
      followUpPrompt: mcpResponse.followUpPrompt,
      missingFields: mcpResponse.missingFields,
    });
  }

  const data = mcpResponse.data;

  switch (mcpResponse.intentType) {
    case "compliance":
      return buildComplianceCard({
        complianceScore: data?.complianceScore ?? 0,
        passedControls: data?.passedControls ?? 0,
        warningControls: data?.warningControls ?? 0,
        failedControls: data?.failedControls ?? 0,
        response: mcpResponse.response,
      });

    case "infrastructure":
      return buildInfrastructureCard({
        resourceId: data?.resourceId ?? "",
        response: mcpResponse.response,
      });

    case "cost":
      return buildCostCard({
        estimatedCost: data?.estimatedCost ?? 0,
        response: mcpResponse.response,
      });

    case "deployment":
      return buildDeploymentCard({
        deploymentStatus: data?.deploymentStatus ?? "Unknown",
        response: mcpResponse.response,
      });

    case "resource_discovery":
      return buildResourceCard({
        resources: data?.resources ?? [],
        response: mcpResponse.response,
      });

    default:
      return buildGenericCard({
        response: mcpResponse.response,
        agentUsed: mcpResponse.agentUsed,
      });
  }
}

// --- Endpoints ---

// POST /api/messages (FR-037)
app.post("/api/messages", async (req: Request, res: Response) => {
  try {
    const { text, conversation, from } = req.body;

    if (!text) {
      const errorCard = buildErrorCard({
        errorMessage: "No message text provided.",
        helpText: "Please type a question or command to get started.",
      });
      res.json({
        type: "message",
        attachments: [
          {
            contentType: "application/vnd.microsoft.card.adaptive",
            content: errorCard,
          },
        ],
      });
      return;
    }

    const conversationId =
      conversation?.id || ATOApiClient.generateConversationId();
    const userId = from?.id || "unknown";
    const userName = from?.name;

    const mcpResponse = await apiClient.sendMessage(
      text,
      conversationId,
      userId,
      userName
    );

    const card = buildCardForResponse(mcpResponse);

    res.json({
      type: "message",
      attachments: [
        {
          contentType: "application/vnd.microsoft.card.adaptive",
          content: card,
        },
      ],
    });
  } catch (error) {
    console.error("Error processing message:", error);
    const errorCard = buildErrorCard({
      errorMessage: "An unexpected error occurred while processing your request.",
      helpText:
        "Please try again. If the problem persists, contact your administrator.",
    });
    res.json({
      type: "message",
      attachments: [
        {
          contentType: "application/vnd.microsoft.card.adaptive",
          content: errorCard,
        },
      ],
    });
  }
});

// GET /health (FR-045)
app.get("/health", (_req: Request, res: Response) => {
  res.json({
    name: "ATO Copilot M365 Extension",
    version: "1.0.0",
    timestamp: new Date().toISOString(),
  });
});

// GET /openapi.json (FR-046)
app.get("/openapi.json", (_req: Request, res: Response) => {
  res.json({
    openapi: "3.0.0",
    info: {
      title: "ATO Copilot M365 Extension",
      version: "1.0.0",
      description:
        "Compliance assessment and remediation for Azure Government via Microsoft Teams.",
    },
    servers: [{ url: `http://localhost:${PORT}` }],
    paths: {
      "/api/messages": {
        post: {
          operationId: "sendMessage",
          summary: "Send a message to ATO Copilot",
          requestBody: {
            required: true,
            content: {
              "application/json": {
                schema: {
                  type: "object",
                  properties: {
                    text: { type: "string", description: "Message text" },
                    conversation: {
                      type: "object",
                      properties: {
                        id: { type: "string" },
                      },
                    },
                    from: {
                      type: "object",
                      properties: {
                        id: { type: "string" },
                        name: { type: "string" },
                      },
                    },
                  },
                  required: ["text"],
                },
              },
            },
          },
          responses: {
            "200": {
              description: "Adaptive Card response",
              content: {
                "application/json": {
                  schema: {
                    type: "object",
                    properties: {
                      type: { type: "string" },
                      attachments: {
                        type: "array",
                        items: {
                          type: "object",
                          properties: {
                            contentType: { type: "string" },
                            content: { type: "object" },
                          },
                        },
                      },
                    },
                  },
                },
              },
            },
          },
        },
      },
      "/health": {
        get: {
          operationId: "healthCheck",
          summary: "Health check",
          responses: {
            "200": {
              description: "Health status",
            },
          },
        },
      },
    },
  });
});

// GET /ai-plugin.json (FR-046)
app.get("/ai-plugin.json", (_req: Request, res: Response) => {
  res.json({
    schema_version: "v1",
    name_for_human: "ATO Copilot",
    name_for_model: "ato_copilot",
    description_for_human:
      "Compliance assessment and remediation for Azure Government",
    description_for_model:
      "Use this plugin to run NIST 800-53 compliance assessments, generate remediation scripts, and manage ATO processes for Azure Government subscriptions.",
    api: {
      type: "openapi",
      url: "/openapi.json",
    },
    logo_url: "/icon.png",
  });
});

// --- Start server ---

const server = app.listen(PORT, () => {
  console.log(`ATO Copilot M365 Extension listening on port ${PORT}`);
  console.log(`Health: http://localhost:${PORT}/health`);
});

// --- Graceful shutdown (FR-050) ---

function gracefulShutdown(signal: string): void {
  console.log(`Received ${signal}, shutting down gracefully...`);
  server.close(() => {
    console.log("Server closed");
    process.exit(0);
  });
  setTimeout(() => process.exit(1), 10_000); // Force exit after 10s
}

process.on("SIGINT", () => gracefulShutdown("SIGINT"));
process.on("SIGTERM", () => gracefulShutdown("SIGTERM"));

export { app, server, buildCardForResponse };
