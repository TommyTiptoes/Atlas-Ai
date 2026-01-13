import { motion } from 'motion/react';

type SystemVariant = 'idle' | 'activeScan' | 'alert';

interface SystemCoreProps {
  variant: SystemVariant;
}

export function SystemCore({ variant }: SystemCoreProps) {
  const rotationSpeed = variant === 'activeScan' ? 15 : 30;
  const ringColor = variant === 'alert' ? 'rgba(255, 120, 0, 0.6)' : 'rgba(0, 230, 255, 0.6)';

  return (
    <div className="relative w-96 h-96 flex items-center justify-center">
      {/* Outer rotating ring */}
      <motion.div
        className="absolute inset-0"
        animate={{ rotate: 360 }}
        transition={{
          duration: rotationSpeed,
          repeat: Infinity,
          ease: "linear"
        }}
      >
        <svg viewBox="0 0 200 200" className="w-full h-full">
          {/* Main ring */}
          <motion.circle
            cx="100"
            cy="100"
            r="90"
            fill="none"
            stroke={ringColor}
            strokeWidth="2"
            strokeDasharray="565"
            strokeDashoffset="0"
            animate={{
              strokeDashoffset: variant === 'activeScan' ? [0, -565] : 0,
              opacity: [0.4, 0.8, 0.4]
            }}
            transition={{
              strokeDashoffset: {
                duration: 3,
                repeat: Infinity,
                ease: "linear"
              },
              opacity: {
                duration: 2,
                repeat: Infinity,
                ease: "easeInOut"
              }
            }}
            style={{
              filter: `drop-shadow(0 0 10px ${ringColor})`
            }}
          />

          {/* Secondary ring */}
          <motion.circle
            cx="100"
            cy="100"
            r="85"
            fill="none"
            stroke={ringColor}
            strokeWidth="0.5"
            opacity="0.3"
          />

          {/* Corner markers */}
          {[0, 90, 180, 270].map((angle, i) => (
            <g key={i} transform={`rotate(${angle} 100 100)`}>
              <motion.line
                x1="100"
                y1="10"
                x2="100"
                y2="20"
                stroke={ringColor}
                strokeWidth="2"
                animate={{
                  opacity: [0.4, 1, 0.4]
                }}
                transition={{
                  duration: 1.5,
                  repeat: Infinity,
                  delay: i * 0.2,
                  ease: "easeInOut"
                }}
              />
            </g>
          ))}
        </svg>
      </motion.div>

      {/* Middle rotating ring */}
      <motion.div
        className="absolute inset-8"
        animate={{ rotate: -360 }}
        transition={{
          duration: rotationSpeed * 1.5,
          repeat: Infinity,
          ease: "linear"
        }}
      >
        <svg viewBox="0 0 200 200" className="w-full h-full">
          <motion.circle
            cx="100"
            cy="100"
            r="70"
            fill="none"
            stroke={ringColor}
            strokeWidth="1"
            opacity="0.4"
            strokeDasharray="10 5"
          />
          
          {/* Orbital nodes */}
          {[0, 120, 240].map((angle, i) => (
            <motion.circle
              key={i}
              cx="100"
              cy="30"
              r="3"
              fill={ringColor}
              transform={`rotate(${angle} 100 100)`}
              animate={{
                r: [3, 5, 3],
                opacity: [0.6, 1, 0.6]
              }}
              transition={{
                duration: 1.5,
                repeat: Infinity,
                delay: i * 0.5,
                ease: "easeInOut"
              }}
              style={{
                filter: `drop-shadow(0 0 5px ${ringColor})`
              }}
            />
          ))}
        </svg>
      </motion.div>

      {/* Core structure - database/server icon */}
      <div className="absolute inset-16 flex items-center justify-center">
        <motion.div
          className="relative w-32 h-32"
          animate={{
            scale: variant === 'activeScan' ? [1, 1.05, 1] : [1, 1.02, 1]
          }}
          transition={{
            duration: 2,
            repeat: Infinity,
            ease: "easeInOut"
          }}
        >
          <svg viewBox="0 0 100 100" className="w-full h-full">
            {/* Database layers */}
            {[20, 40, 60].map((y, i) => (
              <g key={i}>
                <motion.ellipse
                  cx="50"
                  cy={y}
                  rx="30"
                  ry="8"
                  fill="none"
                  stroke={ringColor}
                  strokeWidth="1.5"
                  animate={{
                    opacity: [0.4, 0.8, 0.4]
                  }}
                  transition={{
                    duration: 2,
                    repeat: Infinity,
                    delay: i * 0.3,
                    ease: "easeInOut"
                  }}
                  style={{
                    filter: `drop-shadow(0 0 5px ${ringColor})`
                  }}
                />
                {i < 2 && (
                  <>
                    <motion.line
                      x1="20"
                      y1={y}
                      x2="20"
                      y2={y + 20}
                      stroke={ringColor}
                      strokeWidth="1.5"
                      opacity="0.6"
                    />
                    <motion.line
                      x1="80"
                      y1={y}
                      x2="80"
                      y2={y + 20}
                      stroke={ringColor}
                      strokeWidth="1.5"
                      opacity="0.6"
                    />
                  </>
                )}
              </g>
            ))}

            {/* Data flow indicators */}
            {[0, 1, 2].map((i) => (
              <motion.circle
                key={i}
                cx="50"
                cy="20"
                r="2"
                fill={ringColor}
                animate={{
                  cy: [20, 80],
                  opacity: [0, 1, 0]
                }}
                transition={{
                  duration: 2,
                  repeat: Infinity,
                  delay: i * 0.6,
                  ease: "linear"
                }}
              />
            ))}
          </svg>
        </motion.div>

        {/* Protective shield effect */}
        <motion.div
          className="absolute inset-0 rounded-full"
          style={{
            background: `radial-gradient(circle, ${ringColor.replace('0.6', '0.1')} 0%, transparent 70%)`
          }}
          animate={{
            scale: [1, 1.2, 1],
            opacity: [0.3, 0.5, 0.3]
          }}
          transition={{
            duration: 3,
            repeat: Infinity,
            ease: "easeInOut"
          }}
        />
      </div>

      {/* Scan beam effect */}
      {variant === 'activeScan' && (
        <motion.div
          className="absolute inset-0 overflow-hidden rounded-full"
          style={{
            background: 'transparent'
          }}
        >
          <motion.div
            className="absolute inset-x-0 h-1 bg-gradient-to-r from-transparent via-cyan-400 to-transparent"
            style={{
              filter: 'blur(2px)',
              boxShadow: '0 0 20px rgba(0, 230, 255, 0.8)'
            }}
            animate={{
              y: [0, 384, 0]
            }}
            transition={{
              duration: 2,
              repeat: Infinity,
              ease: "linear"
            }}
          />
        </motion.div>
      )}

      {/* Platform base rings */}
      <div className="absolute -bottom-8 left-1/2 -translate-x-1/2">
        <motion.div
          className="relative"
          animate={{
            opacity: [0.3, 0.6, 0.3]
          }}
          transition={{
            duration: 2,
            repeat: Infinity,
            ease: "easeInOut"
          }}
        >
          {[0, 1, 2].map((i) => (
            <div
              key={i}
              className="absolute left-1/2 -translate-x-1/2 border border-cyan-400/30 rounded-full"
              style={{
                width: `${400 + i * 30}px`,
                height: `${20 - i * 6}px`,
                bottom: `${-i * 8}px`,
                transform: `translateX(-50%) perspective(400px) rotateX(75deg)`
              }}
            />
          ))}
        </motion.div>
      </div>
    </div>
  );
}
