import React, { useState, useCallback, useEffect } from 'react';
import { useChatContext } from '../contexts/ChatContext';

// ────────────────────────────────────────────────────────────────
//  Header Component — US6 (T042 + T043)
// ────────────────────────────────────────────────────────────────

interface HeaderProps {
  sidebarOpen: boolean;
  onToggleSidebar: () => void;
}

export default function Header({ sidebarOpen, onToggleSidebar }: HeaderProps) {
  const { state, createConversation, selectConversation } = useChatContext();
  const [settingsOpen, setSettingsOpen] = useState(false);

  const activeConversation = state.conversations.find(
    (c) => c.id === state.activeConversationId
  );

  const title = activeConversation?.title || 'ATO Copilot';

  const handleNewConversation = useCallback(async () => {
    const conv = await createConversation();
    selectConversation(conv.id);
  }, [createConversation, selectConversation]);

  // Close settings on Escape (FR-045)
  useEffect(() => {
    if (!settingsOpen) return;

    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setSettingsOpen(false);
      }
    };

    window.addEventListener('keydown', handleEscape);
    return () => window.removeEventListener('keydown', handleEscape);
  }, [settingsOpen]);

  return (
    <>
      <header className="flex items-center justify-between px-4 py-3 bg-white border-b border-gray-200 shadow-sm">
        <div className="flex items-center gap-3">
          {/* Hamburger Menu */}
          <button
            onClick={onToggleSidebar}
            className="p-1.5 text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            aria-label={sidebarOpen ? 'Close sidebar' : 'Open sidebar'}
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                d="M4 6h16M4 12h16M4 18h16" />
            </svg>
          </button>

          {/* Title */}
          <h1 className="text-lg font-semibold text-gray-800 truncate max-w-md">{title}</h1>
        </div>

        <div className="flex items-center gap-2">
          {/* New Conversation Shortcut */}
          <button
            onClick={handleNewConversation}
            className="flex items-center gap-1 px-3 py-1.5 text-sm text-gray-600 hover:text-gray-800 hover:bg-gray-100 rounded-lg transition-colors"
            aria-label="New conversation"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
            </svg>
            <span className="hidden sm:inline">New</span>
          </button>

          {/* Settings Gear */}
          <button
            onClick={() => setSettingsOpen(true)}
            className="p-1.5 text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            aria-label="Settings"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.066 2.573c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.573 1.066c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.066-2.573c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          </button>
        </div>
      </header>

      {/* Settings Modal (T043) */}
      {settingsOpen && (
        <SettingsModal onClose={() => setSettingsOpen(false)} />
      )}
    </>
  );
}

// ────────────────────────────────────────────────────────────────
//  Settings Modal (T043)
// ────────────────────────────────────────────────────────────────

function SettingsModal({ onClose }: { onClose: () => void }) {
  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-50"
      onClick={onClose}
    >
      <div
        className="bg-white rounded-xl shadow-xl max-w-md w-full mx-4 p-6"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-xl font-semibold text-gray-800">Settings</h2>
          <button
            onClick={onClose}
            className="p-1 text-gray-400 hover:text-gray-600 rounded"
            aria-label="Close settings"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* App Info */}
        <div className="mb-6">
          <h3 className="text-sm font-medium text-gray-500 uppercase tracking-wider mb-2">
            Application
          </h3>
          <div className="space-y-1 text-sm text-gray-700">
            <p><span className="font-medium">Name:</span> ATO Copilot</p>
            <p><span className="font-medium">Version:</span> 1.0.0</p>
          </div>
        </div>

        {/* Features */}
        <div className="mb-6">
          <h3 className="text-sm font-medium text-gray-500 uppercase tracking-wider mb-2">
            Features
          </h3>
          <ul className="text-sm text-gray-600 space-y-1">
            <li>• AI-powered compliance assistance</li>
            <li>• Real-time messaging via SignalR</li>
            <li>• File attachment analysis</li>
            <li>• Multi-conversation management</li>
            <li>• Markdown rendering with code highlighting</li>
          </ul>
        </div>

        {/* Keyboard Shortcuts */}
        <div>
          <h3 className="text-sm font-medium text-gray-500 uppercase tracking-wider mb-2">
            Keyboard Shortcuts
          </h3>
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-gray-500">
                <th className="py-1 font-medium">Shortcut</th>
                <th className="py-1 font-medium">Action</th>
              </tr>
            </thead>
            <tbody className="text-gray-700">
              <tr>
                <td className="py-1"><kbd className="px-1.5 py-0.5 bg-gray-100 border rounded text-xs">Ctrl+K</kbd></td>
                <td className="py-1">Toggle sidebar</td>
              </tr>
              <tr>
                <td className="py-1"><kbd className="px-1.5 py-0.5 bg-gray-100 border rounded text-xs">Ctrl+N</kbd></td>
                <td className="py-1">New conversation</td>
              </tr>
              <tr>
                <td className="py-1"><kbd className="px-1.5 py-0.5 bg-gray-100 border rounded text-xs">Enter</kbd></td>
                <td className="py-1">Send message</td>
              </tr>
              <tr>
                <td className="py-1"><kbd className="px-1.5 py-0.5 bg-gray-100 border rounded text-xs">Shift+Enter</kbd></td>
                <td className="py-1">New line in message</td>
              </tr>
              <tr>
                <td className="py-1"><kbd className="px-1.5 py-0.5 bg-gray-100 border rounded text-xs">Escape</kbd></td>
                <td className="py-1">Close modal</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
