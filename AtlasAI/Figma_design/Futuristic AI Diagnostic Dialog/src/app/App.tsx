import { useState, useEffect } from 'react';
import { motion } from 'motion/react';
import { HeaderBar } from './components/HeaderBar';
import { DiagnosticScanner } from './components/DiagnosticScanner';
import { SystemLog } from './components/SystemLog';
import { StatusModules } from './components/StatusModules';
import { StatusBar } from './components/StatusBar';
import { AICoreStatus } from './components/AICoreStatus';
import { ThreatRadar } from './components/ThreatRadar';
import { TrustMatrix } from './components/TrustMatrix';
import { AIDecisionPipeline } from './components/AIDecisionPipeline';
import { AIAcceleratorCard } from './components/AIAcceleratorCard';
import { BehavioralMonitor } from './components/BehavioralMonitor';
import { SecurityModeSelector } from './components/SecurityModeSelector';
import { SelfMonitoringCues } from './components/SelfMonitoringCues';
import { BackgroundTelemetry } from './components/BackgroundTelemetry';
import { GhostedDataLayer } from './components/GhostedDataLayer';

export default function App() {
  const [progress, setProgress] = useState(47);

  useEffect(() => {
    const interval = setInterval(() => {
      setProgress((prev) => {
        if (prev >= 100) return 47; // Reset for demo
        return prev + 1;
      });
    }, 500);

    return () => clearInterval(interval);
  }, []);

  return (
    <div className="min-h-screen w-full flex items-center justify-center p-8 relative overflow-hidden bg-[#1a1a2e]">
      {/* Blurred desktop backdrop */}
      <div className="absolute inset-0 bg-gradient-to-br from-[#1a1a2e] via-[#16213e] to-[#0f3460]"></div>
      
      {/* Animated noise texture */}
      <div 
        className="absolute inset-0 opacity-[0.015] pointer-events-none"
        style={{
          backgroundImage: 'url("data:image/svg+xml,%3Csvg viewBox=\'0 0 256 256\' xmlns=\'http://www.w3.org/2000/svg\'%3E%3Cfilter id=\'noiseFilter\'%3E%3CfeTurbulence type=\'fractalNoise\' baseFrequency=\'0.9\' numOctaves=\'4\' /%3E%3C/filter%3E%3Crect width=\'100%25\' height=\'100%25\' filter=\'url(%23noiseFilter)\' /%3E%3C/svg%3E")',
        }}
      />

      {/* Vignette */}
      <div className="absolute inset-0 pointer-events-none" style={{
        background: 'radial-gradient(circle at center, transparent 0%, rgba(10, 12, 20, 0.4) 100%)',
      }} />

      {/* Main dialog window - expanded size */}
      <motion.div
        className="relative w-[900px] h-[700px] bg-[#0A0C14] rounded-2xl overflow-hidden shadow-[0_0_60px_rgba(34,211,238,0.3)]"
        initial={{ scale: 0.9, opacity: 0 }}
        animate={{ scale: 1, opacity: 1 }}
        transition={{ duration: 0.5, ease: "easeOut" }}
      >
        {/* Enhanced background telemetry */}
        <BackgroundTelemetry />

        {/* Subtle scanlines */}
        <div 
          className="absolute inset-0 pointer-events-none opacity-[0.03] z-50"
          style={{
            background: 'repeating-linear-gradient(0deg, transparent, transparent 2px, rgba(34, 211, 238, 0.2) 2px, rgba(34, 211, 238, 0.2) 4px)',
          }}
        />

        {/* Glassmorphism overlay */}
        <div className="absolute inset-0 bg-gradient-to-br from-[#0A0C14]/85 to-[#12121A]/92 backdrop-blur-sm"></div>

        {/* Ghosted secondary data layer */}
        <GhostedDataLayer />

        {/* Self-monitoring cues overlay */}
        <SelfMonitoringCues />

        {/* Content */}
        <div className="relative h-full flex flex-col">
          {/* Header */}
          <HeaderBar />

          {/* AI Decision Pipeline - horizontal bar below header */}
          <div className="px-4 pt-2">
            <AIDecisionPipeline />
          </div>

          {/* Main content area - 3 column grid */}
          <div className="flex-1 grid grid-cols-[220px_1fr_220px] gap-3 p-4 overflow-hidden">
            {/* Left panel: AI Core + Behavioral + System Log */}
            <div className="space-y-3 overflow-y-auto scrollbar-thin scrollbar-thumb-violet-400/20 scrollbar-track-transparent">
              <AICoreStatus />
              <BehavioralMonitor />
              <div className="pt-2">
                <SystemLog />
              </div>
            </div>

            {/* Center: Scanner with asymmetric offset */}
            <div className="flex items-center justify-center relative">
              <motion.div
                animate={{
                  x: [0, -3, 0, 2, 0],
                  y: [0, 2, 0, -1, 0],
                }}
                transition={{
                  duration: 8,
                  repeat: Infinity,
                  ease: "easeInOut",
                }}
              >
                <DiagnosticScanner progress={progress} />
              </motion.div>
            </div>

            {/* Right panel: Security + Threat + Status Modules */}
            <div className="space-y-3 overflow-y-auto scrollbar-thin scrollbar-thumb-cyan-400/20 scrollbar-track-transparent">
              <SecurityModeSelector />
              <ThreatRadar />
              <TrustMatrix />
              <AIAcceleratorCard />
              <div className="pt-2">
                <StatusModules />
              </div>
            </div>
          </div>

          {/* Bottom status bar */}
          <StatusBar progress={progress} />
        </div>

        {/* Neon outline glow with asymmetric pulse */}
        <motion.div 
          className="absolute inset-0 rounded-2xl border pointer-events-none"
          style={{
            borderColor: 'rgba(34, 211, 238, 0.3)',
            boxShadow: 'inset 0 0 30px rgba(34, 211, 238, 0.1)',
          }}
          animate={{
            borderColor: [
              'rgba(34, 211, 238, 0.3)',
              'rgba(139, 92, 246, 0.3)',
              'rgba(34, 211, 238, 0.3)',
            ],
          }}
          transition={{
            duration: 6,
            repeat: Infinity,
            ease: "easeInOut",
          }}
        />
      </motion.div>
    </div>
  );
}