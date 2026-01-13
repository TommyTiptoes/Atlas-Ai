import { useState, useEffect } from 'react';
import { AICore } from './components/AICore';
import { SystemCore } from './components/SystemCore';
import { ResourceRing } from './components/ResourceRing';
import { Waveform } from './components/Waveform';
import { ActionButton } from './components/ActionButton';
import { StatusBar } from './components/StatusBar';
import { ThreatPanel } from './components/ThreatPanel';
import { SystemStats } from './components/SystemStats';
import { ScanningPanel } from './components/ScanningPanel';
import backgroundImage from 'figma:asset/f390f128d879933460e63743c1f3c7ede010d1c0.png';

type AIState = 'idle' | 'thinking' | 'scanning';
type SystemState = 'idle' | 'activeScan' | 'alert';

export default function App() {
  const [aiState, setAiState] = useState<AIState>('idle');
  const [systemState, setSystemState] = useState<SystemState>('idle');
  const [scanActive, setScanActive] = useState(false);

  // Cycle AI between idle and thinking
  useEffect(() => {
    const interval = setInterval(() => {
      if (aiState !== 'scanning') {
        setAiState(prev => prev === 'idle' ? 'thinking' : 'idle');
      }
    }, 3500);
    return () => clearInterval(interval);
  }, [aiState]);

  const handleSecureNow = () => {
    setAiState('scanning');
    setSystemState('activeScan');
    setScanActive(true);
    
    setTimeout(() => {
      setAiState('idle');
      setSystemState('idle');
      setScanActive(false);
    }, 5000);
  };

  return (
    <div className="relative w-full h-screen overflow-hidden bg-[#020815]">
      {/* Background */}
      <div 
        className="absolute inset-0 opacity-40"
        style={{
          backgroundImage: `url(${backgroundImage})`,
          backgroundSize: 'cover',
          backgroundPosition: 'center'
        }}
      />
      
      {/* Animated particles */}
      <div className="absolute inset-0">
        {[...Array(30)].map((_, i) => (
          <div
            key={i}
            className="absolute w-1 h-1 bg-cyan-400 rounded-full animate-float"
            style={{
              left: `${Math.random() * 100}%`,
              top: `${Math.random() * 100}%`,
              animationDelay: `${Math.random() * 5}s`,
              animationDuration: `${5 + Math.random() * 10}s`,
              opacity: 0.3 + Math.random() * 0.4
            }}
          />
        ))}
      </div>

      {/* Grid overlay */}
      <div className="absolute inset-0 opacity-10" style={{
        backgroundImage: `
          linear-gradient(0deg, transparent 24%, rgba(0, 230, 255, 0.05) 25%, rgba(0, 230, 255, 0.05) 26%, transparent 27%, transparent 74%, rgba(0, 230, 255, 0.05) 75%, rgba(0, 230, 255, 0.05) 76%, transparent 77%, transparent),
          linear-gradient(90deg, transparent 24%, rgba(0, 230, 255, 0.05) 25%, rgba(0, 230, 255, 0.05) 26%, transparent 27%, transparent 74%, rgba(0, 230, 255, 0.05) 75%, rgba(0, 230, 255, 0.05) 76%, transparent 77%, transparent)
        `,
        backgroundSize: '50px 50px'
      }} />

      {/* Main content */}
      <div className="relative z-10 w-full h-full flex flex-col p-8">
        {/* Header */}
        <header className="flex items-center justify-between mb-8">
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 rounded-full border-2 border-cyan-400 flex items-center justify-center animate-pulse-glow">
              <div className="w-8 h-8 rounded-full bg-cyan-400/30" />
            </div>
            <div>
              <h1 className="text-2xl font-bold text-cyan-400 tracking-wider">
                AI SECURITY SYSTEM ACTIVE
              </h1>
              <p className="text-xs text-cyan-300/60 tracking-wide">Neural Defense Grid Online</p>
            </div>
          </div>
          <div className="text-right">
            <div className="text-sm text-cyan-400/80 font-mono">
              {new Date().toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}
            </div>
            <div className="text-xs text-cyan-300/60 font-mono">
              {new Date().toLocaleTimeString('en-US', { hour12: false })}
            </div>
          </div>
        </header>

        {/* Main grid */}
        <div className="flex-1 grid grid-cols-12 gap-6">
          {/* Left panel */}
          <div className="col-span-3 flex flex-col gap-6">
            {/* Resource rings */}
            <div className="flex flex-col gap-4">
              <ResourceRing label="CPU USAGE" value={23} max={100} color="cyan" />
              <ResourceRing label="RAM USAGE" value={58} max={100} color="cyan" />
              <ResourceRing label="STORAGE" value={72} max={100} color="orange" alert />
            </div>

            {/* AI Scanning Panel */}
            <ScanningPanel scanActive={scanActive} />
          </div>

          {/* Center panel */}
          <div className="col-span-6 flex flex-col items-center justify-center gap-8">
            {/* Waveform top */}
            <Waveform />

            {/* System Core */}
            <SystemCore variant={systemState} />

            {/* Secure Now Button */}
            <ActionButton 
              variant="primary" 
              size="lg"
              onClick={handleSecureNow}
              className="mt-4"
            >
              SECURE NOW
            </ActionButton>

            {/* System status indicators */}
            <div className="flex gap-8 items-center">
              <div className="flex items-center gap-2">
                <div className="w-3 h-3 rounded-full bg-cyan-400 animate-pulse-glow" />
                <span className="text-xs text-cyan-400 uppercase tracking-wider">Active</span>
              </div>
              <div className="flex items-center gap-2">
                <div className="w-3 h-3 rounded-full bg-cyan-400 animate-pulse-glow" />
                <span className="text-xs text-cyan-400 uppercase tracking-wider">Protected</span>
              </div>
              <div className="flex items-center gap-2">
                <div className="w-3 h-3 rounded-full bg-cyan-400 animate-pulse-glow" />
                <span className="text-xs text-cyan-400 uppercase tracking-wider">Scanning</span>
              </div>
            </div>
          </div>

          {/* Right panel */}
          <div className="col-span-3 flex flex-col gap-6">
            {/* AI Core */}
            <AICore variant={aiState} />

            {/* System Stats */}
            <SystemStats />
          </div>
        </div>

        {/* Bottom navigation */}
        <StatusBar variant={scanActive ? 'scanning' : 'stable'} />
      </div>
    </div>
  );
}