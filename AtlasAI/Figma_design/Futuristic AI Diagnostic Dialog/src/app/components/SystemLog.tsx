import { useEffect, useRef, useState } from 'react';
import { Check, ArrowRight, Circle } from 'lucide-react';

interface LogEntry {
  timestamp: string;
  tag: string;
  message: string;
  status: 'completed' | 'active' | 'queued';
}

const initialLogs: LogEntry[] = [
  { timestamp: '14:31:42', tag: 'AI_CORE', message: 'Neural substrate initialized', status: 'completed' },
  { timestamp: '14:31:58', tag: 'COGNITION', message: 'Decision pathways verified', status: 'completed' },
  { timestamp: '14:32:05', tag: 'MEMORY', message: 'Model residency confirmed', status: 'completed' },
  { timestamp: '14:32:07', tag: 'THREAT', message: 'Scanning attack surfaces...', status: 'active' },
  { timestamp: '14:32:12', tag: 'TRUST', message: 'Integrity verification pending', status: 'queued' },
  { timestamp: '14:32:15', tag: 'PREDICT', message: 'Anomaly detection queued', status: 'queued' },
];

export function SystemLog() {
  const [logs, setLogs] = useState<LogEntry[]>(initialLogs);
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const newLogs = [
      { timestamp: '14:32:19', tag: 'ENCRYPT', message: 'Cryptographic chain validated', status: 'completed' as const },
      { timestamp: '14:32:24', tag: 'BEHAVIOR', message: 'Baseline model stable', status: 'completed' as const },
      { timestamp: '14:32:29', tag: 'PIPELINE', message: 'Decision gate analyzing...', status: 'active' as const },
      { timestamp: '14:32:33', tag: 'SELF_CHK', message: 'Internal consistency verified', status: 'completed' as const },
      { timestamp: '14:32:38', tag: 'TENSOR', message: 'AI accelerator responsive', status: 'completed' as const },
      { timestamp: '14:32:42', tag: 'AUTONOMY', message: 'Supervised mode confirmed', status: 'completed' as const },
    ];

    let index = 0;
    const interval = setInterval(() => {
      if (index < newLogs.length) {
        setLogs(prev => [...prev, newLogs[index]]);
        index++;
      }
    }, 3000);

    return () => clearInterval(interval);
  }, []);

  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [logs]);

  const getIcon = (status: LogEntry['status']) => {
    switch (status) {
      case 'completed':
        return <Check className="w-3 h-3 text-green-400" />;
      case 'active':
        return <ArrowRight className="w-3 h-3 text-cyan-400" />;
      case 'queued':
        return <Circle className="w-3 h-3 text-gray-600" />;
    }
  };

  return (
    <div className="relative w-full h-full bg-[#0A0C14]/70 backdrop-blur-md border border-cyan-400/20 rounded-lg overflow-hidden">
      {/* Scanline overlay */}
      <div 
        className="absolute inset-0 pointer-events-none opacity-10 z-10"
        style={{
          background: 'repeating-linear-gradient(0deg, transparent, transparent 2px, rgba(34, 211, 238, 0.1) 2px, rgba(34, 211, 238, 0.1) 4px)',
        }}
      />

      {/* Vertical grid */}
      <div 
        className="absolute inset-0 pointer-events-none opacity-5"
        style={{
          background: 'repeating-linear-gradient(90deg, transparent, transparent 10px, rgba(34, 211, 238, 0.3) 10px, rgba(34, 211, 238, 0.3) 11px)',
        }}
      />

      {/* Glow edge */}
      <div className="absolute inset-0 pointer-events-none border border-cyan-400/30 rounded-lg shadow-[inset_0_0_20px_rgba(34,211,238,0.1)]"></div>

      {/* Log content */}
      <div 
        ref={scrollRef}
        className="relative h-full overflow-y-auto p-3 space-y-1 scrollbar-thin scrollbar-thumb-cyan-400/20 scrollbar-track-transparent"
        style={{ fontFamily: 'Space Mono, monospace' }}
      >
        {logs.map((log, index) => (
          <div 
            key={index} 
            className="flex items-start gap-2 text-[11px] leading-relaxed animate-in fade-in duration-300"
          >
            <div className="mt-1">{getIcon(log.status)}</div>
            <div className="flex-1">
              <span className="text-cyan-400/60">[{log.timestamp}]</span>{' '}
              <span className="text-violet-400/80">[{log.tag}]</span>{' '}
              <span className={log.status === 'completed' ? 'text-gray-300' : log.status === 'active' ? 'text-cyan-400' : 'text-gray-600'}>
                {log.message}
              </span>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}