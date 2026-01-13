import { motion } from 'motion/react';
import { useEffect, useState } from 'react';

interface ThreatPanelProps {
  scanActive: boolean;
}

interface Threat {
  id: string;
  name: string;
  risk: 'HIGH' | 'MODERATE';
  status: string;
}

export function ThreatPanel({ scanActive }: ThreatPanelProps) {
  const [threats] = useState<Threat[]>([
    { id: '1', name: 'Polymorphic.dll', risk: 'HIGH', status: 'Quarantined' },
    { id: '2', name: 'Backdoor.Win32.dll', risk: 'HIGH', status: 'Analyzing' },
    { id: '3', name: 'Trojan/Banker.exe', risk: 'MODERATE', status: 'Blocked' }
  ]);

  const [scanProgress, setScanProgress] = useState(0);

  useEffect(() => {
    if (scanActive) {
      setScanProgress(0);
      const interval = setInterval(() => {
        setScanProgress(prev => {
          if (prev >= 100) {
            clearInterval(interval);
            return 100;
          }
          return prev + 2;
        });
      }, 50);
      return () => clearInterval(interval);
    }
  }, [scanActive]);

  return (
    <div className="relative">
      {/* Header */}
      <motion.div
        className="flex items-center gap-2 mb-3 px-3 py-2 border border-orange-500/50 bg-orange-500/10"
        style={{
          clipPath: 'polygon(6px 0, 100% 0, 100% calc(100% - 6px), calc(100% - 6px) 100%, 0 100%, 0 6px)'
        }}
        animate={{
          boxShadow: [
            '0 0 10px rgba(255, 120, 0, 0.3)',
            '0 0 20px rgba(255, 120, 0, 0.5)',
            '0 0 10px rgba(255, 120, 0, 0.3)'
          ]
        }}
        transition={{
          duration: 2,
          repeat: Infinity,
          ease: "easeInOut"
        }}
      >
        <motion.div
          animate={{
            rotate: [0, 360]
          }}
          transition={{
            duration: 2,
            repeat: Infinity,
            ease: "linear"
          }}
        >
          <svg className="w-4 h-4 text-orange-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
          </svg>
        </motion.div>
        <span className="text-xs font-bold text-orange-500 uppercase tracking-wider">Malware Threat Detected</span>
      </motion.div>

      {/* Threat list */}
      <div className="space-y-2 mb-4">
        {threats.map((threat, index) => (
          <motion.div
            key={threat.id}
            className="relative px-3 py-2 border border-cyan-400/30 bg-cyan-400/5 backdrop-blur-sm overflow-hidden"
            style={{
              clipPath: 'polygon(6px 0, 100% 0, 100% calc(100% - 6px), calc(100% - 6px) 100%, 0 100%, 0 6px)'
            }}
            initial={{ opacity: 0, x: -20 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{
              delay: index * 0.1,
              duration: 0.3
            }}
          >
            {/* Scan line effect */}
            <motion.div
              className="absolute inset-0 bg-gradient-to-r from-transparent via-cyan-400/20 to-transparent"
              animate={{
                x: ['-100%', '200%']
              }}
              transition={{
                duration: 2,
                repeat: Infinity,
                delay: index * 0.3,
                ease: "linear"
              }}
            />

            <div className="relative z-10 flex items-center justify-between text-[10px]">
              <div className="flex-1">
                <div className="flex items-center gap-2">
                  <motion.div
                    className={`w-1.5 h-1.5 rounded-full ${threat.risk === 'HIGH' ? 'bg-orange-500' : 'bg-yellow-500'}`}
                    animate={{
                      opacity: [0.5, 1, 0.5],
                      scale: [1, 1.3, 1]
                    }}
                    transition={{
                      duration: 1.5,
                      repeat: Infinity,
                      ease: "easeInOut"
                    }}
                    style={{
                      boxShadow: threat.risk === 'HIGH' 
                        ? '0 0 8px rgba(255, 120, 0, 0.8)'
                        : '0 0 8px rgba(234, 179, 8, 0.8)'
                    }}
                  />
                  <span className="text-cyan-300 font-mono">{threat.name}</span>
                </div>
                <div className="mt-1 text-cyan-400/60 uppercase tracking-wide">
                  {threat.risk} - {threat.status}
                </div>
              </div>
            </div>
          </motion.div>
        ))}
      </div>

      {/* Scan progress */}
      {scanActive && (
        <motion.div
          className="px-3 py-2 border border-cyan-400/50 bg-cyan-400/10"
          style={{
            clipPath: 'polygon(6px 0, 100% 0, 100% calc(100% - 6px), calc(100% - 6px) 100%, 0 100%, 0 6px)'
          }}
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
        >
          <div className="text-[10px] text-cyan-400 uppercase tracking-wider mb-2 flex justify-between">
            <span>Deep Scan Progress</span>
            <span>{scanProgress}%</span>
          </div>
          <div className="h-1.5 bg-cyan-400/20 rounded-full overflow-hidden">
            <motion.div
              className="h-full bg-gradient-to-r from-cyan-400 via-blue-400 to-cyan-400 rounded-full"
              style={{
                width: `${scanProgress}%`,
                boxShadow: '0 0 10px rgba(0, 230, 255, 0.8)'
              }}
              animate={{
                backgroundPosition: ['0% 0%', '100% 0%']
              }}
              transition={{
                duration: 1.5,
                repeat: Infinity,
                ease: "linear"
              }}
            />
          </div>
        </motion.div>
      )}

      {/* Secure now indicator */}
      <motion.div
        className="mt-4 px-3 py-2 border border-cyan-400/30 bg-cyan-400/5 text-center"
        style={{
          clipPath: 'polygon(6px 0, 100% 0, 100% calc(100% - 6px), calc(100% - 6px) 100%, 0 100%, 0 6px)'
        }}
        animate={{
          borderColor: ['rgba(0, 230, 255, 0.3)', 'rgba(0, 230, 255, 0.6)', 'rgba(0, 230, 255, 0.3)']
        }}
        transition={{
          duration: 2,
          repeat: Infinity,
          ease: "easeInOut"
        }}
      >
        <div className="text-[10px] text-cyan-400/80 uppercase tracking-wider">
          Press "Secure Now" to neutralize threats
        </div>
      </motion.div>
    </div>
  );
}
