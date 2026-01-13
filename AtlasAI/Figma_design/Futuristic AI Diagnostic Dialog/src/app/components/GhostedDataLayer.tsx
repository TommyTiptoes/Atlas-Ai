import { motion } from 'motion/react';

export function GhostedDataLayer() {
  return (
    <div className="absolute inset-0 pointer-events-none overflow-hidden opacity-20">
      {/* Ghosted grid patterns */}
      <div className="absolute inset-0">
        <svg className="w-full h-full" viewBox="0 0 900 700">
          {/* Diagonal lines */}
          {Array.from({ length: 30 }).map((_, i) => (
            <motion.line
              key={`diag-${i}`}
              x1={i * 40 - 100}
              y1="0"
              x2={i * 40 + 600}
              y2="700"
              stroke="rgba(34, 211, 238, 0.1)"
              strokeWidth="0.5"
              animate={{
                opacity: [0.1, 0.3, 0.1],
              }}
              transition={{
                duration: 5,
                repeat: Infinity,
                delay: i * 0.2,
              }}
            />
          ))}

          {/* Hexagonal pattern overlay */}
          <defs>
            <pattern id="hex-pattern" x="0" y="0" width="30" height="26" patternUnits="userSpaceOnUse">
              <path
                d="M15,0 L30,7.5 L30,22.5 L15,30 L0,22.5 L0,7.5 Z"
                fill="none"
                stroke="rgba(139, 92, 246, 0.15)"
                strokeWidth="0.5"
              />
            </pattern>
          </defs>
          <rect width="100%" height="100%" fill="url(#hex-pattern)" />

          {/* Radial scan lines */}
          {Array.from({ length: 8 }).map((_, i) => {
            const angle = (i * 360) / 8;
            const x1 = 450;
            const y1 = 350;
            const x2 = 450 + Math.cos((angle * Math.PI) / 180) * 500;
            const y2 = 350 + Math.sin((angle * Math.PI) / 180) * 500;
            
            return (
              <motion.line
                key={`radial-${i}`}
                x1={x1}
                y1={y1}
                x2={x2}
                y2={y2}
                stroke="rgba(34, 211, 238, 0.08)"
                strokeWidth="1"
                strokeDasharray="4,8"
                animate={{
                  strokeDashoffset: [0, -12],
                }}
                transition={{
                  duration: 3,
                  repeat: Infinity,
                  ease: "linear",
                }}
              />
            );
          })}
        </svg>
      </div>

      {/* Floating data fragments */}
      {Array.from({ length: 12 }).map((_, i) => (
        <motion.div
          key={`fragment-${i}`}
          className="absolute text-[7px]"
          style={{
            fontFamily: 'Space Mono, monospace',
            left: `${10 + (i % 4) * 25}%`,
            top: `${15 + Math.floor(i / 4) * 30}%`,
            color: 'rgba(139, 92, 246, 0.4)',
          }}
          animate={{
            opacity: [0, 0.6, 0],
            y: [0, -20, -40],
          }}
          transition={{
            duration: 8,
            repeat: Infinity,
            delay: i * 1.5,
            repeatDelay: 4,
          }}
        >
          {Math.random() > 0.5 
            ? `0x${Math.floor(Math.random() * 0xFFFF).toString(16).toUpperCase().padStart(4, '0')}`
            : `[${Math.random().toFixed(4)}]`
          }
        </motion.div>
      ))}

      {/* Peripheral data clusters */}
      <motion.div
        className="absolute top-[10%] right-[5%] w-20 h-20 rounded-full border border-violet-400/20"
        animate={{
          scale: [1, 1.2, 1],
          opacity: [0.3, 0.5, 0.3],
        }}
        transition={{
          duration: 6,
          repeat: Infinity,
        }}
      >
        <div className="absolute inset-0 flex items-center justify-center text-[8px] text-violet-400/40" style={{ fontFamily: 'Space Mono, monospace' }}>
          NODE_42
        </div>
      </motion.div>

      <motion.div
        className="absolute bottom-[15%] left-[8%] w-16 h-16 rounded-full border border-cyan-400/20"
        animate={{
          scale: [1, 1.3, 1],
          opacity: [0.3, 0.5, 0.3],
        }}
        transition={{
          duration: 5,
          repeat: Infinity,
          delay: 2,
        }}
      >
        <div className="absolute inset-0 flex items-center justify-center text-[8px] text-cyan-400/40" style={{ fontFamily: 'Space Mono, monospace' }}>
          LAYER_3
        </div>
      </motion.div>

      {/* Asymmetric corner elements */}
      <motion.div
        className="absolute top-[25%] left-[3%]"
        animate={{
          rotate: [0, 360],
        }}
        transition={{
          duration: 20,
          repeat: Infinity,
          ease: "linear",
        }}
      >
        <svg width="40" height="40" viewBox="0 0 40 40">
          <polygon
            points="20,5 35,15 35,25 20,35 5,25 5,15"
            fill="none"
            stroke="rgba(34, 211, 238, 0.2)"
            strokeWidth="1"
          />
          <circle cx="20" cy="20" r="3" fill="rgba(34, 211, 238, 0.3)" />
        </svg>
      </motion.div>

      <motion.div
        className="absolute bottom-[20%] right-[10%]"
        animate={{
          rotate: [360, 0],
        }}
        transition={{
          duration: 15,
          repeat: Infinity,
          ease: "linear",
        }}
      >
        <svg width="30" height="30" viewBox="0 0 30 30">
          <rect
            x="5"
            y="5"
            width="20"
            height="20"
            fill="none"
            stroke="rgba(139, 92, 246, 0.2)"
            strokeWidth="1"
            transform="rotate(45 15 15)"
          />
        </svg>
      </motion.div>
    </div>
  );
}
