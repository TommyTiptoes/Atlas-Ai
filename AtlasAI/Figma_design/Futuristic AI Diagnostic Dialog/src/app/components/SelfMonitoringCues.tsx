import { motion, AnimatePresence } from 'motion/react';
import { useState, useEffect } from 'react';

export function SelfMonitoringCues() {
  const [currentMessage, setCurrentMessage] = useState(0);

  const messages = [
    'Self-check passed',
    'Confidence model stable',
    'No internal contradictions detected',
    'Neural pathways optimized',
    'Cognitive baseline verified',
    'Prediction algorithms aligned',
    'Threat model updated',
    'Decision matrices calibrated',
  ];

  useEffect(() => {
    const interval = setInterval(() => {
      setCurrentMessage((prev) => (prev + 1) % messages.length);
    }, 4000);
    return () => clearInterval(interval);
  }, [messages.length]);

  return (
    <div className="absolute top-20 right-6 w-56 pointer-events-none z-40">
      <AnimatePresence mode="wait">
        <motion.div
          key={currentMessage}
          initial={{ opacity: 0, x: 20, y: -10 }}
          animate={{ opacity: 1, x: 0, y: 0 }}
          exit={{ opacity: 0, x: -20, y: 10 }}
          transition={{ duration: 0.8 }}
          className="relative"
        >
          {/* Eye/Iris motif */}
          <div className="absolute -left-6 top-1/2 -translate-y-1/2">
            <motion.div
              className="relative w-4 h-4"
              animate={{
                scale: [1, 1.1, 1],
              }}
              transition={{
                duration: 3,
                repeat: Infinity,
              }}
            >
              <svg className="w-full h-full" viewBox="0 0 20 20">
                <ellipse
                  cx="10"
                  cy="10"
                  rx="8"
                  ry="6"
                  fill="none"
                  stroke="rgba(139, 92, 246, 0.6)"
                  strokeWidth="1"
                />
                <motion.circle
                  cx="10"
                  cy="10"
                  r="3"
                  fill="rgba(139, 92, 246, 0.8)"
                  animate={{
                    r: [3, 2.5, 3],
                  }}
                  transition={{
                    duration: 2,
                    repeat: Infinity,
                  }}
                />
                <circle
                  cx="10"
                  cy="10"
                  r="1.5"
                  fill="rgba(34, 211, 238, 1)"
                />
              </svg>
            </motion.div>
          </div>

          {/* Message bubble */}
          <div className="bg-[#12121A]/90 backdrop-blur-md border border-violet-400/30 rounded-lg px-3 py-2 relative">
            <div 
              className="text-[9px] text-violet-400/90 tracking-wide"
              style={{ fontFamily: 'Space Mono, monospace' }}
            >
              {messages[currentMessage]}
            </div>

            {/* Pulse indicator */}
            <motion.div
              className="absolute -bottom-1 left-1/2 -translate-x-1/2 w-1 h-1 rounded-full"
              style={{ backgroundColor: 'rgba(139, 92, 246, 1)' }}
              animate={{
                opacity: [1, 0.3, 1],
                scale: [1, 1.5, 1],
              }}
              transition={{
                duration: 2,
                repeat: Infinity,
              }}
            />

            {/* Glow effect */}
            <motion.div
              className="absolute inset-0 rounded-lg pointer-events-none"
              style={{
                boxShadow: '0 0 15px rgba(139, 92, 246, 0.3)',
              }}
              animate={{
                opacity: [0.5, 1, 0.5],
              }}
              transition={{
                duration: 3,
                repeat: Infinity,
              }}
            />
          </div>
        </motion.div>
      </AnimatePresence>
    </div>
  );
}
