import { motion } from 'motion/react';

export function TrustMatrix() {
  const metrics = [
    { label: 'KERNEL TRUST', value: 98, color: 'rgba(34, 197, 94, 1)' },
    { label: 'MEMORY INTEGRITY', value: 100, color: 'rgba(34, 197, 94, 1)' },
    { label: 'NETWORK CONFIDENCE', value: 96, color: 'rgba(34, 197, 94, 1)' },
    { label: 'AI SELF-VERIFICATION', value: 99, color: 'rgba(34, 197, 94, 1)' },
  ];

  return (
    <motion.div
      className="relative bg-[#12121A]/70 backdrop-blur-md border border-green-400/20 rounded-lg p-3"
      initial={{ opacity: 0, x: 20 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{ duration: 0.8, delay: 0.3 }}
    >
      {/* Corner brackets */}
      <div className="absolute top-0 left-0 w-3 h-3 border-l border-t border-green-400/40"></div>
      <div className="absolute top-0 right-0 w-3 h-3 border-r border-t border-green-400/40"></div>
      <div className="absolute bottom-0 left-0 w-3 h-3 border-l border-b border-green-400/40"></div>
      <div className="absolute bottom-0 right-0 w-3 h-3 border-r border-b border-green-400/40"></div>

      <div className="text-[10px] tracking-widest text-green-400/80 mb-3" style={{ fontFamily: 'Orbitron, sans-serif' }}>
        TRUST MATRIX
      </div>

      <div className="space-y-2.5">
        {metrics.map((metric, i) => (
          <div key={i}>
            <div className="flex justify-between items-center mb-1">
              <span className="text-[8px] text-gray-500 tracking-wide">{metric.label}</span>
              <span className="text-[10px] text-green-400" style={{ fontFamily: 'Space Mono, monospace' }}>
                {metric.value}%
              </span>
            </div>
            
            {/* Circular phase ring */}
            <div className="relative h-1.5">
              <div className="absolute inset-0 flex gap-[1px]">
                {Array.from({ length: 40 }).map((_, j) => {
                  const isActive = j < (metric.value / 100) * 40;
                  return (
                    <motion.div
                      key={j}
                      className="flex-1 rounded-full"
                      style={{
                        backgroundColor: isActive ? metric.color : 'rgba(34, 197, 94, 0.1)',
                      }}
                      initial={{ opacity: 0 }}
                      animate={{
                        opacity: isActive ? [0.6, 1, 0.6] : 0.3,
                      }}
                      transition={{
                        duration: 2,
                        repeat: Infinity,
                        delay: i * 0.1 + j * 0.02,
                      }}
                    />
                  );
                })}
              </div>
            </div>
          </div>
        ))}
      </div>

      <motion.div 
        className="mt-3 text-[9px] text-green-400/70 text-center"
        style={{ fontFamily: 'Space Mono, monospace' }}
        animate={{ opacity: [0.7, 1, 0.7] }}
        transition={{ duration: 3, repeat: Infinity }}
      >
        CRYPTOGRAPHIC VERIFICATION ACTIVE
      </motion.div>
    </motion.div>
  );
}
