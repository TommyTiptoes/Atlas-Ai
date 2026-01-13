import { GitBranch, AlertCircle, Wifi, Zap, Check } from 'lucide-react';
import { motion } from 'motion/react';

export function StatusBar() {
  return (
    <div 
      className="h-7 bg-[#080810] border-t border-[rgba(0,212,255,0.15)] flex items-center justify-between px-3 text-xs"
      style={{
        backdropFilter: 'blur(10px)',
        backgroundColor: 'rgba(8, 8, 16, 0.98)',
        boxShadow: '0 -1px 20px rgba(0, 212, 255, 0.05)'
      }}
    >
      {/* Left Section */}
      <div className="flex items-center gap-4">
        <motion.div 
          className="flex items-center gap-1.5 px-2 py-0.5 bg-[rgba(0,212,255,0.1)] hover:bg-[rgba(0,212,255,0.15)] rounded cursor-pointer transition-all group"
          whileHover={{ scale: 1.05 }}
          whileTap={{ scale: 0.95 }}
        >
          <GitBranch className="w-3 h-3 text-[#00d4ff]" />
          <span className="text-[#e0e0e8]">main</span>
          <motion.div
            className="w-1.5 h-1.5 rounded-full bg-[#00ff9f]"
            animate={{ 
              scale: [1, 1.2, 1],
              opacity: [0.6, 1, 0.6]
            }}
            transition={{ 
              duration: 2, 
              repeat: Infinity,
              ease: "easeInOut"
            }}
            style={{
              boxShadow: '0 0 8px rgba(0, 255, 159, 0.8)'
            }}
          />
        </motion.div>

        <div className="flex items-center gap-1.5 text-[#8b8b9a] hover:text-[#e0e0e8] cursor-pointer transition-colors">
          <AlertCircle className="w-3 h-3 text-[#ff1493]" />
          <span>3 Errors</span>
        </div>

        <div className="flex items-center gap-1.5 text-[#8b8b9a] hover:text-[#e0e0e8] cursor-pointer transition-colors">
          <AlertCircle className="w-3 h-3 text-[#ffaa00]" />
          <span>1 Warning</span>
        </div>
      </div>

      {/* Right Section */}
      <div className="flex items-center gap-4">
        <div className="flex items-center gap-1.5 text-[#8b8b9a]">
          <span>Ln 42, Col 18</span>
        </div>

        <div className="flex items-center gap-1.5 text-[#8b8b9a]">
          <span>TypeScript JSX</span>
        </div>

        <div className="flex items-center gap-1.5 text-[#8b8b9a]">
          <span>UTF-8</span>
        </div>

        <div className="flex items-center gap-1.5 text-[#8b8b9a]">
          <span>LF</span>
        </div>

        <motion.div 
          className="flex items-center gap-1.5 text-[#00ff9f] cursor-pointer"
          whileHover={{ scale: 1.1 }}
        >
          <Check className="w-3 h-3" />
          <span>Prettier</span>
        </motion.div>

        <motion.div 
          className="flex items-center gap-1.5 px-2 py-0.5 bg-[rgba(0,255,159,0.1)] rounded cursor-pointer"
          whileHover={{ scale: 1.05 }}
          animate={{
            boxShadow: [
              '0 0 10px rgba(0, 255, 159, 0.2)',
              '0 0 20px rgba(0, 255, 159, 0.3)',
              '0 0 10px rgba(0, 255, 159, 0.2)'
            ]
          }}
          transition={{ duration: 2, repeat: Infinity }}
        >
          <Wifi className="w-3 h-3 text-[#00ff9f]" />
          <span className="text-[#00ff9f]">Connected</span>
        </motion.div>

        <motion.div 
          className="flex items-center gap-1.5 text-[#00d4ff]"
          animate={{
            opacity: [0.6, 1, 0.6]
          }}
          transition={{ duration: 2, repeat: Infinity }}
        >
          <Zap className="w-3 h-3" />
          <span>AI</span>
        </motion.div>
      </div>
    </div>
  );
}
