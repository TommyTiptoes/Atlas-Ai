import { useState } from 'react';
import { TopBar } from '@/app/components/top-bar';
import { IDESidebar } from '@/app/components/ide-sidebar';
import { CodeEditor } from '@/app/components/code-editor';
import { BottomPanel } from '@/app/components/bottom-panel';
import { StatusBar } from '@/app/components/status-bar';
import { DebugPanel } from '@/app/components/debug-panel';
import { KeyboardShortcuts } from '@/app/components/keyboard-shortcuts';
import { LoadingScreen } from '@/app/components/loading-screen';
import { AIChatPanel } from '@/app/components/ai-chat-panel';
import { motion, AnimatePresence } from 'motion/react';
import { Layers, HelpCircle, MessageSquare } from 'lucide-react';

export default function App() {
  const [isBottomPanelExpanded, setIsBottomPanelExpanded] = useState(true);
  const [showDebugPanel, setShowDebugPanel] = useState(false);
  const [showAIChat, setShowAIChat] = useState(false);
  const [showShortcuts, setShowShortcuts] = useState(false);
  const [isLoading, setIsLoading] = useState(true);

  if (isLoading) {
    return <LoadingScreen onComplete={() => setIsLoading(false)} />;
  }

  return (
    <div 
      className="h-screen w-screen flex flex-col overflow-hidden dark"
      style={{
        fontFamily: "'Inter', sans-serif",
        background: 'radial-gradient(ellipse at top, rgba(0, 212, 255, 0.05) 0%, #0a0a0f 50%)'
      }}
    >
      {/* Top Bar */}
      <TopBar />

      {/* Main Content Area */}
      <div className="flex-1 flex overflow-hidden">
        {/* Sidebar */}
        <IDESidebar />

        {/* Editor Area */}
        <div className="flex-1 flex flex-col overflow-hidden">
          <div className="flex-1 flex overflow-hidden">
            <CodeEditor />
            
            {/* Debug Panel (Right Side) */}
            {showDebugPanel && <DebugPanel />}
          </div>
          
          {/* Bottom Panel */}
          <BottomPanel 
            isExpanded={isBottomPanelExpanded}
            onToggle={() => setIsBottomPanelExpanded(!isBottomPanelExpanded)}
          />
        </div>
      </div>

      {/* Status Bar */}
      <StatusBar />

      {/* View Mode Toggle Button */}
      <motion.button
        className="fixed bottom-8 right-8 w-14 h-14 bg-gradient-to-br from-[#00d4ff] to-[#b967ff] rounded-2xl flex items-center justify-center z-50 group"
        onClick={() => setShowDebugPanel(!showDebugPanel)}
        whileHover={{ 
          scale: 1.1,
          boxShadow: '0 0 30px rgba(0, 212, 255, 0.6), 0 0 60px rgba(185, 103, 255, 0.4)'
        }}
        whileTap={{ scale: 0.9 }}
        style={{
          boxShadow: '0 0 20px rgba(0, 212, 255, 0.4), 0 0 40px rgba(185, 103, 255, 0.2)'
        }}
      >
        <Layers className="w-6 h-6 text-[#0a0a0f]" />
        <motion.div
          className="absolute inset-0 rounded-2xl bg-white opacity-0 group-hover:opacity-20 transition-opacity"
        />
      </motion.button>

      {/* Help Button */}
      <motion.button
        className="fixed bottom-8 right-28 w-14 h-14 bg-[rgba(185,103,255,0.2)] hover:bg-[rgba(185,103,255,0.3)] border border-[rgba(185,103,255,0.4)] rounded-2xl flex items-center justify-center z-50 group"
        onClick={() => setShowShortcuts(!showShortcuts)}
        whileHover={{ 
          scale: 1.1,
          boxShadow: '0 0 30px rgba(185, 103, 255, 0.6)'
        }}
        whileTap={{ scale: 0.9 }}
        style={{
          boxShadow: '0 0 15px rgba(185, 103, 255, 0.3)'
        }}
      >
        <HelpCircle className="w-6 h-6 text-[#b967ff]" />
      </motion.button>

      {/* AI Chat Button */}
      <motion.button
        className="fixed bottom-8 right-48 w-14 h-14 bg-[rgba(185,103,255,0.2)] hover:bg-[rgba(185,103,255,0.3)] border border-[rgba(185,103,255,0.4)] rounded-2xl flex items-center justify-center z-50 group"
        onClick={() => setShowAIChat(!showAIChat)}
        whileHover={{ 
          scale: 1.1,
          boxShadow: '0 0 30px rgba(185, 103, 255, 0.6)'
        }}
        whileTap={{ scale: 0.9 }}
        style={{
          boxShadow: '0 0 15px rgba(185, 103, 255, 0.3)'
        }}
      >
        <MessageSquare className="w-6 h-6 text-[#b967ff]" />
      </motion.button>

      {/* Keyboard Shortcuts Overlay */}
      <KeyboardShortcuts 
        isOpen={showShortcuts}
        onClose={() => setShowShortcuts(false)}
      />

      {/* AI Chat Panel */}
      <AIChatPanel 
        isOpen={showAIChat}
        onClose={() => setShowAIChat(false)}
      />

      {/* Ambient glow effects */}
      <div className="pointer-events-none fixed inset-0 overflow-hidden">
        <motion.div
          className="absolute -top-40 -left-40 w-96 h-96 bg-[#00d4ff] rounded-full opacity-5 blur-[100px]"
          animate={{
            scale: [1, 1.2, 1],
            opacity: [0.05, 0.08, 0.05]
          }}
          transition={{
            duration: 8,
            repeat: Infinity,
            ease: "easeInOut"
          }}
        />
        <motion.div
          className="absolute -bottom-40 -right-40 w-96 h-96 bg-[#b967ff] rounded-full opacity-5 blur-[100px]"
          animate={{
            scale: [1, 1.3, 1],
            opacity: [0.05, 0.08, 0.05]
          }}
          transition={{
            duration: 10,
            repeat: Infinity,
            ease: "easeInOut",
            delay: 1
          }}
        />
      </div>
    </div>
  );
}