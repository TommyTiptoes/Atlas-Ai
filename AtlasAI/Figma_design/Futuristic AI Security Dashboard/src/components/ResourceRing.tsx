import { motion } from 'motion/react';
import { useEffect, useState } from 'react';

interface ResourceRingProps {
  label: string;
  value: number;
  max: number;
  color?: 'cyan' | 'orange';
  alert?: boolean;
}

export function ResourceRing({ label, value, max, color = 'cyan', alert = false }: ResourceRingProps) {
  const [displayValue, setDisplayValue] = useState(0);
  const percentage = (value / max) * 100;
  const circumference = 2 * Math.PI * 45;
  const offset = circumference - (percentage / 100) * circumference;

  const ringColor = color === 'orange' || alert ? 'rgb(255, 120, 0)' : 'rgb(0, 230, 255)';
  const glowColor = color === 'orange' || alert ? 'rgba(255, 120, 0, 0.6)' : 'rgba(0, 230, 255, 0.6)';

  useEffect(() => {
    let start = 0;
    const duration = 1500;
    const startTime = Date.now();

    const animate = () => {
      const elapsed = Date.now() - startTime;
      const progress = Math.min(elapsed / duration, 1);
      const eased = 1 - Math.pow(1 - progress, 3); // ease out cubic
      
      setDisplayValue(Math.floor(eased * value));

      if (progress < 1) {
        requestAnimationFrame(animate);
      }
    };

    animate();
  }, [value]);

  // Simulate slight value changes
  useEffect(() => {
    const interval = setInterval(() => {
      setDisplayValue(prev => {
        const variance = Math.random() * 2 - 1;
        return Math.max(0, Math.min(max, prev + variance));
      });
    }, 2000);

    return () => clearInterval(interval);
  }, [max]);

  return (
    <div className="relative">
      {/* Label */}
      <div className="mb-2">
        <span className="text-xs text-cyan-300/70 uppercase tracking-wider">{label}</span>
      </div>

      {/* Ring container */}
      <div className="relative w-28 h-28 mx-auto">
        <svg className="w-full h-full -rotate-90" viewBox="0 0 100 100">
          {/* Background ring */}
          <circle
            cx="50"
            cy="50"
            r="45"
            fill="none"
            stroke="rgba(0, 230, 255, 0.1)"
            strokeWidth="8"
          />

          {/* Progress ring */}
          <motion.circle
            cx="50"
            cy="50"
            r="45"
            fill="none"
            stroke={ringColor}
            strokeWidth="8"
            strokeLinecap="round"
            strokeDasharray={circumference}
            initial={{ strokeDashoffset: circumference }}
            animate={{ 
              strokeDashoffset: offset,
              opacity: [0.6, 1, 0.6]
            }}
            transition={{
              strokeDashoffset: {
                duration: 1.5,
                ease: "easeOut"
              },
              opacity: {
                duration: 2,
                repeat: Infinity,
                ease: "easeInOut"
              }
            }}
            style={{
              filter: `drop-shadow(0 0 8px ${glowColor})`
            }}
          />

          {/* Animated dots on the ring */}
          <motion.circle
            cx="50"
            cy="5"
            r="3"
            fill={ringColor}
            animate={{
              opacity: [0.4, 1, 0.4],
              r: [3, 4, 3]
            }}
            transition={{
              duration: 1.5,
              repeat: Infinity,
              ease: "easeInOut"
            }}
            style={{
              filter: `drop-shadow(0 0 5px ${glowColor})`
            }}
          />
        </svg>

        {/* Center value */}
        <div className="absolute inset-0 flex flex-col items-center justify-center">
          <motion.div
            className="text-3xl font-bold"
            style={{ color: ringColor }}
            animate={{
              scale: [1, 1.05, 1]
            }}
            transition={{
              duration: 2,
              repeat: Infinity,
              ease: "easeInOut"
            }}
          >
            {Math.round(displayValue)}%
          </motion.div>
          <div className="text-[10px] text-cyan-300/50 uppercase tracking-wide mt-1">
            {displayValue < 50 ? 'Optimal' : displayValue < 80 ? 'Normal' : 'High'}
          </div>
        </div>

        {/* Glow effect */}
        <motion.div
          className="absolute inset-0 rounded-full"
          style={{
            background: `radial-gradient(circle, ${glowColor.replace('0.6', '0.2')} 0%, transparent 60%)`
          }}
          animate={{
            scale: [1, 1.1, 1],
            opacity: [0.3, 0.5, 0.3]
          }}
          transition={{
            duration: 2.5,
            repeat: Infinity,
            ease: "easeInOut"
          }}
        />
      </div>

      {/* Mini bar indicator */}
      <div className="mt-3 h-1 bg-cyan-400/10 rounded-full overflow-hidden">
        <motion.div
          className="h-full rounded-full"
          style={{ 
            backgroundColor: ringColor,
            boxShadow: `0 0 10px ${glowColor}`
          }}
          initial={{ width: 0 }}
          animate={{ width: `${percentage}%` }}
          transition={{
            duration: 1.5,
            ease: "easeOut"
          }}
        />
      </div>
    </div>
  );
}
