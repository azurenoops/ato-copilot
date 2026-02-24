import React, { useState, useEffect, useCallback } from 'react';
import { ChatProvider, useChatContext } from './contexts/ChatContext';
import ChatWindow from './components/ChatWindow';
import ConversationList from './components/ConversationList';
import Header from './components/Header';
import './styles/App.css';

// ────────────────────────────────────────────────────────────────
//  App Root — wraps in ChatProvider, provides layout
// ────────────────────────────────────────────────────────────────

function App() {
  return (
    <ChatProvider>
      <AppLayout />
    </ChatProvider>
  );
}

function AppLayout() {
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const { createConversation, selectConversation } = useChatContext();

  // ─── Global Keyboard Shortcuts (US6 — T044) ─────────────────

  const handleKeyDown = useCallback(
    (e: globalThis.KeyboardEvent) => {
      // Ctrl+K / Cmd+K: Toggle sidebar
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault();
        setSidebarOpen((prev) => !prev);
      }

      // Ctrl+N / Cmd+N: New conversation
      if ((e.ctrlKey || e.metaKey) && e.key === 'n') {
        e.preventDefault();
        createConversation().then((conv) => {
          selectConversation(conv.id);
        });
      }
    },
    [createConversation, selectConversation]
  );

  useEffect(() => {
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [handleKeyDown]);

  return (
    <div className="flex flex-col h-screen bg-gray-50">
      <Header
        sidebarOpen={sidebarOpen}
        onToggleSidebar={() => setSidebarOpen((prev) => !prev)}
      />
      <div className="flex flex-1 overflow-hidden">
        {/* Sidebar */}
        <div
          className={`${
            sidebarOpen ? 'w-72' : 'w-0'
          } transition-all duration-300 overflow-hidden border-r border-gray-200 bg-white flex-shrink-0`}
        >
          <ConversationList />
        </div>

        {/* Main Chat Area */}
        <ChatWindow />
      </div>
    </div>
  );
}

export default App;
