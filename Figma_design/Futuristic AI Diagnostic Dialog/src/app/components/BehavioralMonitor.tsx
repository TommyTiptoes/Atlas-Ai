import { motion } from 'motion/react';
import { useState, useEffect } from 'react';

export function BehavioralMonitor() {
  const [dataPoints, setDataPoints] = useState<number[]>(Array(20).fill(50));

  useEffect(() => {
    const interval = setInterval(() => {
      setDataPoints(prev => [...prev.slice(1), 48 + Math.random() * 4]);
    }, 1000);
    return () => clearInterval(interval);
  }, []);

  return (
    <motion.div
      className="relative bg-[#12121A]/70 backdrop-blur-md border border-green-400/20 rounded-lg p-3"
      initial={{ opacity: 0, x: -20 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{ duration: 0.8, delay: 0.5 }}
    >
      {/* Corner brackets */}
      <div className="absolute top-0 left-0 w-3 h-3 border-l border-t border-green-400/40"></div>
      <div className="absolute top-0 right-0 w-3 h-3 border-r border-t border-green-400/40"></div>

      <div className="text-[10px] tracking-widest text-green-400/80 mb-2" style={{ fontFamily: 'Orbitron, sans-serif' }}>
        BEHAVIORAL ANALYSIS
      </div>

      {/* Baseline graph */}
      <div className="relative w-full h-12 mb-3 bg-[#0A0C14]/60 rounded border border-green-400/10">
        <svg className="w-full h-full" viewBox="0 0 100 50" preserveAspectRatio="none">
          {/* Baseline reference */}
          <line
            x1="0"
            y1="25"
            x2="100"
            y2="25"
            stroke="rgba(34, 197, 94, 0.2)"
            strokeWidth="0.5"
            strokeDasharray="2,2"
          />

          {/* Data line */}
          <motion.polyline
            points={dataPoints
              .map((point, i) => `${(i / (dataPoints.length - 1)) * 100},${point}`)
              .join(' ')}
            fill="none"
            stroke="rgba(34, 197, 94, 0.8)"
            strokeWidth="1"
            initial={{ pathLength: 0 }}
            animate={{ pathLength: 1 }}
            transition={{ duration: 1 }}
          />

          {/* Gradient fill */}
          <defs>
            <linearGradient id="behaviorGradient" x1="0" x2="0" y1="0" y2="1">
              <stop offset="0%" stopColor="rgba(34, 197, 94, 0.3)" />
              <stop offset="100%" stopColor="rgba(34, 197, 94, 0)" />
            </linearGradient>
          </defs>
          <motion.polygon
            points={`0,50 ${dataPoints
              .map((point, i) => `${(i / (dataPoints.length - 1)) * 100},${point}`)
              .join(' ')} 100,50`}
            fill="url(#behaviorGradient)"
          />
        </svg>
      </div>

      {/* Status indicators */}
      <div className="space-y-1.5 text-[9px]">
        <div className="flex justify-between items-center">
          <span className="text-gray-500 tracking-wide">USER BEHAVIOR</span>
          <span className="text-green-400" style={{ fontFamily: 'Space Mono, monospace' }}>CONSISTENT</span>
        </div>

        <div className="flex justify-between items-center">
          <span className="text-gray-500 tracking-wide">PROCESS BEHAVIOR</span>
          <span className="text-green-400" style={{ fontFamily: 'Space Mono, monospace' }}>WITHIN MODEL</span>
        </div>

        {/* Anomaly deviation meter */}
        <div className="pt-1">
          <div className="flex justify-between items-center mb-1">
            <span className="text-gray-500 tracking-wide">ANOMALY DEVIATION</span>
            <span className="text-green-400" style={{ fontFamily: 'Space Mono, monospace' }}>0.02%</span>
          </div>
          <div className="relative h-1 bg-[#0A0C14]/60 rounded-full overflow-hidden">
            <motion.div
              className="absolute inset-y-0 left-0 rounded-full"
              style={{
                width: '0.02%',
                backgroundColor: 'rgba(34, 197, 94, 1)',
              }}
              animate={{
                opacity: [0.6, 1, 0.6],
              }}
              transition={{
                duration: 2,
                repeat: Infinity,
              }}
            />
          </div>
        </div>

        <motion.div 
          className="text-[9px] text-green-400/70 text-center pt-1"
          style={{ fontFamily: 'Space Mono, monospace' }}
          animate={{ opacity: [0.5, 1, 0.5] }}
          transition={{ duration: 3, repeat: Infinity }}
        >
          ZERO-DEVIATION STATE
        </motion.div>
      </div>
    </motion.div>
  );
}
