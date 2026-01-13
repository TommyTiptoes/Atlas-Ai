import { motion } from 'motion/react';
import { useEffect, useState } from 'react';

export function Waveform() {
  const [points, setPoints] = useState<number[]>([]);
  const numPoints = 60;

  useEffect(() => {
    // Initialize with random values
    setPoints(Array.from({ length: numPoints }, () => Math.random() * 40 + 10));

    // Continuously update points to simulate live data
    const interval = setInterval(() => {
      setPoints(prev => {
        const newPoints = [...prev];
        newPoints.shift();
        newPoints.push(Math.random() * 40 + 10);
        return newPoints;
      });
    }, 100);

    return () => clearInterval(interval);
  }, []);

  // Generate SVG path from points
  const generatePath = (pts: number[], baseY: number) => {
    if (pts.length === 0) return '';
    
    const width = 400;
    const spacing = width / (pts.length - 1);
    
    let path = `M 0,${baseY - pts[0]}`;
    
    for (let i = 1; i < pts.length; i++) {
      const x = i * spacing;
      const y = baseY - pts[i];
      const prevX = (i - 1) * spacing;
      const prevY = baseY - pts[i - 1];
      
      const cpX = (prevX + x) / 2;
      path += ` Q ${cpX},${prevY} ${x},${y}`;
    }
    
    return path;
  };

  return (
    <div className="w-full max-w-2xl h-24 relative">
      {/* Grid background */}
      <div className="absolute inset-0 opacity-20">
        <svg className="w-full h-full">
          {/* Horizontal lines */}
          {[0, 25, 50, 75, 100].map((y) => (
            <line
              key={`h-${y}`}
              x1="0"
              y1={`${y}%`}
              x2="100%"
              y2={`${y}%`}
              stroke="rgba(0, 230, 255, 0.3)"
              strokeWidth="0.5"
            />
          ))}
          {/* Vertical lines */}
          {Array.from({ length: 20 }, (_, i) => (
            <line
              key={`v-${i}`}
              x1={`${(i / 19) * 100}%`}
              y1="0"
              x2={`${(i / 19) * 100}%`}
              y2="100%"
              stroke="rgba(0, 230, 255, 0.2)"
              strokeWidth="0.5"
            />
          ))}
        </svg>
      </div>

      {/* Waveforms */}
      <svg className="w-full h-full relative z-10" preserveAspectRatio="none" viewBox="0 0 400 100">
        {/* Main waveform */}
        <motion.path
          d={generatePath(points, 50)}
          fill="none"
          stroke="rgb(0, 230, 255)"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
          style={{
            filter: 'drop-shadow(0 0 8px rgba(0, 230, 255, 0.8))'
          }}
          animate={{
            opacity: [0.6, 1, 0.6]
          }}
          transition={{
            duration: 2,
            repeat: Infinity,
            ease: "easeInOut"
          }}
        />

        {/* Secondary waveform (offset) */}
        <motion.path
          d={generatePath(points.map(p => p * 0.6), 50)}
          fill="none"
          stroke="rgb(0, 230, 255)"
          strokeWidth="1"
          opacity="0.4"
          strokeLinecap="round"
          strokeLinejoin="round"
        />

        {/* Glow path */}
        <motion.path
          d={generatePath(points, 50)}
          fill="none"
          stroke="rgba(0, 230, 255, 0.3)"
          strokeWidth="6"
          strokeLinecap="round"
          strokeLinejoin="round"
          style={{
            filter: 'blur(4px)'
          }}
        />

        {/* Moving indicator dots */}
        {[0, 1, 2].map((i) => (
          <motion.circle
            key={i}
            cx="0"
            cy="50"
            r="3"
            fill="rgb(0, 230, 255)"
            animate={{
              cx: [0, 400],
              opacity: [0, 1, 1, 0]
            }}
            transition={{
              duration: 4,
              repeat: Infinity,
              delay: i * 1.3,
              ease: "linear"
            }}
            style={{
              filter: 'drop-shadow(0 0 5px rgba(0, 230, 255, 0.9))'
            }}
          />
        ))}
      </svg>

      {/* Scan line effect */}
      <motion.div
        className="absolute top-0 bottom-0 w-0.5 bg-gradient-to-b from-transparent via-cyan-400 to-transparent"
        style={{
          filter: 'blur(1px)',
          boxShadow: '0 0 15px rgba(0, 230, 255, 0.8)'
        }}
        animate={{
          left: ['-10%', '110%']
        }}
        transition={{
          duration: 3,
          repeat: Infinity,
          ease: "linear"
        }}
      />

      {/* Labels */}
      <div className="absolute -top-5 left-0 text-[10px] text-cyan-400/60 uppercase tracking-wider">
        Network Traffic
      </div>
      <div className="absolute -bottom-5 right-0 text-[10px] text-cyan-400/60 uppercase tracking-wider">
        Real-time Monitor
      </div>
    </div>
  );
}
