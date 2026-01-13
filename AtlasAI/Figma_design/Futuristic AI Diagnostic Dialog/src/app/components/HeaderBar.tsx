import { motion } from 'motion/react';
import { X, Diamond } from 'lucide-react';

export function HeaderBar() {
  return (
    <div className="relative h-[60px] bg-[#0A0C14]/80 backdrop-blur-md border-b border-cyan-400/30">
      {/* Bottom glow line */}
      <div className="absolute bottom-0 left-0 right-0 h-[2px] bg-gradient-to-r from-transparent via-cyan-400 to-transparent opacity-50"></div>

      <div className="h-full px-6 flex items-center justify-between">
        {/* Left: Title */}
        <div className="flex items-center gap-3">
          <Diamond className="w-4 h-4 text-cyan-400" fill="currentColor" />
          <h1 className="text-lg tracking-[0.3em] text-cyan-400" style={{ fontFamily: 'Orbitron, sans-serif' }}>
            ATLAS AI SECURITY AGENT
          </h1>
          <Diamond className="w-4 h-4 text-cyan-400" fill="currentColor" />
        </div>

        {/* Center-right: Processing status */}
        <div className="flex items-center gap-4">
          {/* Animated status dots */}
          <div className="flex items-center gap-2">
            {[0, 1, 2].map((i) => (
              <motion.div
                key={i}
                className="w-2 h-2 rounded-full"
                style={{ backgroundColor: 'rgba(34, 211, 238, 1)' }}
                animate={{
                  scale: [1, 1.3, 1],
                  opacity: [0.5, 1, 0.5],
                }}
                transition={{
                  duration: 1.5,
                  repeat: Infinity,
                  delay: i * 0.2,
                }}
              />
            ))}
          </div>

          {/* Status pill */}
          <div className="px-3 py-1.5 bg-green-400/10 border border-green-400/30 rounded-full flex items-center gap-2">
            <div className="w-1.5 h-1.5 bg-green-400 rounded-full animate-pulse"></div>
            <span className="text-[10px] tracking-widest text-green-400" style={{ fontFamily: 'Space Mono, monospace' }}>
              THREAT_LEVEL: 0x00
            </span>
          </div>

          {/* Close button */}
          <motion.button
            className="w-8 h-8 rounded-full border border-cyan-400/40 flex items-center justify-center hover:border-cyan-400 hover:bg-cyan-400/10 transition-colors"
            whileHover={{ scale: 1.1 }}
            whileTap={{ scale: 0.95 }}
          >
            <X className="w-4 h-4 text-cyan-400" />
          </motion.button>
        </div>
      </div>
    </div>
  );
}