/**
 * SSE (Server-Sent Events) client for VS Code extension (FR-029a, FR-029e).
 *
 * Uses native fetch + ReadableStream for line-based SSE parsing.
 * Supports retry with exponential backoff, AbortController cancellation,
 * and automatic fallback to synchronous /mcp/chat on exhausted retries.
 */

/**
 * Parsed SSE event.
 */
export interface SseEvent {
  event: string;
  data: string;
  id?: string;
  timestamp?: string;
}

/**
 * SSE client configuration.
 */
export interface SseClientOptions {
  /** Maximum number of retry attempts (default: 3) */
  maxRetries?: number;
  /** Initial retry delay in milliseconds (default: 1000) */
  initialRetryDelayMs?: number;
  /** Maximum retry delay in milliseconds (default: 30000) */
  maxRetryDelayMs?: number;
  /** Request timeout in milliseconds (default: 120000) */
  timeoutMs?: number;
}

/**
 * Callback for SSE event dispatch.
 */
export type SseEventHandler = (event: SseEvent) => void;

/**
 * Parse a raw SSE chunk into events.
 * SSE format: lines separated by \n, events separated by \n\n.
 * Fields: event:, data:, id:
 */
export function parseSseChunk(chunk: string): SseEvent[] {
  const events: SseEvent[] = [];
  const blocks = chunk.split("\n\n").filter((b) => b.trim().length > 0);

  for (const block of blocks) {
    const lines = block.split("\n");
    let eventType = "message";
    let data = "";
    let id: string | undefined;

    for (const line of lines) {
      if (line.startsWith("event:")) {
        eventType = line.substring(6).trim();
      } else if (line.startsWith("data:")) {
        data = line.substring(5).trim();
      } else if (line.startsWith("id:")) {
        id = line.substring(3).trim();
      }
    }

    if (data) {
      const event: SseEvent = { event: eventType, data };
      if (id) {
        event.id = id;
      }

      // Try to extract timestamp from JSON data
      try {
        const parsed = JSON.parse(data);
        if (parsed.timestamp) {
          event.timestamp = parsed.timestamp;
        }
      } catch {
        // Not JSON — leave timestamp undefined
      }

      events.push(event);
    }
  }

  return events;
}

/**
 * SSE client for streaming responses from MCP Server.
 */
export class SseClient {
  private readonly maxRetries: number;
  private readonly initialRetryDelayMs: number;
  private readonly maxRetryDelayMs: number;
  private readonly timeoutMs: number;

  constructor(options?: SseClientOptions) {
    this.maxRetries = options?.maxRetries ?? 3;
    this.initialRetryDelayMs = options?.initialRetryDelayMs ?? 1000;
    this.maxRetryDelayMs = options?.maxRetryDelayMs ?? 30000;
    this.timeoutMs = options?.timeoutMs ?? 120000;
  }

  /**
   * Open an SSE stream and dispatch events to the handler.
   * Returns when the stream completes or is aborted.
   *
   * @param url - Full URL to the SSE endpoint (e.g. http://localhost:3001/mcp/chat/stream)
   * @param body - Request body for POST
   * @param headers - HTTP headers (must include Content-Type and Authorization)
   * @param onEvent - Callback for each parsed SSE event
   * @param abortController - AbortController for cancellation (Constitution VIII)
   * @returns true if stream completed, false if fallback is needed
   */
  public async stream(
    url: string,
    body: unknown,
    headers: Record<string, string>,
    onEvent: SseEventHandler,
    abortController?: AbortController
  ): Promise<boolean> {
    let retries = 0;
    let delay = this.initialRetryDelayMs;

    while (retries <= this.maxRetries) {
      try {
        const controller = abortController ?? new AbortController();

        // Set timeout
        const timeoutId = setTimeout(() => controller.abort(), this.timeoutMs);

        const response = await fetch(url, {
          method: "POST",
          headers: {
            ...headers,
            Accept: "text/event-stream",
          },
          body: JSON.stringify(body),
          signal: controller.signal,
        });

        clearTimeout(timeoutId);

        if (!response.ok) {
          throw new Error(`SSE request failed: ${response.status}`);
        }

        if (!response.body) {
          throw new Error("SSE response has no body");
        }

        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let buffer = "";

        while (true) {
          const { done, value } = await reader.read();

          if (done) {
            // Process remaining buffer
            if (buffer.trim()) {
              const events = parseSseChunk(buffer);
              for (const event of events) {
                onEvent(event);
              }
            }
            return true;
          }

          buffer += decoder.decode(value, { stream: true });

          // Process complete events (separated by \n\n)
          const parts = buffer.split("\n\n");
          // Keep the last potentially incomplete part in the buffer
          buffer = parts.pop() ?? "";

          for (const part of parts) {
            if (part.trim()) {
              const events = parseSseChunk(part + "\n\n");
              for (const event of events) {
                onEvent(event);
              }
            }
          }
        }
      } catch (error) {
        if (abortController?.signal.aborted) {
          // User-initiated cancellation — don't retry
          return false;
        }

        retries++;
        if (retries > this.maxRetries) {
          // Exhausted retries — caller should fall back to sync
          return false;
        }

        // Exponential backoff
        await new Promise((resolve) => setTimeout(resolve, delay));
        delay = Math.min(delay * 2, this.maxRetryDelayMs);
      }
    }

    return false;
  }
}
