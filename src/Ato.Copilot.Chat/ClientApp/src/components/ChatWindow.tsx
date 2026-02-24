import React, { useState, useRef, useEffect, useCallback, KeyboardEvent } from 'react';
import ReactMarkdown from 'react-markdown';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { oneDark } from 'react-syntax-highlighter/dist/esm/styles/prism';
import { useChatContext } from '../contexts/ChatContext';
import {
  ChatMessage,
  MessageRole,
  MessageStatus,
  ConnectionStatus,
  SuggestedAction,
  ToolExecutionResult,
} from '../types/chat';

// ────────────────────────────────────────────────────────────────
//  ChatWindow Component — US1 + US3 + US4 + US5
// ────────────────────────────────────────────────────────────────

const MAX_FILE_SIZE = 10_485_760; // 10 MB

export default function ChatWindow() {
  const { state, sendMessage, dispatch } = useChatContext();
  const [input, setInput] = useState('');
  const [attachments, setAttachments] = useState<File[]>([]);
  const [expandedTools, setExpandedTools] = useState<Set<string>>(new Set());
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Auto-scroll to newest message (FR-027)
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [state.messages]);

  // Focus input on mount
  useEffect(() => {
    inputRef.current?.focus();
  }, [state.activeConversationId]);

  // ─── Input Handling ────────────────────────────────────────────

  const handleSend = useCallback(async () => {
    const content = input.trim();
    if (!content && attachments.length === 0) return;
    if (state.isProcessing) return; // Debounce (FR-043)

    // Auto-generate analysis prompt for file-only send
    let messageContent = content;
    if (!content && attachments.length > 0) {
      messageContent = `Please analyze the attached file(s): ${attachments.map((f) => f.name).join(', ')}`;
    }

    setInput('');
    setAttachments([]);
    await sendMessage(messageContent);
  }, [input, attachments, state.isProcessing, sendMessage]);

  const handleKeyDown = useCallback(
    (e: KeyboardEvent<HTMLTextAreaElement>) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        handleSend();
      }
    },
    [handleSend]
  );

  // ─── File Attachments ──────────────────────────────────────────

  const handleFileSelect = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const files = Array.from(e.target.files || []);
      const validFiles = files.filter((f) => {
        if (f.size > MAX_FILE_SIZE) {
          dispatch({
            type: 'SET_ERROR',
            payload: `File "${f.name}" exceeds the 10 MB limit`,
          });
          return false;
        }
        return true;
      });
      setAttachments((prev) => [...prev, ...validFiles]);
      if (fileInputRef.current) fileInputRef.current.value = '';
    },
    [dispatch]
  );

  const removeAttachment = useCallback((index: number) => {
    setAttachments((prev) => prev.filter((_, i) => i !== index));
  }, []);

  // ─── Tool Panel Toggle ────────────────────────────────────────

  const toggleTool = useCallback((messageId: string) => {
    setExpandedTools((prev) => {
      const next = new Set(prev);
      if (next.has(messageId)) next.delete(messageId);
      else next.add(messageId);
      return next;
    });
  }, []);

  // ─── Suggestion Click ─────────────────────────────────────────

  const handleSuggestionClick = useCallback((prompt: string) => {
    setInput(prompt);
    inputRef.current?.focus();
  }, []);

  // ─── Render ────────────────────────────────────────────────────

  if (!state.activeConversationId) {
    return (
      <div className="flex-1 flex items-center justify-center bg-gray-50 text-gray-500">
        <p>Select or create a conversation to start chatting</p>
      </div>
    );
  }

  return (
    <div className="flex-1 flex flex-col h-full">
      {/* Connection Status Indicator (FR-044) */}
      <ConnectionStatusBar status={state.connectionStatus} />

      {/* Messages Area */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4 scrollbar-thin">
        {state.messages.map((message) => (
          <MessageBubble
            key={message.id}
            message={message}
            isToolExpanded={expandedTools.has(message.id)}
            onToggleTool={() => toggleTool(message.id)}
            onSuggestionClick={handleSuggestionClick}
          />
        ))}

        {/* Processing Indicator (SC-003) */}
        {state.isProcessing && (
          <div className="flex items-center gap-2 text-gray-500 animate-pulse-slow">
            <div className="flex gap-1">
              <span className="w-2 h-2 bg-blue-400 rounded-full animate-bounce" style={{ animationDelay: '0ms' }} />
              <span className="w-2 h-2 bg-blue-400 rounded-full animate-bounce" style={{ animationDelay: '150ms' }} />
              <span className="w-2 h-2 bg-blue-400 rounded-full animate-bounce" style={{ animationDelay: '300ms' }} />
            </div>
            <span className="text-sm">Processing your request...</span>
          </div>
        )}

        {/* Typing Indicator */}
        {state.typingUsers.length > 0 && (
          <div className="text-sm text-gray-400 italic">
            {state.typingUsers.join(', ')} {state.typingUsers.length === 1 ? 'is' : 'are'} typing...
          </div>
        )}

        {/* Error Display */}
        {state.error && (
          <div className="bg-red-50 border border-red-200 rounded-lg p-3 text-red-700 text-sm">
            {state.error}
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Attachment Previews */}
      {attachments.length > 0 && (
        <div className="px-4 py-2 flex flex-wrap gap-2 border-t border-gray-200 bg-gray-50">
          {attachments.map((file, index) => (
            <div
              key={`${file.name}-${index}`}
              className="flex items-center gap-1 bg-white border rounded-full px-3 py-1 text-sm"
            >
              <span className="truncate max-w-32">{file.name}</span>
              <button
                onClick={() => removeAttachment(index)}
                className="text-gray-400 hover:text-red-500 ml-1"
                aria-label={`Remove ${file.name}`}
              >
                ×
              </button>
            </div>
          ))}
        </div>
      )}

      {/* Input Area */}
      <div className="border-t border-gray-200 p-4 bg-white">
        <div className="flex items-end gap-2">
          {/* Attach Button */}
          <button
            onClick={() => fileInputRef.current?.click()}
            className="p-2 text-gray-400 hover:text-gray-600 transition-colors"
            aria-label="Attach file"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                d="M15.172 7l-6.586 6.586a2 2 0 102.828 2.828l6.414-6.586a4 4 0 00-5.656-5.656l-6.415 6.585a6 6 0 108.486 8.486L20.5 13" />
            </svg>
          </button>
          <input
            ref={fileInputRef}
            type="file"
            className="hidden"
            onChange={handleFileSelect}
            multiple
          />

          {/* Message Input */}
          <textarea
            ref={inputRef}
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Type a message... (Enter to send, Shift+Enter for newline)"
            className="flex-1 resize-none border border-gray-300 rounded-lg px-4 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent max-h-32"
            rows={1}
            disabled={state.isProcessing}
          />

          {/* Send Button */}
          <button
            onClick={handleSend}
            disabled={state.isProcessing || (!input.trim() && attachments.length === 0)}
            className="p-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            aria-label="Send message"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" />
            </svg>
          </button>
        </div>
      </div>
    </div>
  );
}

// ────────────────────────────────────────────────────────────────
//  Connection Status Bar (FR-044)
// ────────────────────────────────────────────────────────────────

function ConnectionStatusBar({ status }: { status: ConnectionStatus }) {
  if (status === ConnectionStatus.Connected) {
    return (
      <div className="px-4 py-1 flex items-center gap-2 text-xs text-green-600 bg-green-50 border-b" role="status" aria-label="Real-time features active">
        <span className="w-2 h-2 bg-green-500 rounded-full" />
        Real-time features active
      </div>
    );
  }
  if (status === ConnectionStatus.Reconnecting) {
    return (
      <div className="px-4 py-1 flex items-center gap-2 text-xs text-orange-600 bg-orange-50 border-b" role="status" aria-label="Reconnecting">
        <span className="w-2 h-2 bg-orange-500 rounded-full animate-pulse" />
        Reconnecting...
      </div>
    );
  }
  return (
    <div className="px-4 py-1 flex items-center gap-2 text-xs text-red-600 bg-red-50 border-b" role="status" aria-label="Real-time features unavailable">
      <span className="w-2 h-2 bg-red-500 rounded-full" />
      Real-time features unavailable
    </div>
  );
}

// ────────────────────────────────────────────────────────────────
//  Message Bubble Component
// ────────────────────────────────────────────────────────────────

interface MessageBubbleProps {
  message: ChatMessage;
  isToolExpanded: boolean;
  onToggleTool: () => void;
  onSuggestionClick: (prompt: string) => void;
}

function MessageBubble({ message, isToolExpanded, onToggleTool, onSuggestionClick }: MessageBubbleProps) {
  const isUser = message.role === MessageRole.User;
  const isAssistant = message.role === MessageRole.Assistant;

  return (
    <div className={`flex ${isUser ? 'justify-end' : 'justify-start'} animate-fade-in`}>
      <div
        className={`max-w-[80%] rounded-lg px-4 py-3 ${
          isUser
            ? 'bg-blue-600 text-white'
            : 'bg-white border border-gray-200 text-gray-800'
        }`}
      >
        {/* Message Status */}
        {isUser && message.status !== MessageStatus.Completed && (
          <div className="text-xs opacity-70 mb-1">
            {message.status === MessageStatus.Sending && 'Sending...'}
            {message.status === MessageStatus.Error && '⚠ Failed to send'}
          </div>
        )}

        {/* Message Content with Markdown */}
        {isAssistant ? (
          <div className="prose prose-sm max-w-none">
            <ReactMarkdown
              components={{
                code(props) {
                  const { children, className, ...rest } = props;
                  const match = /language-(\w+)/.exec(className || '');
                  const inline = !match;
                  return !inline ? (
                    <SyntaxHighlighter
                      style={oneDark}
                      language={match[1]}
                      PreTag="div"
                    >
                      {String(children).replace(/\n$/, '')}
                    </SyntaxHighlighter>
                  ) : (
                    <code className={className} {...rest}>
                      {children}
                    </code>
                  );
                },
              }}
            >
              {message.content}
            </ReactMarkdown>
          </div>
        ) : (
          <p className="whitespace-pre-wrap">{message.content}</p>
        )}

        {/* Attachments */}
        {message.attachments && message.attachments.length > 0 && (
          <div className="mt-2 flex flex-wrap gap-1">
            {message.attachments.map((att) => (
              <span
                key={att.id}
                className="inline-flex items-center px-2 py-0.5 rounded text-xs bg-gray-100 text-gray-600"
              >
                📎 {att.fileName}
              </span>
            ))}
          </div>
        )}

        {/* ─── US4: Intent Badge (FR-023) ─────────────────────── */}
        {isAssistant && message.metadata?.intentType && (
          <div className="mt-2">
            <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-purple-100 text-purple-800">
              {String(message.metadata.intentType)}
              {message.metadata.confidence && (
                <span className="ml-1 opacity-75">
                  — {Math.round(Number(message.metadata.confidence) * 100)}%
                </span>
              )}
            </span>
          </div>
        )}

        {/* ─── US4: Tool Execution Panel (FR-024) ─────────────── */}
        {isAssistant && message.toolResult && (
          <div className="mt-2 border border-gray-200 rounded">
            <button
              onClick={onToggleTool}
              className="w-full flex items-center justify-between px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
            >
              <span>
                {(message.toolResult as ToolExecutionResult).success ? '✅' : '❌'}{' '}
                Tool Executed: {(message.toolResult as ToolExecutionResult).toolName}
              </span>
              <span className="text-gray-400">{isToolExpanded ? '▴' : '▾'}</span>
            </button>
            {isToolExpanded && (
              <div className="px-3 py-2 border-t bg-gray-50">
                <pre className="text-xs text-gray-600 overflow-x-auto whitespace-pre-wrap">
                  {JSON.stringify((message.toolResult as ToolExecutionResult).result, null, 2)}
                </pre>
              </div>
            )}
          </div>
        )}

        {/* ─── US4: Workflow Progress (FR-025) ────────────────── */}
        {isAssistant && message.metadata?.toolChain && (
          <WorkflowProgress metadata={message.metadata} />
        )}

        {/* ─── US4: Suggested Actions (FR-026) ────────────────── */}
        {isAssistant && message.metadata?.suggestedActions && (
          <div className="mt-3 space-y-2">
            {(message.metadata.suggestedActions as unknown as SuggestedAction[]).map(
              (action, index) => (
                <button
                  key={index}
                  onClick={() => onSuggestionClick(action.prompt)}
                  className="w-full text-left p-2 border border-blue-200 rounded-lg hover:bg-blue-50 transition-colors"
                >
                  <div className="flex items-center justify-between">
                    <span className="text-sm font-medium text-blue-800">{action.title}</span>
                    {action.priority && (
                      <span className="text-xs px-1.5 py-0.5 rounded bg-blue-100 text-blue-700">
                        {action.priority}
                      </span>
                    )}
                  </div>
                  <p className="text-xs text-gray-500 mt-0.5">{action.description}</p>
                </button>
              )
            )}
          </div>
        )}
      </div>
    </div>
  );
}

// ────────────────────────────────────────────────────────────────
//  Workflow Progress (FR-025)
// ────────────────────────────────────────────────────────────────

function WorkflowProgress({ metadata }: { metadata: Record<string, unknown> }) {
  const toolChain = metadata.toolChain as { completed: number; total: number; status: string; successRate: number } | undefined;
  if (!toolChain) return null;

  const percentage = toolChain.total > 0 ? (toolChain.completed / toolChain.total) * 100 : 0;

  return (
    <div className="mt-2 p-2 bg-gray-50 rounded border">
      <div className="flex items-center justify-between text-xs text-gray-600 mb-1">
        <span>{toolChain.completed}/{toolChain.total} steps</span>
        <span>{toolChain.status}</span>
      </div>
      <div className="w-full bg-gray-200 rounded-full h-1.5">
        <div
          className="bg-blue-600 h-1.5 rounded-full transition-all duration-300"
          style={{ width: `${percentage}%` }}
        />
      </div>
      <div className="text-xs text-gray-500 mt-1">
        Success rate: {Math.round(toolChain.successRate * 100)}%
      </div>
    </div>
  );
}
