import { motion } from 'motion/react';
import { Activity, Database, HardDrive, Wifi } from 'lucide-react';

export function StatusModules() {
  return (
    <div className="space-y-3">
      {/* Memory Topology */}
      <StatusCard title="MEMORY TOPOLOGY">
        <div className="space-y-2">
          {/* Large isolated metric */}
          <div className="text-center mb-2">
            <div className="text-2xl text-violet-400 tracking-tighter" style={{ fontFamily: 'Orbitron, sans-serif' }}>
              22.9
            </div>
            <div className="text-[8px] text-gray-600 tracking-widest">
              GB <span className="text-gray-700">/ 64 ALLOCATED</span>
            </div>
          </div>
          
          {/* Memory topology grid instead of bar */}
          <div className="grid grid-cols-8 gap-[2px] h-16">
            {Array.from({ length: 64 }).map((_, i) => {
              const isAllocated = i < 23;
              return (
                <motion.div
                  key={i}
                  className="rounded-[1px]"
                  style={{
                    backgroundColor: isAllocated 
                      ? 'rgba(139, 92, 246, 0.6)' 
                      : 'rgba(139, 92, 246, 0.08)',
                  }}
                  animate={{
                    opacity: isAllocated ? [0.6, 1, 0.6] : 0.3,
                  }}
                  transition={{
                    duration: 3,
                    repeat: Infinity,
                    delay: i * 0.02,
                  }}
                />
              );
            })}
          </div>

          <div className="text-[8px] text-violet-400/70 tracking-widest">RESIDENCY_MAP_OPTIMIZED</div>
        </div>
      </StatusCard>

      {/* Entropy Monitor (instead of Storage) */}
      <StatusCard title="ENTROPY MONITOR">
        <div className="space-y-2">
          <div className="text-center mb-2">
            <div className="text-2xl text-cyan-400 tracking-tighter" style={{ fontFamily: 'Orbitron, sans-serif' }}>
              1.6
            </div>
            <div className="text-[8px] text-gray-600 tracking-widest">TB FREE_SPACE</div>
          </div>
          
          {/* Entropy lines instead of activity dots */}
          <div className="relative h-12 bg-[#0A0C14]/60 rounded">
            <svg className="w-full h-full" viewBox="0 0 100 50">
              {Array.from({ length: 5 }).map((_, i) => {
                const points = Array.from({ length: 30 }, (_, j) => {
                  const x = (j / 29) * 100;
                  const y = 25 + Math.sin(j * 0.5 + i) * (8 - i * 1.5);
                  return `${x},${y}`;
                }).join(' ');
                
                return (
                  <motion.polyline
                    key={i}
                    points={points}
                    fill="none"
                    stroke={`rgba(34, 211, 238, ${0.8 - i * 0.15})`}
                    strokeWidth="1"
                    animate={{
                      opacity: [0.3, 0.8, 0.3],
                    }}
                    transition={{
                      duration: 2,
                      repeat: Infinity,
                      delay: i * 0.2,
                    }}
                  />
                );
              })}
            </svg>
          </div>

          <div className="text-[8px] text-green-400/70 tracking-widest">WRITE_INTEGRITY_VERIFIED</div>
        </div>
      </StatusCard>

      {/* Network Trajectory */}
      <StatusCard title="NETWORK TRAJECTORY">
        <div className="space-y-2">
          <div className="text-center mb-2">
            <div className="text-2xl text-green-400 tracking-tighter" style={{ fontFamily: 'Orbitron, sans-serif' }}>
              12
            </div>
            <div className="text-[8px] text-gray-600 tracking-widest">ms LATENCY</div>
          </div>
          
          {/* Packet trajectory streaks */}
          <div className="relative h-12 bg-[#0A0C14]/60 rounded overflow-hidden">
            {Array.from({ length: 6 }).map((_, i) => (
              <motion.div
                key={i}
                className="absolute h-[2px] rounded-full"
                style={{
                  width: `${30 + Math.random() * 40}%`,
                  top: `${10 + i * 14}%`,
                  left: '-30%',
                  backgroundColor: 'rgba(34, 197, 94, 0.8)',
                  boxShadow: '0 0 4px rgba(34, 197, 94, 0.6)',
                }}
                animate={{
                  x: ['0%', '200%'],
                  opacity: [0, 1, 0],
                }}
                transition={{
                  duration: 2 + i * 0.3,
                  repeat: Infinity,
                  delay: i * 0.4,
                  ease: 'easeInOut',
                }}
              />
            ))}
          </div>

          <div className="text-[8px] text-green-400/70 tracking-widest">PACKET_CONFIDENCE_HIGH</div>
        </div>
      </StatusCard>

      {/* Phase Ring Metrics */}
      <div className="grid grid-cols-2 gap-2">
        <MicroCard label="CORE_TEMP" value="62" unit="°C" color="cyan" />
        <MicroCard label="FAN_DELTA" value="1420" unit="rpm" color="violet" />
        <MicroCard label="PWR_DRAW" value="185" unit="W" color="green" />
        <MicroCard label="CLK_FREQ" value="3.8" unit="GHz" color="cyan" />
      </div>
    </div>
  );
}

function StatusCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <motion.div
      className="relative bg-[#12121A]/70 backdrop-blur-md border border-cyan-400/20 rounded-lg p-3"
      whileHover={{ borderColor: 'rgba(34, 211, 238, 0.4)' }}
    >
      {/* Corner brackets */}
      <div className="absolute top-0 left-0 w-3 h-3 border-l border-t border-cyan-400/40"></div>
      <div className="absolute top-0 right-0 w-3 h-3 border-r border-t border-cyan-400/40"></div>
      <div className="absolute bottom-0 left-0 w-3 h-3 border-l border-b border-cyan-400/40"></div>
      <div className="absolute bottom-0 right-0 w-3 h-3 border-r border-b border-cyan-400/40"></div>

      <div className="text-[10px] tracking-widest text-cyan-400/80 mb-2" style={{ fontFamily: 'Orbitron, sans-serif' }}>
        {title}
      </div>
      {children}
    </motion.div>
  );
}

function MicroCard({ label, value, unit, color }: { label: string; value: string; unit: string; color: string }) {
  const colorMap = {
    cyan: 'rgba(34, 211, 238, 1)',
    violet: 'rgba(139, 92, 246, 1)',
    green: 'rgba(34, 197, 94, 1)',
  };

  const selectedColor = colorMap[color as keyof typeof colorMap];

  return (
    <div className="bg-[#0A0C14]/80 border border-cyan-400/10 rounded p-2">
      <div className="text-[7px] text-gray-600 tracking-widest mb-1">{label}</div>
      <div className="flex items-baseline gap-0.5">
        <div className="text-sm tracking-tight" style={{ fontFamily: 'Orbitron, sans-serif', color: selectedColor }}>
          {value}
        </div>
        <div className="text-[7px] text-gray-600">{unit}</div>
      </div>
    </div>
  );
}