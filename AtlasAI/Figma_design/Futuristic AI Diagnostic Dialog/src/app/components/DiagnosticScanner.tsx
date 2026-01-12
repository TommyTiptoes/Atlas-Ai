import { motion } from 'motion/react';
import { Cpu, HardDrive, Wifi, MemoryStick } from 'lucide-react';
import { useEffect, useState } from 'react';

interface DiagnosticScannerProps {
  progress: number;
}

const icons = [Cpu, HardDrive, Wifi, MemoryStick];

export function DiagnosticScanner({ progress }: DiagnosticScannerProps) {
  const [iconIndex, setIconIndex] = useState(0);
  
  useEffect(() => {
    const interval = setInterval(() => {
      setIconIndex((prev) => (prev + 1) % icons.length);
    }, 2000);
    return () => clearInterval(interval);
  }, []);

  const Icon = icons[iconIndex];

  return (
    <div className="relative flex items-center justify-center w-[360px] h-[360px]">
      {/* Corner brackets */}
      <div className="absolute inset-0 pointer-events-none">
        {/* Top-left */}
        <div className="absolute top-0 left-0 w-8 h-8 border-l-2 border-t-2 border-cyan-400/60"></div>
        {/* Top-right */}
        <div className="absolute top-0 right-0 w-8 h-8 border-r-2 border-t-2 border-cyan-400/60"></div>
        {/* Bottom-left */}
        <div className="absolute bottom-0 left-0 w-8 h-8 border-l-2 border-b-2 border-cyan-400/60"></div>
        {/* Bottom-right */}
        <div className="absolute bottom-0 right-0 w-8 h-8 border-r-2 border-b-2 border-cyan-400/60"></div>
      </div>

      {/* Outer rotating ring */}
      <motion.div
        className="absolute w-[340px] h-[340px] rounded-full"
        animate={{ rotate: 360 }}
        transition={{ duration: 8, repeat: Infinity, ease: "linear" }}
        style={{
          background: `conic-gradient(from 0deg, transparent 0deg, transparent 270deg, rgba(34, 211, 238, 0.6) 315deg, transparent 360deg)`,
          filter: 'blur(2px)',
        }}
      />

      {/* Outer ring border */}
      <div className="absolute w-[340px] h-[340px] rounded-full border border-cyan-400/20"></div>

      {/* Secondary segmented ring */}
      <motion.div
        className="absolute w-[300px] h-[300px] rounded-full"
        animate={{ rotate: -360 }}
        transition={{ duration: 12, repeat: Infinity, ease: "linear" }}
      >
        {Array.from({ length: 12 }).map((_, i) => (
          <div
            key={i}
            className="absolute w-1 h-4 bg-violet-400/50 rounded-full"
            style={{
              top: '0%',
              left: '50%',
              transformOrigin: '0px 150px',
              transform: `rotate(${i * 30}deg) translateX(-50%)`,
            }}
          />
        ))}
      </motion.div>

      {/* Concentric rings */}
      {[280, 260, 240, 220].map((size, i) => (
        <motion.div
          key={size}
          className="absolute rounded-full border border-cyan-400/10"
          style={{ width: size, height: size }}
          animate={{ opacity: [0.1, 0.3, 0.1] }}
          transition={{
            duration: 2,
            repeat: Infinity,
            delay: i * 0.2,
            ease: "easeInOut"
          }}
        />
      ))}

      {/* Hexagonal grid overlay */}
      <div 
        className="absolute w-[200px] h-[200px] rounded-full opacity-20"
        style={{
          background: `repeating-conic-gradient(from 30deg, transparent 0deg, transparent 60deg, rgba(34, 211, 238, 0.1) 60deg, rgba(34, 211, 238, 0.1) 61deg)`,
        }}
      />

      {/* Data particles */}
      {Array.from({ length: 8 }).map((_, i) => (
        <motion.div
          key={i}
          className="absolute w-1.5 h-1.5 rounded-full"
          style={{
            top: '50%',
            left: '50%',
            backgroundColor: 'rgba(34, 211, 238, 1)',
          }}
          animate={{
            x: [0, Math.cos((i * Math.PI * 2) / 8) * 120],
            y: [0, Math.sin((i * Math.PI * 2) / 8) * 120],
            opacity: [0, 1, 0],
            scale: [0, 1, 0],
          }}
          transition={{
            duration: 3,
            repeat: Infinity,
            delay: i * 0.3,
            ease: "easeOut"
          }}
        />
      ))}

      {/* Inner core circle */}
      <div className="absolute w-[180px] h-[180px] rounded-full bg-[#0A0C14]/90 border border-cyan-400/30 backdrop-blur-sm flex items-center justify-center flex-col shadow-[0_0_30px_rgba(34,211,238,0.3)]">
        {/* Core icon */}
        <motion.div
          key={iconIndex}
          initial={{ scale: 0.8, opacity: 0 }}
          animate={{ scale: 1, opacity: 1 }}
          exit={{ scale: 0.8, opacity: 0 }}
          transition={{ duration: 0.3 }}
          className="mb-3"
        >
          <Icon className="w-10 h-10 text-cyan-400" strokeWidth={1.5} />
        </motion.div>

        {/* Progress percentage */}
        <div className="text-5xl font-bold text-cyan-400 tracking-wider" style={{ fontFamily: 'Orbitron, sans-serif' }}>
          {progress}%
        </div>

        {/* Status text */}
        <div className="mt-2 text-xs tracking-[0.2em] text-cyan-400/80" style={{ fontFamily: 'Orbitron, sans-serif' }}>
          ANALYZING
        </div>

        {/* Micro text */}
        <div className="mt-1 text-[9px] tracking-wider text-cyan-400/50" style={{ fontFamily: 'Space Mono, monospace' }}>
          COGNITIVE_SUBSTRATE_SCAN
        </div>
      </div>

      {/* Pulsing glow effect */}
      <motion.div
        className="absolute w-[180px] h-[180px] rounded-full"
        style={{ backgroundColor: 'rgba(34, 211, 238, 0.05)' }}
        animate={{ scale: [1, 1.1, 1], opacity: [0.5, 0.2, 0.5] }}
        transition={{ duration: 2, repeat: Infinity, ease: "easeInOut" }}
      />
    </div>
  );
}