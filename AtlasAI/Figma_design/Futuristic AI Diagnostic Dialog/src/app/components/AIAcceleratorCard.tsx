import { motion } from 'motion/react';
import { useState, useEffect } from 'react';

export function AIAcceleratorCard() {
  const [tensorLoad, setTensorLoad] = useState(67);
  const [inferenceRate, setInferenceRate] = useState(4821);

  useEffect(() => {
    const interval = setInterval(() => {
      setTensorLoad(65 + Math.random() * 8);
      setInferenceRate(4800 + Math.random() * 100);
    }, 2000);
    return () => clearInterval(interval);
  }, []);

  return (
    <motion.div
      className="relative bg-[#12121A]/70 backdrop-blur-md border border-violet-400/20 rounded-lg p-3"
      initial={{ opacity: 0, scale: 0.95 }}
      animate={{ opacity: 1, scale: 1 }}
      transition={{ duration: 0.8, delay: 0.4 }}
    >
      {/* Corner brackets */}
      <div className="absolute top-0 left-0 w-3 h-3 border-l border-t border-violet-400/40"></div>
      <div className="absolute top-0 right-0 w-3 h-3 border-r border-t border-violet-400/40"></div>
      <div className="absolute bottom-0 left-0 w-3 h-3 border-l border-b border-violet-400/40"></div>
      <div className="absolute bottom-0 right-0 w-3 h-3 border-r border-b border-violet-400/40"></div>

      <div className="text-[10px] tracking-widest text-violet-400/80 mb-3" style={{ fontFamily: 'Orbitron, sans-serif' }}>
        AI ACCELERATOR / GPU
      </div>

      {/* Silicon chip visualization */}
      <div className="relative w-full h-20 mb-3 bg-[#0A0C14]/60 rounded border border-violet-400/10 overflow-hidden">
        {/* Grid pattern */}
        <div className="absolute inset-0">
          <svg className="w-full h-full opacity-30">
            <defs>
              <pattern id="chip-grid" width="8" height="8" patternUnits="userSpaceOnUse">
                <rect width="8" height="8" fill="none" stroke="rgba(139, 92, 246, 0.3)" strokeWidth="0.5" />
              </pattern>
            </defs>
            <rect width="100%" height="100%" fill="url(#chip-grid)" />
          </svg>
        </div>

        {/* Heat map gradient */}
        <motion.div
          className="absolute inset-0"
          style={{
            background: `linear-gradient(135deg, 
              rgba(139, 92, 246, 0) 0%, 
              rgba(139, 92, 246, ${tensorLoad / 200}) 50%, 
              rgba(236, 72, 153, ${tensorLoad / 150}) 100%)`,
          }}
          animate={{
            opacity: [0.6, 0.9, 0.6],
          }}
          transition={{
            duration: 3,
            repeat: Infinity,
          }}
        />

        {/* Active traces */}
        {Array.from({ length: 8 }).map((_, i) => (
          <motion.div
            key={i}
            className="absolute h-[1px]"
            style={{
              width: `${20 + Math.random() * 60}%`,
              top: `${10 + i * 10}%`,
              left: `${Math.random() * 20}%`,
              backgroundColor: 'rgba(139, 92, 246, 1)',
              boxShadow: '0 0 4px rgba(139, 92, 246, 0.8)',
            }}
            animate={{
              opacity: [0, 1, 0],
              scaleX: [0, 1, 0],
            }}
            transition={{
              duration: 2,
              repeat: Infinity,
              delay: i * 0.3,
              repeatDelay: 1,
            }}
          />
        ))}
      </div>

      {/* Metrics */}
      <div className="space-y-2">
        <div className="flex justify-between items-center">
          <span className="text-[9px] text-gray-500 tracking-wide">TENSOR LOAD</span>
          <span className="text-xs text-violet-400" style={{ fontFamily: 'Space Mono, monospace' }}>
            {tensorLoad.toFixed(1)}%
          </span>
        </div>

        <div className="flex justify-between items-center">
          <span className="text-[9px] text-gray-500 tracking-wide">INFERENCE RATE</span>
          <span className="text-xs text-cyan-400" style={{ fontFamily: 'Space Mono, monospace' }}>
            {Math.floor(inferenceRate)} <span className="text-[9px] text-gray-500">ops/s</span>
          </span>
        </div>

        <div className="flex justify-between items-center">
          <span className="text-[9px] text-gray-500 tracking-wide">MODEL MEMORY</span>
          <span className="text-xs text-violet-400" style={{ fontFamily: 'Space Mono, monospace' }}>
            8.2 <span className="text-[9px] text-gray-500">GB</span>
          </span>
        </div>

        {/* Temperature bar with gradient */}
        <div className="pt-1">
          <div className="flex justify-between items-center mb-1">
            <span className="text-[9px] text-gray-500 tracking-wide">THERMAL STATE</span>
            <span className="text-[10px] text-green-400" style={{ fontFamily: 'Space Mono, monospace' }}>68°C</span>
          </div>
          <div className="relative h-1.5 bg-[#0A0C14]/60 rounded-full overflow-hidden">
            <motion.div
              className="absolute inset-y-0 left-0 rounded-full"
              style={{
                width: '68%',
                background: 'linear-gradient(90deg, rgba(34, 197, 94, 0.8), rgba(139, 92, 246, 0.8))',
              }}
              animate={{
                opacity: [0.8, 1, 0.8],
              }}
              transition={{
                duration: 2,
                repeat: Infinity,
              }}
            />
          </div>
        </div>
      </div>
    </motion.div>
  );
}
