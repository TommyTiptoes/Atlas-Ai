import { useState } from 'react';
import { Terminal, AlertCircle, FileText, X, ChevronUp } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';

type PanelTab = 'terminal' | 'problems' | 'output';

interface Problem {
  type: 'error' | 'warning';
  message: string;
  file: string;
  line: number;
  col: number;
}

const sampleProblems: Problem[] = [
  {
    type: 'error',
    message: "Property 'map' does not exist on type 'string'",
    file: 'src/components/CodeEditor.tsx',
    line: 42,
    col: 18
  },
  {
    type: 'warning',
    message: 'Unused variable: theme',
    file: 'src/App.tsx',
    line: 12,
    col: 9
  },
  {
    type: 'error',
    message: "Cannot find module './utils/helpers'",
    file: 'src/components/Header.tsx',
    line: 3,
    col: 21
  }
];

const terminalLines = [
  { type: 'command', text: '$ npm run dev' },
  { type: 'output', text: '' },
  { type: 'output', text: '> my-project@1.0.0 dev' },
  { type: 'output', text: '> vite' },
  { type: 'output', text: '' },
  { type: 'success', text: '  VITE v5.0.0  ready in 423 ms' },
  { type: 'output', text: '' },
  { type: 'info', text: '  ➜  Local:   http://localhost:5173/' },
  { type: 'info', text: '  ➜  Network: use --host to expose' },
  { type: 'output', text: '' },
  { type: 'success', text: '  ✓ Built in 127ms' },
];

interface BottomPanelProps {
  isExpanded: boolean;
  onToggle: () => void;
}

export function BottomPanel({ isExpanded, onToggle }: BottomPanelProps) {
  const [activeTab, setActiveTab] = useState<PanelTab>('terminal');

  const tabs = [
    { id: 'terminal' as PanelTab, label: 'Terminal', icon: Terminal, count: null },
    { id: 'problems' as PanelTab, label: 'Problems', icon: AlertCircle, count: sampleProblems.length },
    { id: 'output' as PanelTab, label: 'Output', icon: FileText, count: null },
  ];

  return (
    <AnimatePresence>
      {isExpanded && (
        <motion.div
          initial={{ height: 0, opacity: 0 }}
          animate={{ height: 280, opacity: 1 }}
          exit={{ height: 0, opacity: 0 }}
          transition={{ duration: 0.3 }}
          className="border-t border-[rgba(0,212,255,0.1)] bg-[#080810] overflow-hidden"
          style={{
            backdropFilter: 'blur(10px)',
            backgroundColor: 'rgba(8, 8, 16, 0.95)'
          }}
        >
          {/* Tab Bar */}
          <div className="flex items-center justify-between bg-[#0a0a0f] border-b border-[rgba(0,212,255,0.1)] px-2">
            <div className="flex items-center">
              {tabs.map((tab) => {
                const Icon = tab.icon;
                const isActive = activeTab === tab.id;
                return (
                  <motion.button
                    key={tab.id}
                    className={`flex items-center gap-2 px-4 py-2 text-sm transition-all relative ${
                      isActive ? 'text-[#e0e0e8]' : 'text-[#8b8b9a] hover:text-[#e0e0e8]'
                    }`}
                    onClick={() => setActiveTab(tab.id)}
                    whileHover={{ scale: 1.05 }}
                  >
                    <Icon className={`w-4 h-4 ${isActive ? 'text-[#00d4ff]' : ''}`} />
                    <span>{tab.label}</span>
                    {tab.count !== null && (
                      <span className={`px-1.5 py-0.5 rounded-full text-xs ${
                        isActive 
                          ? 'bg-[rgba(255,20,147,0.2)] text-[#ff1493]' 
                          : 'bg-[rgba(139,139,154,0.2)] text-[#8b8b9a]'
                      }`}>
                        {tab.count}
                      </span>
                    )}
                    {isActive && (
                      <motion.div
                        className="absolute bottom-0 left-0 right-0 h-0.5 bg-[#00d4ff]"
                        layoutId="bottomTabIndicator"
                        transition={{ type: 'spring', stiffness: 300, damping: 30 }}
                        style={{
                          boxShadow: '0 0 10px rgba(0, 212, 255, 0.6)'
                        }}
                      />
                    )}
                  </motion.button>
                );
              })}
            </div>

            <div className="flex items-center gap-2">
              <motion.button
                className="p-1.5 hover:bg-white/5 rounded transition-all"
                onClick={onToggle}
                whileHover={{ scale: 1.1 }}
                whileTap={{ scale: 0.9 }}
              >
                <ChevronUp className="w-4 h-4 text-[#8b8b9a] hover:text-[#e0e0e8]" />
              </motion.button>
              <motion.button
                className="p-1.5 hover:bg-white/5 rounded transition-all"
                onClick={onToggle}
                whileHover={{ scale: 1.1 }}
                whileTap={{ scale: 0.9 }}
              >
                <X className="w-4 h-4 text-[#8b8b9a] hover:text-[#e0e0e8]" />
              </motion.button>
            </div>
          </div>

          {/* Panel Content */}
          <div className="h-[calc(100%-41px)] overflow-auto">
            {activeTab === 'terminal' && (
              <div 
                className="p-4 font-mono text-sm"
                style={{ fontFamily: "'JetBrains Mono', monospace" }}
              >
                {terminalLines.map((line, i) => (
                  <div key={i} className="py-0.5">
                    {line.type === 'command' && (
                      <span className="text-[#00ff9f]">{line.text}</span>
                    )}
                    {line.type === 'output' && (
                      <span className="text-[#8b8b9a]">{line.text}</span>
                    )}
                    {line.type === 'success' && (
                      <span className="text-[#00ff9f]">{line.text}</span>
                    )}
                    {line.type === 'info' && (
                      <span className="text-[#00d4ff]">{line.text}</span>
                    )}
                  </div>
                ))}
                <div className="flex items-center mt-2">
                  <span className="text-[#00ff9f] mr-2">$</span>
                  <motion.span
                    className="inline-block w-2 h-4 bg-[#00d4ff]"
                    animate={{ opacity: [1, 0, 1] }}
                    transition={{ duration: 1, repeat: Infinity }}
                    style={{
                      boxShadow: '0 0 10px rgba(0, 212, 255, 0.8)'
                    }}
                  />
                </div>
              </div>
            )}

            {activeTab === 'problems' && (
              <div className="divide-y divide-[rgba(0,212,255,0.05)]">
                {sampleProblems.map((problem, i) => (
                  <motion.div
                    key={i}
                    className="p-3 hover:bg-white/5 cursor-pointer transition-all group"
                    whileHover={{ x: 4 }}
                  >
                    <div className="flex items-start gap-3">
                      <AlertCircle 
                        className={`w-4 h-4 mt-0.5 ${
                          problem.type === 'error' ? 'text-[#ff1493]' : 'text-[#ffaa00]'
                        }`}
                        style={{
                          filter: problem.type === 'error' 
                            ? 'drop-shadow(0 0 8px rgba(255, 20, 147, 0.5))'
                            : 'drop-shadow(0 0 8px rgba(255, 170, 0, 0.5))'
                        }}
                      />
                      <div className="flex-1 min-w-0">
                        <div className="text-sm text-[#e0e0e8] mb-1">
                          {problem.message}
                        </div>
                        <div className="text-xs text-[#8b8b9a]">
                          {problem.file} [{problem.line}, {problem.col}]
                        </div>
                      </div>
                    </div>
                  </motion.div>
                ))}
              </div>
            )}

            {activeTab === 'output' && (
              <div 
                className="p-4 font-mono text-sm text-[#8b8b9a]"
                style={{ fontFamily: "'JetBrains Mono', monospace" }}
              >
                [Extension Host] Debugger attached.
                <br />
                [Extension Host] Extension activated.
                <br />
                [Renderer] Loading workspace configuration...
                <br />
                <span className="text-[#00d4ff]">[Info]</span> Build completed successfully
              </div>
            )}
          </div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
