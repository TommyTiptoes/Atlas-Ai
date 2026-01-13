import { motion } from 'motion/react';
import { useEffect, useState } from 'react';

interface ScanItem {
  id: string;
  label: string;
  status: 'pending' | 'scanning' | 'complete';
  progress: number;
}

interface ScanningPanelProps {
  scanActive: boolean;
}

export function ScanningPanel({ scanActive }: ScanningPanelProps) {
  const [scanItems, setScanItems] = useState<ScanItem[]>([
    { id: 'cpu', label: 'CPU CORES', status: 'pending', progress: 0 },
    { id: 'ram', label: 'RAM SECTORS', status: 'pending', progress: 0 },
    { id: 'hdd', label: 'STORAGE BLOCKS', status: 'pending', progress: 0 },
    { id: 'registry', label: 'REGISTRY KEYS', status: 'pending', progress: 0 },
    { id: 'network', label: 'NETWORK PORTS', status: 'pending', progress: 0 },
    { id: 'processes', label: 'ACTIVE PROCESSES', status: 'pending', progress: 0 },
  ]);

  useEffect(() => {
    if (!scanActive) {
      // Reset all items
      setScanItems(prev => prev.map(item => ({ ...item, status: 'pending', progress: 0 })));
      return;
    }

    // Simulate sequential scanning
    let currentIndex = 0;
    const scanInterval = setInterval(() => {
      setScanItems(prev => {
        const updated = [...prev];
        
        // Mark current as scanning
        if (currentIndex < updated.length) {
          updated[currentIndex].status = 'scanning';
          updated[currentIndex].progress = Math.min(100, updated[currentIndex].progress + 10);
          
          // Move to next when complete
          if (updated[currentIndex].progress >= 100) {
            updated[currentIndex].status = 'complete';
            currentIndex++;
          }
        }
        
        return updated;
      });
    }, 200);

    return () => clearInterval(scanInterval);
  }, [scanActive]);

  return (
    <div className="w-full bg-black/40 backdrop-blur-sm border border-cyan-400/30 rounded-lg p-6">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          <motion.div
            className="w-3 h-3 rounded-full bg-orange-400"
            animate={{
              opacity: scanActive ? [0.4, 1, 0.4] : 0.4,
              scale: scanActive ? [1, 1.3, 1] : 1,
            }}
            transition={{
              duration: 1.5,
              repeat: Infinity,
              ease: "easeInOut"
            }}
          />
          <h3 className="text-sm text-cyan-400 tracking-wider uppercase">
            AI Agent {scanActive ? 'Scanning' : 'Standby'}
          </h3>
        </div>
        <div className="text-xs text-cyan-400/60 font-mono">
          {scanActive ? 'DEEP ANALYSIS IN PROGRESS' : 'AWAITING COMMAND'}
        </div>
      </div>

      {/* Scan Items Grid */}
      <div className="grid grid-cols-2 gap-4">
        {scanItems.map((item, index) => (
          <motion.div
            key={item.id}
            className="relative bg-black/30 border border-cyan-400/20 rounded p-4"
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: index * 0.1 }}
          >
            {/* Item label and status */}
            <div className="flex items-center justify-between mb-3">
              <span className="text-xs text-cyan-400/80 tracking-wider">
                {item.label}
              </span>
              {item.status === 'scanning' && (
                <motion.div
                  className="w-2 h-2 rounded-full bg-orange-400"
                  animate={{
                    opacity: [0.5, 1, 0.5],
                    scale: [1, 1.2, 1],
                  }}
                  transition={{
                    duration: 0.8,
                    repeat: Infinity,
                    ease: "easeInOut"
                  }}
                />
              )}
              {item.status === 'complete' && (
                <div className="w-2 h-2 rounded-full bg-cyan-400" />
              )}
              {item.status === 'pending' && (
                <div className="w-2 h-2 rounded-full bg-cyan-400/20" />
              )}
            </div>

            {/* Progress bar */}
            <div className="relative w-full h-1.5 bg-cyan-400/10 rounded-full overflow-hidden">
              <motion.div
                className={`absolute left-0 top-0 h-full rounded-full ${
                  item.status === 'complete' 
                    ? 'bg-cyan-400' 
                    : item.status === 'scanning'
                    ? 'bg-orange-400'
                    : 'bg-cyan-400/20'
                }`}
                initial={{ width: 0 }}
                animate={{ 
                  width: `${item.progress}%`,
                }}
                transition={{ duration: 0.3, ease: "easeOut" }}
              />
              
              {/* Scanning beam effect */}
              {item.status === 'scanning' && (
                <motion.div
                  className="absolute top-0 h-full w-8 bg-gradient-to-r from-transparent via-orange-400/60 to-transparent"
                  animate={{
                    left: ['-10%', '110%'],
                  }}
                  transition={{
                    duration: 1,
                    repeat: Infinity,
                    ease: "linear"
                  }}
                />
              )}
            </div>

            {/* Percentage */}
            <div className="mt-2 text-right">
              <span className={`text-xs font-mono ${
                item.status === 'complete' 
                  ? 'text-cyan-400' 
                  : item.status === 'scanning'
                  ? 'text-orange-400'
                  : 'text-cyan-400/30'
              }`}>
                {item.status === 'complete' ? 'CLEAR' : item.status === 'scanning' ? `${item.progress}%` : 'IDLE'}
              </span>
            </div>

            {/* Scanning animation overlay */}
            {item.status === 'scanning' && (
              <motion.div
                className="absolute inset-0 border border-orange-400/30 rounded pointer-events-none"
                animate={{
                  opacity: [0.2, 0.6, 0.2],
                }}
                transition={{
                  duration: 1.5,
                  repeat: Infinity,
                  ease: "easeInOut"
                }}
              />
            )}
          </motion.div>
        ))}
      </div>

      {/* Scanning details footer */}
      <div className="mt-6 pt-4 border-t border-cyan-400/20">
        <div className="flex items-center justify-between text-xs">
          <div className="flex items-center gap-4">
            <div className="flex items-center gap-2">
              <div className="w-2 h-2 rounded-full bg-orange-400" />
              <span className="text-cyan-400/60">ACTIVE</span>
            </div>
            <div className="flex items-center gap-2">
              <div className="w-2 h-2 rounded-full bg-cyan-400" />
              <span className="text-cyan-400/60">COMPLETE</span>
            </div>
            <div className="flex items-center gap-2">
              <div className="w-2 h-2 rounded-full bg-cyan-400/20" />
              <span className="text-cyan-400/60">PENDING</span>
            </div>
          </div>
          <motion.div
            className="text-cyan-400/60 font-mono"
            animate={{
              opacity: scanActive ? [0.5, 1, 0.5] : 0.5,
            }}
            transition={{
              duration: 2,
              repeat: Infinity,
              ease: "easeInOut"
            }}
          >
            {scanActive ? 'ANALYZING THREATS...' : 'SYSTEM READY'}
          </motion.div>
        </div>
      </div>
    </div>
  );
}
