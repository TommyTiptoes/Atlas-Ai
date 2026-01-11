import { motion } from 'motion/react';
import { useState } from 'react';

export function SecurityModeSelector() {
  const [selectedMode, setSelectedMode] = useState(1);

  const modes = [
    { label: 'PASSIVE MONITOR', color: 'rgba(34, 211, 238, 1)' },
    { label: 'ACTIVE DEFENSE', color: 'rgba(34, 197, 94, 1)' },
    { label: 'LOCKDOWN', color: 'rgba(251, 191, 36, 1)' },
    { label: 'AUTONOMOUS', color: 'rgba(107, 114, 128, 1)', disabled: true },
  ];

  return (
    <motion.div
      className="relative bg-[#12121A]/70 backdrop-blur-md border border-cyan-400/20 rounded-lg p-3"
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.8, delay: 0.6 }}
    >
      {/* Corner brackets */}
      <div className="absolute top-0 left-0 w-3 h-3 border-l border-t border-cyan-400/40"></div>
      <div className="absolute top-0 right-0 w-3 h-3 border-r border-t border-cyan-400/40"></div>

      <div className="text-[10px] tracking-widest text-cyan-400/80 mb-3" style={{ fontFamily: 'Orbitron, sans-serif' }}>
        SECURITY POSTURE
      </div>

      <div className="space-y-2">
        {modes.map((mode, i) => (
          <motion.button
            key={i}
            onClick={() => !mode.disabled && setSelectedMode(i)}
            className="w-full text-left relative"
            disabled={mode.disabled}
            whileHover={!mode.disabled ? { scale: 1.02 } : {}}
            whileTap={!mode.disabled ? { scale: 0.98 } : {}}
          >
            <div 
              className="relative px-3 py-1.5 rounded border transition-all"
              style={{
                borderColor: selectedMode === i 
                  ? mode.color 
                  : mode.disabled 
                    ? 'rgba(107, 114, 128, 0.2)' 
                    : 'rgba(34, 211, 238, 0.1)',
                backgroundColor: selectedMode === i 
                  ? `${mode.color.replace('1)', '0.1)')}` 
                  : 'rgba(10, 12, 20, 0.3)',
              }}
            >
              <div className="flex items-center gap-2">
                {/* Mode indicator */}
                <div className="relative">
                  <div 
                    className="w-2 h-2 rounded-full border"
                    style={{
                      borderColor: mode.color,
                      backgroundColor: selectedMode === i ? mode.color : 'transparent',
                    }}
                  />
                  {selectedMode === i && (
                    <motion.div
                      className="absolute inset-0 rounded-full"
                      style={{
                        backgroundColor: mode.color,
                      }}
                      animate={{
                        scale: [1, 1.5, 1],
                        opacity: [1, 0, 1],
                      }}
                      transition={{
                        duration: 2,
                        repeat: Infinity,
                      }}
                    />
                  )}
                </div>

                <span 
                  className="text-[9px] tracking-wider flex-1"
                  style={{
                    fontFamily: 'Space Mono, monospace',
                    color: mode.disabled ? 'rgba(107, 114, 128, 0.5)' : mode.color,
                  }}
                >
                  {mode.label}
                  {mode.disabled && <span className="ml-2 text-[8px]">[DISABLED]</span>}
                </span>

                {selectedMode === i && !mode.disabled && (
                  <motion.div
                    initial={{ scale: 0 }}
                    animate={{ scale: 1 }}
                    className="text-[8px]"
                    style={{ color: mode.color }}
                  >
                    ✓
                  </motion.div>
                )}
              </div>

              {/* Selection glow */}
              {selectedMode === i && !mode.disabled && (
                <motion.div
                  className="absolute inset-0 rounded"
                  style={{
                    boxShadow: `0 0 10px ${mode.color.replace('1)', '0.4)')}`,
                  }}
                  animate={{
                    opacity: [0.5, 1, 0.5],
                  }}
                  transition={{
                    duration: 2,
                    repeat: Infinity,
                  }}
                />
              )}
            </div>
          </motion.button>
        ))}
      </div>

      <motion.div 
        className="mt-3 text-[9px] text-center"
        style={{
          fontFamily: 'Space Mono, monospace',
          color: modes[selectedMode].color,
        }}
        animate={{ opacity: [0.6, 1, 0.6] }}
        transition={{ duration: 2, repeat: Infinity }}
      >
        MODE: {modes[selectedMode].label}
      </motion.div>
    </motion.div>
  );
}
