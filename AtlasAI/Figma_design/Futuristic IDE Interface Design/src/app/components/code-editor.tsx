import { useState } from 'react';
import { X, Circle, ChevronDown, ChevronRight } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';

interface Tab {
  id: string;
  name: string;
  path: string;
  modified: boolean;
  content: string;
}

const sampleTabs: Tab[] = [
  {
    id: '1',
    name: 'App.tsx',
    path: 'src/App.tsx',
    modified: false,
    content: `import React from 'react';
import { Header } from './components/Header';
import { Sidebar } from './components/Sidebar';
import { CodeEditor } from './components/CodeEditor';

export default function App() {
  const [theme, setTheme] = React.useState('dark');
  const [sidebarOpen, setSidebarOpen] = React.useState(true);

  return (
    <div className="app-container">
      <Header 
        theme={theme} 
        onThemeChange={setTheme}
      />
      <div className="main-content">
        {sidebarOpen && <Sidebar />}
        <CodeEditor />
      </div>
    </div>
  );
}`
  },
  {
    id: '2',
    name: 'CodeEditor.tsx',
    path: 'src/components/CodeEditor.tsx',
    modified: true,
    content: `import React from 'react';
import { motion } from 'motion/react';

interface CodeEditorProps {
  theme?: 'dark' | 'light';
  language?: string;
}

export function CodeEditor({ theme = 'dark', language = 'typescript' }: CodeEditorProps) {
  const [code, setCode] = React.useState('');

  const handleChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    setCode(e.target.value);
  };

  return (
    <motion.div 
      className="code-editor"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
    >
      <textarea
        value={code}
        onChange={handleChange}
        placeholder="Start coding..."
      />
    </motion.div>
  );
}`
  },
  {
    id: '3',
    name: 'api.ts',
    path: 'src/utils/api.ts',
    modified: true,
    content: `import axios from 'axios';

const API_BASE_URL = process.env.REACT_APP_API_URL || 'http://localhost:3000';

export async function fetchData(endpoint: string) {
  try {
    const response = await axios.get(\`\${API_BASE_URL}/\${endpoint}\`);
    return response.data;
  } catch (error) {
    console.error('API Error:', error);
    throw error;
  }
}

export async function postData(endpoint: string, data: any) {
  return await axios.post(\`\${API_BASE_URL}/\${endpoint}\`, data);
}`
  }
];

function syntaxHighlight(code: string) {
  // Simple syntax highlighting for TypeScript/React
  const keywords = ['import', 'export', 'default', 'function', 'const', 'let', 'var', 'return', 'if', 'else', 'from', 'async', 'await', 'try', 'catch', 'throw', 'interface', 'type'];
  const types = ['React', 'string', 'number', 'boolean', 'any', 'void'];
  
  const lines = code.split('\n');
  
  return lines.map((line, lineIndex) => {
    let highlighted = line;
    
    // Highlight imports
    if (line.trim().startsWith('import')) {
      const parts = line.split("'");
      if (parts.length >= 2) {
        return (
          <div key={lineIndex} className="code-line">
            <span className="line-number">{lineIndex + 1}</span>
            <span className="text-[#b967ff]">import</span>
            <span className="text-[#e0e0e8]"> {parts[0].replace('import', '')}</span>
            <span className="text-[#00ff9f]">'{parts[1]}'</span>
            {parts[2] && <span className="text-[#e0e0e8]">{parts[2]}</span>}
          </div>
        );
      }
    }
    
    // Highlight comments
    if (line.trim().startsWith('//')) {
      return (
        <div key={lineIndex} className="code-line">
          <span className="line-number">{lineIndex + 1}</span>
          <span className="text-[#717182] italic">{line}</span>
        </div>
      );
    }
    
    // Highlight strings
    const stringRegex = /(['"`])(.*?)\1/g;
    const strings: Array<{ match: string; index: number }> = [];
    let match;
    while ((match = stringRegex.exec(line)) !== null) {
      strings.push({ match: match[0], index: match.index });
    }
    
    // Split line into segments for highlighting
    let segments = [];
    let lastIndex = 0;
    
    strings.forEach(({ match, index }) => {
      if (index > lastIndex) {
        segments.push({ text: line.substring(lastIndex, index), type: 'code' });
      }
      segments.push({ text: match, type: 'string' });
      lastIndex = index + match.length;
    });
    
    if (lastIndex < line.length) {
      segments.push({ text: line.substring(lastIndex), type: 'code' });
    }
    
    if (segments.length === 0) {
      segments.push({ text: line, type: 'code' });
    }
    
    return (
      <div key={lineIndex} className="code-line">
        <span className="line-number">{lineIndex + 1}</span>
        {segments.map((segment, i) => {
          if (segment.type === 'string') {
            return <span key={i} className="text-[#00ff9f]">{segment.text}</span>;
          }
          
          // Highlight keywords and types in code segments
          const words = segment.text.split(/(\s+|[{}()[\];,.<>])/);
          return words.map((word, j) => {
            if (keywords.includes(word)) {
              return <span key={`${i}-${j}`} className="text-[#b967ff]">{word}</span>;
            } else if (types.includes(word)) {
              return <span key={`${i}-${j}`} className="text-[#00d4ff]">{word}</span>;
            } else if (word.match(/^[A-Z]/)) {
              return <span key={`${i}-${j}`} className="text-[#00ffff]">{word}</span>;
            } else {
              return <span key={`${i}-${j}`} className="text-[#e0e0e8]">{word}</span>;
            }
          });
        })}
      </div>
    );
  });
}

export function CodeEditor() {
  const [tabs, setTabs] = useState<Tab[]>(sampleTabs);
  const [activeTabId, setActiveTabId] = useState('2');

  const activeTab = tabs.find(t => t.id === activeTabId);

  const closeTab = (tabId: string, e: React.MouseEvent) => {
    e.stopPropagation();
    setTabs(tabs.filter(t => t.id !== tabId));
    if (activeTabId === tabId && tabs.length > 1) {
      const index = tabs.findIndex(t => t.id === tabId);
      const newActiveTab = tabs[index === 0 ? 1 : index - 1];
      setActiveTabId(newActiveTab.id);
    }
  };

  // Get breadcrumb from path
  const getBreadcrumbs = (path: string) => {
    return path.split('/');
  };

  return (
    <div className="flex-1 flex flex-col h-full bg-[#0a0a0f]">
      {/* Tab Bar */}
      <div className="flex items-center bg-[#080810] border-b border-[rgba(0,212,255,0.1)] overflow-x-auto">
        <AnimatePresence>
          {tabs.map((tab) => {
            const isActive = tab.id === activeTabId;
            return (
              <motion.div
                key={tab.id}
                initial={{ opacity: 0, width: 0 }}
                animate={{ opacity: 1, width: 'auto' }}
                exit={{ opacity: 0, width: 0 }}
                className={`flex items-center gap-2 px-4 py-2.5 border-r border-[rgba(0,212,255,0.1)] cursor-pointer relative group min-w-[150px] ${
                  isActive ? 'bg-[#0a0a0f]' : 'bg-[#080810] hover:bg-[rgba(255,255,255,0.03)]'
                }`}
                onClick={() => setActiveTabId(tab.id)}
                style={{
                  boxShadow: isActive ? '0 -2px 0 0 #00d4ff inset, 0 0 20px rgba(0, 212, 255, 0.15)' : 'none'
                }}
              >
                <Circle className={`w-2 h-2 ${tab.modified ? 'text-[#00d4ff] fill-current' : 'text-transparent'}`} />
                <span className={`text-sm ${isActive ? 'text-[#e0e0e8]' : 'text-[#8b8b9a]'}`}>
                  {tab.name}
                </span>
                <button
                  onClick={(e) => closeTab(tab.id, e)}
                  className="ml-auto opacity-0 group-hover:opacity-100 hover:bg-[rgba(255,255,255,0.1)] rounded p-0.5 transition-all"
                >
                  <X className="w-3 h-3 text-[#8b8b9a] hover:text-[#e0e0e8]" />
                </button>
              </motion.div>
            );
          })}
        </AnimatePresence>
      </div>

      {/* Breadcrumb */}
      {activeTab && (
        <div className="flex items-center gap-1 px-4 py-2 bg-[rgba(15,15,26,0.4)] border-b border-[rgba(0,212,255,0.05)] text-xs">
          {getBreadcrumbs(activeTab.path).map((crumb, i, arr) => (
            <div key={i} className="flex items-center gap-1">
              <span className={`${i === arr.length - 1 ? 'text-[#00d4ff]' : 'text-[#8b8b9a]'} hover:text-[#e0e0e8] cursor-pointer transition-colors`}>
                {crumb}
              </span>
              {i < arr.length - 1 && (
                <ChevronRight className="w-3 h-3 text-[#8b8b9a]" />
              )}
            </div>
          ))}
        </div>
      )}

      {/* Editor Content */}
      <div className="flex-1 flex overflow-hidden">
        {/* Main Editor Area */}
        <div className="flex-1 overflow-auto relative">
          <div 
            className="absolute inset-0 font-mono text-[13px] leading-[1.6] p-4"
            style={{ fontFamily: "'JetBrains Mono', monospace" }}
          >
            {activeTab && syntaxHighlight(activeTab.content)}
          </div>
          
          {/* Cursor with glow */}
          <motion.div
            className="absolute w-0.5 h-5 bg-[#00d4ff] pointer-events-none"
            style={{ 
              top: '16px', 
              left: '48px',
              boxShadow: '0 0 10px rgba(0, 212, 255, 0.8), 0 0 20px rgba(0, 212, 255, 0.4)'
            }}
            animate={{ opacity: [1, 0, 1] }}
            transition={{ duration: 1, repeat: Infinity }}
          />
        </div>

        {/* Minimap */}
        <div className="w-24 bg-[rgba(15,15,26,0.5)] border-l border-[rgba(0,212,255,0.1)] overflow-hidden">
          <div className="p-2 space-y-[1px] opacity-40">
            {activeTab && activeTab.content.split('\n').map((_, i) => (
              <div
                key={i}
                className="h-[2px] bg-gradient-to-r from-[#00d4ff] to-[#b967ff] rounded-full"
                style={{ width: `${Math.random() * 100}%` }}
              />
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}