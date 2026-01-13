import { useState } from 'react';
import { 
  Play, 
  Bug, 
  ChevronDown, 
  Search,
  Settings,
  Menu,
  Terminal
} from 'lucide-react';
import { motion } from 'motion/react';

export function TopBar() {
  const [showCommandPalette, setShowCommandPalette] = useState(false);

  return (
    <div 
      className="h-12 bg-[#080810] border-b border-[rgba(0,212,255,0.1)] flex items-center justify-between px-4"
      style={{
        backdropFilter: 'blur(10px)',
        backgroundColor: 'rgba(8, 8, 16, 0.95)'
      }}
    >
      {/* Left Section */}
      <div className="flex items-center gap-4">
        <div className="flex items-center gap-2">
          <Menu className="w-5 h-5 text-[#8b8b9a] cursor-pointer hover:text-[#00d4ff] transition-colors" />
          <div className="text-lg font-semibold bg-gradient-to-r from-[#00d4ff] to-[#b967ff] bg-clip-text text-transparent">
            NEXUS IDE
          </div>
        </div>

        {/* Project Switcher */}
        <motion.button
          className="flex items-center gap-2 px-3 py-1.5 bg-[rgba(0,212,255,0.1)] hover:bg-[rgba(0,212,255,0.15)] rounded-lg text-sm transition-all group"
          whileHover={{ scale: 1.02 }}
          whileTap={{ scale: 0.98 }}
        >
          <Terminal className="w-4 h-4 text-[#00d4ff]" />
          <span className="text-[#e0e0e8]">my-project</span>
          <ChevronDown className="w-3 h-3 text-[#8b8b9a] group-hover:text-[#00d4ff] transition-colors" />
        </motion.button>
      </div>

      {/* Center - Command Palette */}
      <div className="flex-1 max-w-xl mx-auto">
        <motion.div
          className="relative"
          whileHover={{ scale: 1.01 }}
        >
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[#8b8b9a]" />
          <input
            type="text"
            placeholder="Search or type a command... (Ctrl+P)"
            className="w-full bg-[#1a1a24] border border-[rgba(0,212,255,0.2)] rounded-xl pl-10 pr-4 py-2 text-sm text-[#e0e0e8] placeholder:text-[#8b8b9a] focus:outline-none focus:border-[#00d4ff] focus:shadow-[0_0_20px_rgba(0,212,255,0.3)] transition-all"
            onFocus={() => setShowCommandPalette(true)}
            onBlur={() => setTimeout(() => setShowCommandPalette(false), 200)}
          />
          <div className="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-[#8b8b9a] bg-[#0a0a0f] px-2 py-0.5 rounded border border-[rgba(0,212,255,0.2)]">
            ⌘P
          </div>
        </motion.div>
      </div>

      {/* Right Section - Run Controls */}
      <div className="flex items-center gap-2">
        <motion.button
          className="flex items-center gap-2 px-4 py-1.5 bg-gradient-to-r from-[#00d4ff] to-[#00ffff] hover:from-[#00ffff] hover:to-[#00d4ff] rounded-lg text-sm transition-all relative overflow-hidden group"
          whileHover={{ scale: 1.05, boxShadow: '0 0 25px rgba(0, 212, 255, 0.5)' }}
          whileTap={{ scale: 0.95 }}
        >
          <motion.div
            className="absolute inset-0 bg-white opacity-0 group-hover:opacity-20 transition-opacity"
            initial={{ x: '-100%' }}
            whileHover={{ x: '100%' }}
            transition={{ duration: 0.6 }}
          />
          <Play className="w-4 h-4 text-[#0a0a0f] fill-current" />
          <span className="text-[#0a0a0f] font-medium">Run</span>
        </motion.button>

        <motion.button
          className="flex items-center gap-2 px-4 py-1.5 bg-[rgba(185,103,255,0.15)] hover:bg-[rgba(185,103,255,0.25)] border border-[rgba(185,103,255,0.3)] rounded-lg text-sm transition-all"
          whileHover={{ scale: 1.05, boxShadow: '0 0 20px rgba(185, 103, 255, 0.4)' }}
          whileTap={{ scale: 0.95 }}
        >
          <Bug className="w-4 h-4 text-[#b967ff]" />
          <span className="text-[#e0e0e8]">Debug</span>
        </motion.button>

        <motion.button
          className="w-9 h-9 flex items-center justify-center bg-[rgba(255,255,255,0.05)] hover:bg-[rgba(255,255,255,0.1)] rounded-lg transition-all"
          whileHover={{ scale: 1.05 }}
          whileTap={{ scale: 0.95 }}
        >
          <Settings className="w-4 h-4 text-[#8b8b9a] hover:text-[#00d4ff] transition-colors" />
        </motion.button>
      </div>
    </div>
  );
}
