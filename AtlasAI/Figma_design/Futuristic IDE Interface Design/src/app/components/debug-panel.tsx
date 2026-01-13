import { useState } from 'react';
import { Play, Pause, RotateCw, Square, SkipForward, ArrowDownToLine, ArrowUpFromLine } from 'lucide-react';
import { motion } from 'motion/react';

interface DebugVariable {
  name: string;
  value: string;
  type: string;
}

const debugVariables: DebugVariable[] = [
  { name: 'theme', value: "'dark'", type: 'string' },
  { name: 'sidebarOpen', value: 'true', type: 'boolean' },
  { name: 'activeTab', value: "'2'", type: 'string' },
  { name: 'tabs', value: 'Array(3)', type: 'Array' },
];

const callStack = [
  { function: 'CodeEditor', file: 'CodeEditor.tsx', line: 127 },
  { function: 'App', file: 'App.tsx', line: 18 },
  { function: 'render', file: 'index.tsx', line: 7 },
];

export function DebugPanel() {
  const [isRunning, setIsRunning] = useState(false);

  return (
    <div className="h-full bg-[#0a0a0f] border-l border-[rgba(0,212,255,0.1)] w-80 flex flex-col">
      {/* Debug Controls */}
      <div className="p-3 border-b border-[rgba(0,212,255,0.1)] bg-[rgba(15,15,26,0.6)]">
        <div className="flex items-center gap-2 mb-3">
          <motion.button
            className={`p-2 rounded-lg transition-all ${
              isRunning 
                ? 'bg-[rgba(255,20,147,0.2)] hover:bg-[rgba(255,20,147,0.3)]' 
                : 'bg-[rgba(0,255,159,0.2)] hover:bg-[rgba(0,255,159,0.3)]'
            }`}
            onClick={() => setIsRunning(!isRunning)}
            whileHover={{ scale: 1.05 }}
            whileTap={{ scale: 0.95 }}
            style={{
              boxShadow: isRunning 
                ? '0 0 15px rgba(255, 20, 147, 0.3)' 
                : '0 0 15px rgba(0, 255, 159, 0.3)'
            }}
          >
            {isRunning ? (
              <Pause className="w-4 h-4 text-[#ff1493]" />
            ) : (
              <Play className="w-4 h-4 text-[#00ff9f]" />
            )}
          </motion.button>

          <motion.button
            className="p-2 rounded-lg bg-[rgba(0,212,255,0.1)] hover:bg-[rgba(0,212,255,0.2)] transition-all"
            whileHover={{ scale: 1.05 }}
            whileTap={{ scale: 0.95 }}
          >
            <RotateCw className="w-4 h-4 text-[#00d4ff]" />
          </motion.button>

          <motion.button
            className="p-2 rounded-lg bg-[rgba(139,139,154,0.1)] hover:bg-[rgba(139,139,154,0.2)] transition-all"
            whileHover={{ scale: 1.05 }}
            whileTap={{ scale: 0.95 }}
          >
            <Square className="w-4 h-4 text-[#8b8b9a]" />
          </motion.button>

          <div className="ml-auto flex gap-1">
            <motion.button
              className="p-2 rounded-lg bg-[rgba(185,103,255,0.1)] hover:bg-[rgba(185,103,255,0.2)] transition-all"
              whileHover={{ scale: 1.05 }}
              whileTap={{ scale: 0.95 }}
              title="Step Over"
            >
              <SkipForward className="w-4 h-4 text-[#b967ff]" />
            </motion.button>
            <motion.button
              className="p-2 rounded-lg bg-[rgba(185,103,255,0.1)] hover:bg-[rgba(185,103,255,0.2)] transition-all"
              whileHover={{ scale: 1.05 }}
              whileTap={{ scale: 0.95 }}
              title="Step Into"
            >
              <ArrowDownToLine className="w-4 h-4 text-[#b967ff]" />
            </motion.button>
            <motion.button
              className="p-2 rounded-lg bg-[rgba(185,103,255,0.1)] hover:bg-[rgba(185,103,255,0.2)] transition-all"
              whileHover={{ scale: 1.05 }}
              whileTap={{ scale: 0.95 }}
              title="Step Out"
            >
              <ArrowUpFromLine className="w-4 h-4 text-[#b967ff]" />
            </motion.button>
          </div>
        </div>
      </div>

      {/* Variables Section */}
      <div className="flex-1 overflow-auto">
        <div className="p-3">
          <div className="text-xs uppercase tracking-wider text-[#8b8b9a] mb-2">
            Variables
          </div>
          <div className="space-y-1">
            {debugVariables.map((variable, i) => (
              <motion.div
                key={i}
                className="px-2 py-1.5 rounded hover:bg-white/5 cursor-pointer text-sm font-mono"
                whileHover={{ x: 4 }}
              >
                <div className="flex items-center gap-2">
                  <span className="text-[#00d4ff]">{variable.name}</span>
                  <span className="text-[#8b8b9a]">:</span>
                  <span className="text-[#00ff9f]">{variable.value}</span>
                </div>
                <div className="text-xs text-[#8b8b9a] mt-0.5">{variable.type}</div>
              </motion.div>
            ))}
          </div>
        </div>

        {/* Call Stack */}
        <div className="p-3 border-t border-[rgba(0,212,255,0.1)]">
          <div className="text-xs uppercase tracking-wider text-[#8b8b9a] mb-2">
            Call Stack
          </div>
          <div className="space-y-1">
            {callStack.map((item, i) => (
              <motion.div
                key={i}
                className="px-2 py-1.5 rounded hover:bg-white/5 cursor-pointer text-sm"
                whileHover={{ x: 4 }}
              >
                <div className="text-[#e0e0e8] font-medium">{item.function}</div>
                <div className="text-xs text-[#8b8b9a]">
                  {item.file}:{item.line}
                </div>
              </motion.div>
            ))}
          </div>
        </div>

        {/* Breakpoints */}
        <div className="p-3 border-t border-[rgba(0,212,255,0.1)]">
          <div className="text-xs uppercase tracking-wider text-[#8b8b9a] mb-2">
            Breakpoints
          </div>
          <div className="space-y-1">
            <motion.div
              className="px-2 py-1.5 rounded hover:bg-white/5 cursor-pointer text-sm"
              whileHover={{ x: 4 }}
            >
              <div className="flex items-center gap-2">
                <div className="w-2 h-2 rounded-full bg-[#ff1493]" style={{
                  boxShadow: '0 0 8px rgba(255, 20, 147, 0.6)'
                }} />
                <span className="text-[#e0e0e8]">CodeEditor.tsx:127</span>
              </div>
            </motion.div>
            <motion.div
              className="px-2 py-1.5 rounded hover:bg-white/5 cursor-pointer text-sm"
              whileHover={{ x: 4 }}
            >
              <div className="flex items-center gap-2">
                <div className="w-2 h-2 rounded-full bg-[#ff1493]" style={{
                  boxShadow: '0 0 8px rgba(255, 20, 147, 0.6)'
                }} />
                <span className="text-[#e0e0e8]">App.tsx:18</span>
              </div>
            </motion.div>
          </div>
        </div>
      </div>
    </div>
  );
}