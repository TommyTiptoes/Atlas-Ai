import { motion } from 'motion/react';

interface StatusBarProps {
  progress: number;
}

export function StatusBar({ progress }: StatusBarProps) {
  return (
    <div className="relative h-[50px] bg-[#0A0C14]/80 backdrop-blur-md border-t border-cyan-400/30">
      {/* Animated gradient sweep */}
      <motion.div
        className="absolute inset-0 opacity-30"
        style={{
          background: 'linear-gradient(90deg, transparent, rgba(34, 211, 238, 0.2), transparent)',
        }}
        animate={{
          x: ['-100%', '100%'],
        }}
        transition={{
          duration: 3,
          repeat: Infinity,
          ease: "linear"
        }}
      />

      <div className="relative h-full px-6 flex items-center justify-between">
        {/* Left: Status text with AI terminology */}
        <div className="flex items-center gap-3">
          <motion.div
            className="text-xs tracking-widest text-cyan-400"
            style={{ fontFamily: 'Space Mono, monospace' }}
            animate={{ opacity: [0.7, 1, 0.7] }}
            transition={{ duration: 2, repeat: Infinity }}
          >
            COGNITIVE_INTEGRITY_SCAN: ACTIVE
          </motion.div>

          {/* Progress ticks */}
          <div className="flex gap-1">
            {Array.from({ length: 10 }).map((_, i) => (
              <motion.div
                key={i}
                className="w-[2px] h-4 rounded-full"
                style={{
                  backgroundColor: i < Math.floor(progress / 10) ? 'rgba(34, 211, 238, 0.8)' : 'rgba(34, 211, 238, 0.3)',
                }}
                animate={{
                  backgroundColor: i < Math.floor(progress / 10) ? 'rgba(34, 211, 238, 0.8)' : 'rgba(34, 211, 238, 0.3)',
                }}
                transition={{ duration: 0.3 }}
              />
            ))}
          </div>
          
          {/* Progress percentage with AI-style formatting */}
          <div className="text-xs text-violet-400" style={{ fontFamily: 'Space Mono, monospace' }}>
            0x{progress.toString(16).toUpperCase().padStart(2, '0')}
          </div>
        </div>

        {/* Right: AI Heartbeat indicator */}
        <div className="flex items-center gap-3">
          <div className="text-[8px] text-gray-600 tracking-widest">
            AI_HEARTBEAT
          </div>
          <svg className="w-16 h-6" viewBox="0 0 64 24">
            <motion.path
              d="M 0,12 L 10,12 L 14,6 L 18,18 L 22,12 L 26,12 L 30,8 L 34,16 L 38,12 L 48,12 L 52,6 L 56,18 L 60,12 L 64,12"
              stroke="rgba(139, 92, 246, 0.6)"
              strokeWidth="2"
              fill="none"
              strokeLinecap="round"
              strokeLinejoin="round"
              initial={{ pathLength: 0, opacity: 0 }}
              animate={{ pathLength: 1, opacity: 1 }}
              transition={{
                pathLength: { duration: 2, repeat: Infinity, ease: "linear" },
                opacity: { duration: 0.5 }
              }}
            />
          </svg>
          
          <motion.div
            className="w-2 h-2 rounded-full"
            style={{ backgroundColor: 'rgba(139, 92, 246, 1)' }}
            animate={{
              scale: [1, 1.5, 1],
              opacity: [0.5, 1, 0.5],
            }}
            transition={{
              duration: 1,
              repeat: Infinity,
            }}
          />
        </div>
      </div>
    </div>
  );
}