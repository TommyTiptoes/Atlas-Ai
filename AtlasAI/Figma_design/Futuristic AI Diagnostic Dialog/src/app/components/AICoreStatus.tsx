import { motion } from 'motion/react';
import { useEffect, useState } from 'react';

export function AICoreStatus() {
  const [activeNodes, setActiveNodes] = useState<number[]>([]);

  useEffect(() => {
    const interval = setInterval(() => {
      setActiveNodes(Array.from({ length: 3 }, () => Math.floor(Math.random() * 12)));
    }, 2000);
    return () => clearInterval(interval);
  }, []);

  // Neural lattice nodes arranged in a spherical pattern
  const nodes = [
    { x: 50, y: 20, z: 0 },
    { x: 80, y: 30, z: 1 },
    { x: 90, y: 50, z: 0.5 },
    { x: 80, y: 70, z: 1 },
    { x: 50, y: 80, z: 0 },
    { x: 20, y: 70, z: 1 },
    { x: 10, y: 50, z: 0.5 },
    { x: 20, y: 30, z: 1 },
    { x: 50, y: 50, z: 0.3 },
    { x: 35, y: 40, z: 0.7 },
    { x: 65, y: 40, z: 0.7 },
    { x: 50, y: 65, z: 0.6 },
  ];

  return (
    <motion.div
      className="relative bg-[#12121A]/70 backdrop-blur-md border border-violet-400/20 rounded-lg p-3"
      initial={{ opacity: 0, scale: 0.95 }}
      animate={{ opacity: 1, scale: 1 }}
      transition={{ duration: 0.8 }}
    >
      {/* Corner brackets */}
      <div className="absolute top-0 left-0 w-3 h-3 border-l border-t border-violet-400/40"></div>
      <div className="absolute top-0 right-0 w-3 h-3 border-r border-t border-violet-400/40"></div>
      <div className="absolute bottom-0 left-0 w-3 h-3 border-l border-b border-violet-400/40"></div>
      <div className="absolute bottom-0 right-0 w-3 h-3 border-r border-b border-violet-400/40"></div>

      <div className="text-[10px] tracking-widest text-violet-400/80 mb-3" style={{ fontFamily: 'Orbitron, sans-serif' }}>
        AI CORE – SENTIENT PROCESSING
      </div>

      {/* Neural lattice visualization */}
      <div className="relative w-full h-32 mb-3">
        <svg className="w-full h-full" viewBox="0 0 100 100">
          {/* Connection lines */}
          {nodes.map((node, i) => 
            nodes.slice(i + 1).map((target, j) => {
              const distance = Math.sqrt(
                Math.pow(target.x - node.x, 2) + 
                Math.pow(target.y - node.y, 2)
              );
              if (distance < 50) {
                const isActive = activeNodes.includes(i) || activeNodes.includes(i + j + 1);
                return (
                  <motion.line
                    key={`${i}-${j}`}
                    x1={node.x}
                    y1={node.y}
                    x2={target.x}
                    y2={target.y}
                    stroke={isActive ? 'rgba(139, 92, 246, 0.6)' : 'rgba(34, 211, 238, 0.2)'}
                    strokeWidth="0.3"
                    animate={{
                      stroke: isActive 
                        ? ['rgba(139, 92, 246, 0.6)', 'rgba(34, 211, 238, 0.4)', 'rgba(139, 92, 246, 0.6)']
                        : 'rgba(34, 211, 238, 0.2)'
                    }}
                    transition={{ duration: 2, repeat: Infinity }}
                  />
                );
              }
              return null;
            })
          )}

          {/* Nodes */}
          {nodes.map((node, i) => {
            const isActive = activeNodes.includes(i);
            const size = 2 + node.z * 2;
            return (
              <motion.circle
                key={i}
                cx={node.x}
                cy={node.y}
                r={size}
                fill={isActive ? 'rgba(139, 92, 246, 1)' : 'rgba(34, 211, 238, 0.8)'}
                animate={{
                  scale: isActive ? [1, 1.5, 1] : 1,
                  opacity: isActive ? [1, 0.6, 1] : [0.8, 1, 0.8],
                }}
                transition={{
                  duration: isActive ? 1 : 3,
                  repeat: Infinity,
                  delay: i * 0.1,
                }}
              />
            );
          })}
        </svg>

        {/* Pulsing glow overlay */}
        <motion.div
          className="absolute inset-0 rounded-lg"
          style={{
            background: 'radial-gradient(circle at center, rgba(139, 92, 246, 0.2), transparent)',
          }}
          animate={{
            opacity: [0.3, 0.6, 0.3],
          }}
          transition={{
            duration: 3,
            repeat: Infinity,
          }}
        />
      </div>

      {/* Status indicators */}
      <div className="space-y-1.5 text-[9px]">
        <div className="flex justify-between items-center">
          <span className="text-gray-500 tracking-wide">COGNITION</span>
          <div className="flex items-center gap-1">
            <motion.div 
              className="w-1 h-1 rounded-full"
              style={{ backgroundColor: 'rgba(34, 197, 94, 1)' }}
              animate={{ opacity: [1, 0.5, 1] }}
              transition={{ duration: 1.5, repeat: Infinity }}
            />
            <span className="text-green-400" style={{ fontFamily: 'Space Mono, monospace' }}>ACTIVE</span>
          </div>
        </div>

        <div className="flex justify-between items-center">
          <span className="text-gray-500 tracking-wide">DECISION LATENCY</span>
          <span className="text-cyan-400" style={{ fontFamily: 'Space Mono, monospace' }}>2.1 ms</span>
        </div>

        <div className="flex justify-between items-center">
          <span className="text-gray-500 tracking-wide">AUTONOMY LEVEL</span>
          <span className="text-violet-400" style={{ fontFamily: 'Space Mono, monospace' }}>SUPERVISED</span>
        </div>

        <div className="flex justify-between items-center">
          <span className="text-gray-500 tracking-wide">NEURAL INTEGRITY</span>
          <span className="text-green-400" style={{ fontFamily: 'Space Mono, monospace' }}>99.8%</span>
        </div>
      </div>

      {/* Heartbeat pulse indicator */}
      <motion.div 
        className="mt-2 h-[1px] w-full"
        style={{ backgroundColor: 'rgba(139, 92, 246, 1)' }}
        animate={{
          opacity: [0.2, 1, 0.2],
          scaleX: [0.8, 1, 0.8],
        }}
        transition={{
          duration: 2,
          repeat: Infinity,
        }}
      />
    </motion.div>
  );
}
