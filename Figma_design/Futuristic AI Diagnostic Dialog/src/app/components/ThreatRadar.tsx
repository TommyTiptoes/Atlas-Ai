import { motion } from 'motion/react';
import { useState, useEffect } from 'react';

export function ThreatRadar() {
  const [sweepAngle, setSweepAngle] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      setSweepAngle((prev) => (prev + 6) % 360);
    }, 50);
    return () => clearInterval(interval);
  }, []);

  return (
    <motion.div
      className="relative bg-[#12121A]/70 backdrop-blur-md border border-cyan-400/20 rounded-lg p-3"
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.8, delay: 0.2 }}
    >
      {/* Corner brackets */}
      <div className="absolute top-0 left-0 w-3 h-3 border-l border-t border-cyan-400/40"></div>
      <div className="absolute top-0 right-0 w-3 h-3 border-r border-t border-cyan-400/40"></div>

      <div className="text-[10px] tracking-widest text-cyan-400/80 mb-2" style={{ fontFamily: 'Orbitron, sans-serif' }}>
        PREDICTIVE THREAT ANALYSIS
      </div>

      {/* Radar display */}
      <div className="relative w-full aspect-square max-w-[140px] mx-auto mb-2">
        <svg className="w-full h-full" viewBox="0 0 100 100">
          {/* Radar circles */}
          {[20, 40, 60, 80].map((r, i) => (
            <circle
              key={i}
              cx="50"
              cy="50"
              r={r / 2}
              fill="none"
              stroke="rgba(34, 211, 238, 0.15)"
              strokeWidth="0.5"
            />
          ))}

          {/* Crosshairs */}
          <line x1="50" y1="0" x2="50" y2="100" stroke="rgba(34, 211, 238, 0.15)" strokeWidth="0.5" />
          <line x1="0" y1="50" x2="100" y2="50" stroke="rgba(34, 211, 238, 0.15)" strokeWidth="0.5" />

          {/* Sweep line */}
          <motion.line
            x1="50"
            y1="50"
            x2={50 + 40 * Math.cos((sweepAngle * Math.PI) / 180)}
            y2={50 + 40 * Math.sin((sweepAngle * Math.PI) / 180)}
            stroke="rgba(34, 211, 238, 0.8)"
            strokeWidth="1"
            style={{
              filter: 'drop-shadow(0 0 4px rgba(34, 211, 238, 0.6))',
            }}
          />

          {/* Sweep gradient */}
          <defs>
            <radialGradient id="sweepGradient">
              <stop offset="0%" stopColor="rgba(34, 211, 238, 0.3)" />
              <stop offset="100%" stopColor="rgba(34, 211, 238, 0)" />
            </radialGradient>
          </defs>
          <motion.path
            d={`M 50 50 L ${50 + 40 * Math.cos((sweepAngle * Math.PI) / 180)} ${50 + 40 * Math.sin((sweepAngle * Math.PI) / 180)} A 40 40 0 0 0 ${50 + 40 * Math.cos(((sweepAngle - 60) * Math.PI) / 180)} ${50 + 40 * Math.sin(((sweepAngle - 60) * Math.PI) / 180)} Z`}
            fill="url(#sweepGradient)"
          />

          {/* Center dot */}
          <circle
            cx="50"
            cy="50"
            r="2"
            fill="rgba(34, 211, 238, 1)"
            style={{
              filter: 'drop-shadow(0 0 3px rgba(34, 211, 238, 0.8))',
            }}
          />
        </svg>

        {/* Status overlay */}
        <div className="absolute inset-0 flex items-center justify-center">
          <div className="text-center">
            <div className="text-[8px] text-gray-500 mb-0.5">THREAT LEVEL</div>
            <div className="text-lg text-green-400" style={{ fontFamily: 'Orbitron, sans-serif' }}>0</div>
          </div>
        </div>
      </div>

      <div className="text-[9px] text-green-400 flex items-center justify-center gap-1">
        <motion.div 
          className="w-1 h-1 rounded-full"
          style={{ backgroundColor: 'rgba(34, 197, 94, 1)' }}
          animate={{ opacity: [1, 0.3, 1] }}
          transition={{ duration: 2, repeat: Infinity }}
        />
        <span style={{ fontFamily: 'Space Mono, monospace' }}>ALL CLEAR – RUNNING</span>
      </div>
    </motion.div>
  );
}
