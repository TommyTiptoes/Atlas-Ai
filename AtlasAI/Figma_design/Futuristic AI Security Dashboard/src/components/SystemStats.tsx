import { motion } from 'motion/react';
import { useEffect, useState } from 'react';

interface ResourceData {
  label: string;
  percentage: number;
  status: 'normal' | 'warning' | 'critical';
}

export function SystemStats() {
  const [resources, setResources] = useState<ResourceData[]>([
    { label: 'CPU', percentage: 38, status: 'normal' },
    { label: 'RAM', percentage: 76, status: 'warning' },
    { label: 'GPU', percentage: 87, status: 'critical' }
  ]);

  // Simulate live data updates
  useEffect(() => {
    const interval = setInterval(() => {
      setResources(prev => prev.map(resource => ({
        ...resource,
        percentage: Math.max(10, Math.min(95, resource.percentage + (Math.random() - 0.5) * 5))
      })));
    }, 2000);

    return () => clearInterval(interval);
  }, []);

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'critical': return 'rgb(255, 120, 0)';
      case 'warning': return 'rgb(234, 179, 8)';
      default: return 'rgb(0, 230, 255)';
    }
  };

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="text-center mb-4">
        <h3 className="text-sm text-cyan-400/80 tracking-wider uppercase">System Resource Usage</h3>
      </div>

      {/* Resource bars */}
      <div className="space-y-3">
        {resources.map((resource, index) => (
          <motion.div
            key={resource.label}
            className="relative"
            initial={{ opacity: 0, x: 20 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{
              delay: index * 0.1,
              duration: 0.3
            }}
          >
            {/* Label and percentage */}
            <div className="flex justify-between items-center mb-1.5">
              <div className="flex items-center gap-2">
                <motion.div
                  className="w-1.5 h-1.5 rounded-full"
                  style={{ backgroundColor: getStatusColor(resource.status) }}
                  animate={{
                    opacity: [0.5, 1, 0.5],
                    scale: [1, 1.3, 1]
                  }}
                  transition={{
                    duration: 1.5,
                    repeat: Infinity,
                    ease: "easeInOut"
                  }}
                />
                <span className="text-xs text-cyan-300/80 uppercase tracking-wide">{resource.label}</span>
              </div>
              <span className="text-xs font-bold" style={{ color: getStatusColor(resource.status) }}>
                {Math.round(resource.percentage)}%
              </span>
            </div>

            {/* Progress bar */}
            <div className="relative h-2 bg-cyan-400/10 rounded-full overflow-hidden border border-cyan-400/20">
              <motion.div
                className="absolute inset-y-0 left-0 rounded-full"
                style={{
                  backgroundColor: getStatusColor(resource.status),
                  boxShadow: `0 0 10px ${getStatusColor(resource.status).replace('rgb', 'rgba').replace(')', ', 0.6)')}`
                }}
                animate={{
                  width: `${resource.percentage}%`
                }}
                transition={{
                  duration: 1,
                  ease: "easeOut"
                }}
              />

              {/* Scan line effect */}
              <motion.div
                className="absolute inset-0 bg-gradient-to-r from-transparent via-white/30 to-transparent w-8"
                animate={{
                  x: ['-32px', '100%']
                }}
                transition={{
                  duration: 2,
                  repeat: Infinity,
                  ease: "linear",
                  delay: index * 0.3
                }}
              />
            </div>

            {/* Mini graph simulation */}
            <div className="mt-2 h-8 flex items-end gap-0.5">
              {Array.from({ length: 20 }, (_, i) => {
                const height = Math.random() * 100;
                return (
                  <motion.div
                    key={i}
                    className="flex-1 rounded-t"
                    style={{
                      backgroundColor: getStatusColor(resource.status),
                      opacity: 0.4
                    }}
                    animate={{
                      height: `${height}%`,
                      opacity: [0.3, 0.6, 0.3]
                    }}
                    transition={{
                      height: {
                        duration: 2,
                        repeat: Infinity,
                        delay: i * 0.1,
                        ease: "easeInOut"
                      },
                      opacity: {
                        duration: 2,
                        repeat: Infinity,
                        ease: "easeInOut"
                      }
                    }}
                  />
                );
              })}
            </div>
          </motion.div>
        ))}
      </div>

      {/* Global network map */}
      <motion.div
        className="mt-6 p-4 border border-cyan-400/30 bg-cyan-400/5 relative overflow-hidden"
        style={{
          clipPath: 'polygon(8px 0, 100% 0, 100% calc(100% - 8px), calc(100% - 8px) 100%, 0 100%, 0 8px)'
        }}
      >
        <div className="text-[10px] text-cyan-400/80 uppercase tracking-wider mb-3">Global Network Status</div>
        
        {/* World map ASCII-style */}
        <div className="relative h-24 flex items-center justify-center opacity-40">
          <svg viewBox="0 0 200 100" className="w-full h-full">
            {/* Simplified world continents outline */}
            <motion.path
              d="M20,40 L30,35 L40,40 L45,35 L50,40 L55,35 L65,40 L70,45 L65,50 L60,55 L50,50 L40,55 L30,50 L25,45 Z"
              fill="none"
              stroke="rgba(0, 230, 255, 0.5)"
              strokeWidth="0.5"
              animate={{
                opacity: [0.3, 0.6, 0.3]
              }}
              transition={{
                duration: 3,
                repeat: Infinity,
                ease: "easeInOut"
              }}
            />
            <motion.path
              d="M80,30 L90,25 L100,30 L110,25 L120,30 L125,35 L120,45 L110,50 L100,45 L90,50 L85,45 L80,40 Z"
              fill="none"
              stroke="rgba(0, 230, 255, 0.5)"
              strokeWidth="0.5"
              animate={{
                opacity: [0.3, 0.6, 0.3]
              }}
              transition={{
                duration: 3,
                repeat: Infinity,
                delay: 0.5,
                ease: "easeInOut"
              }}
            />

            {/* Network nodes */}
            {[[30, 45], [60, 40], [90, 35], [110, 40], [140, 45], [170, 35]].map(([x, y], i) => (
              <g key={i}>
                <motion.circle
                  cx={x}
                  cy={y}
                  r="2"
                  fill="rgba(0, 230, 255, 0.8)"
                  animate={{
                    r: [2, 3, 2],
                    opacity: [0.6, 1, 0.6]
                  }}
                  transition={{
                    duration: 2,
                    repeat: Infinity,
                    delay: i * 0.2,
                    ease: "easeInOut"
                  }}
                />
                {/* Connection lines */}
                {i < 5 && (
                  <motion.line
                    x1={x}
                    y1={y}
                    x2={[[30, 45], [60, 40], [90, 35], [110, 40], [140, 45], [170, 35]][i + 1][0]}
                    y2={[[30, 45], [60, 40], [90, 35], [110, 40], [140, 45], [170, 35]][i + 1][1]}
                    stroke="rgba(0, 230, 255, 0.3)"
                    strokeWidth="0.5"
                    animate={{
                      opacity: [0.2, 0.5, 0.2]
                    }}
                    transition={{
                      duration: 2,
                      repeat: Infinity,
                      delay: i * 0.3,
                      ease: "easeInOut"
                    }}
                  />
                )}
              </g>
            ))}
          </svg>
        </div>

        <div className="flex justify-between items-center text-[10px] text-cyan-400/60 mt-2">
          <div className="flex items-center gap-2">
            <motion.div
              className="w-1.5 h-1.5 rounded-full bg-cyan-400"
              animate={{
                opacity: [0.5, 1, 0.5]
              }}
              transition={{
                duration: 1.5,
                repeat: Infinity,
                ease: "easeInOut"
              }}
            />
            <span className="uppercase tracking-wide">6 Nodes Active</span>
          </div>
          <span className="uppercase tracking-wide">Latency: 12ms</span>
        </div>

        {/* Scan effect */}
        <motion.div
          className="absolute inset-0 bg-gradient-to-b from-transparent via-cyan-400/10 to-transparent h-full"
          animate={{
            y: ['-100%', '200%']
          }}
          transition={{
            duration: 3,
            repeat: Infinity,
            ease: "linear"
          }}
        />
      </motion.div>
    </div>
  );
}
