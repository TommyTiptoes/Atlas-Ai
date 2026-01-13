import { motion } from 'motion/react';

interface StatusBarProps {
  variant: 'stable' | 'scanning' | 'alert';
}

export function StatusBar({ variant }: StatusBarProps) {
  const navItems = [
    { label: 'CONFIGURE', icon: '◇' },
    { label: 'SETTINGS', icon: '◈' },
    { label: 'SECURE', icon: '◆' },
    { label: 'BACKUP', icon: '◇' },
    { label: 'HELP', icon: '◈' }
  ];

  return (
    <div className="mt-8 relative">
      {/* Sweep light effect */}
      {variant === 'scanning' && (
        <motion.div
          className="absolute inset-0 bg-gradient-to-r from-transparent via-cyan-400/20 to-transparent"
          animate={{
            x: ['-100%', '200%']
          }}
          transition={{
            duration: 2,
            repeat: Infinity,
            ease: "linear"
          }}
        />
      )}

      {/* Status indicators */}
      <div className="flex justify-center gap-3 mb-4">
        {[0, 1, 2, 3, 4].map((i) => (
          <motion.div
            key={i}
            className="w-2 h-2 rounded-full bg-cyan-400"
            animate={{
              opacity: variant === 'scanning' ? [0.3, 1, 0.3] : [0.5, 0.8, 0.5],
              scale: variant === 'scanning' ? [1, 1.3, 1] : [1, 1.1, 1]
            }}
            transition={{
              duration: 1.5,
              repeat: Infinity,
              delay: i * 0.2,
              ease: "easeInOut"
            }}
            style={{
              boxShadow: '0 0 10px rgba(0, 230, 255, 0.8)'
            }}
          />
        ))}
      </div>

      {/* Navigation buttons */}
      <div className="flex justify-center gap-4">
        {navItems.map((item, index) => (
          <motion.button
            key={item.label}
            className="relative px-8 py-3 text-sm font-bold text-cyan-400 uppercase tracking-wider border border-cyan-400/40 backdrop-blur-sm overflow-hidden group"
            style={{
              clipPath: 'polygon(12px 0, 100% 0, 100% calc(100% - 12px), calc(100% - 12px) 100%, 0 100%, 0 12px)',
              boxShadow: '0 0 15px rgba(0, 230, 255, 0.2), inset 0 0 15px rgba(0, 230, 255, 0.05)'
            }}
            whileHover={{
              scale: 1.05,
              boxShadow: '0 0 25px rgba(0, 230, 255, 0.4), inset 0 0 25px rgba(0, 230, 255, 0.1)'
            }}
            whileTap={{
              scale: 0.98
            }}
            transition={{
              duration: 0.15,
              ease: "easeOut"
            }}
          >
            {/* Background glow on hover */}
            <motion.div
              className="absolute inset-0 bg-cyan-400/10"
              initial={{ opacity: 0 }}
              whileHover={{ opacity: 1 }}
              transition={{ duration: 0.2 }}
            />

            {/* Corner markers */}
            <div className="absolute top-0 left-0 w-3 h-3 border-t-2 border-l-2 border-cyan-400" />
            <div className="absolute bottom-0 right-0 w-3 h-3 border-b-2 border-r-2 border-cyan-400" />

            {/* Icon */}
            <motion.span
              className="inline-block mr-2 text-cyan-400"
              animate={{
                rotate: [0, 180, 360]
              }}
              transition={{
                duration: 8,
                repeat: Infinity,
                ease: "linear",
                delay: index * 0.5
              }}
            >
              {item.icon}
            </motion.span>

            {/* Label */}
            <span className="relative z-10">{item.label}</span>

            {/* Scan line effect */}
            <motion.div
              className="absolute inset-0 bg-gradient-to-r from-transparent via-cyan-400/30 to-transparent"
              animate={{
                x: ['-200%', '200%']
              }}
              transition={{
                duration: 3,
                repeat: Infinity,
                ease: "linear",
                delay: index * 0.3
              }}
            />

            {/* Shimmer on scanning */}
            {variant === 'scanning' && (
              <motion.div
                className="absolute inset-0 bg-gradient-to-t from-transparent via-cyan-400/20 to-transparent"
                animate={{
                  y: ['-100%', '100%']
                }}
                transition={{
                  duration: 1.5,
                  repeat: Infinity,
                  ease: "linear",
                  delay: index * 0.2
                }}
              />
            )}
          </motion.button>
        ))}
      </div>

      {/* Bottom status line */}
      <div className="mt-4 flex items-center justify-center gap-4 text-xs text-cyan-400/60">
        <motion.div
          className="flex items-center gap-2"
          animate={{
            opacity: [0.4, 1, 0.4]
          }}
          transition={{
            duration: 2,
            repeat: Infinity,
            ease: "easeInOut"
          }}
        >
          <div className="w-1.5 h-1.5 rounded-full bg-cyan-400" style={{ boxShadow: '0 0 5px rgba(0, 230, 255, 0.8)' }} />
          <span className="uppercase tracking-wider">System Status: {variant === 'scanning' ? 'Active Scan' : 'Secure'}</span>
        </motion.div>
        <div className="w-px h-4 bg-cyan-400/30" />
        <motion.div
          className="flex items-center gap-2"
          animate={{
            opacity: [0.4, 1, 0.4]
          }}
          transition={{
            duration: 2,
            repeat: Infinity,
            delay: 0.5,
            ease: "easeInOut"
          }}
        >
          <div className="w-1.5 h-1.5 rounded-full bg-cyan-400" style={{ boxShadow: '0 0 5px rgba(0, 230, 255, 0.8)' }} />
          <span className="uppercase tracking-wider">Neural Core Online</span>
        </motion.div>
        <div className="w-px h-4 bg-cyan-400/30" />
        <motion.div
          className="flex items-center gap-2"
          animate={{
            opacity: [0.4, 1, 0.4]
          }}
          transition={{
            duration: 2,
            repeat: Infinity,
            delay: 1,
            ease: "easeInOut"
          }}
        >
          <div className="w-1.5 h-1.5 rounded-full bg-cyan-400" style={{ boxShadow: '0 0 5px rgba(0, 230, 255, 0.8)' }} />
          <span className="uppercase tracking-wider">Threats Neutralized: 0</span>
        </motion.div>
      </div>
    </div>
  );
}
