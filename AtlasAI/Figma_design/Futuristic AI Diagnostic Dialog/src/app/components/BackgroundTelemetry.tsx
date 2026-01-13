import { motion } from 'motion/react';

export function BackgroundTelemetry() {
  const verticalStreams = Array.from({ length: 8 }, (_, i) => ({
    id: i,
    left: `${10 + i * 12}%`,
    delay: Math.random() * 5,
    duration: 15 + Math.random() * 10,
  }));

  const horizontalStreams = Array.from({ length: 5 }, (_, i) => ({
    id: i,
    top: `${15 + i * 18}%`,
    delay: Math.random() * 5,
    duration: 20 + Math.random() * 10,
  }));

  const annotations = [
    'NEURAL_SIG_0x4A2F',
    'THREAT_SCORE: 0.001',
    'DECISION_CONF: 0.987',
    'ANOMALY_DELTA: -0.002',
    'PREDICTION_ERR: 0.003%',
    'COGNITION_LOAD: 67%',
    'TRUST_INDEX: 0.98',
    'BEHAVIORAL_HASH: 0xF8A1',
  ];

  return (
    <div className="absolute inset-0 overflow-hidden pointer-events-none">
      {/* Vertical telemetry streams */}
      {verticalStreams.map((stream) => (
        <motion.div
          key={`v-${stream.id}`}
          className="absolute text-[8px] opacity-0"
          style={{
            fontFamily: 'Space Mono, monospace',
            left: stream.left,
            top: '-10%',
            color: 'rgba(34, 211, 238, 0.4)',
          }}
          animate={{
            y: ['0vh', '120vh'],
            opacity: [0, 0.3, 0.6, 0.3, 0],
          }}
          transition={{
            duration: stream.duration,
            repeat: Infinity,
            delay: stream.delay,
            ease: 'linear',
          }}
        >
          {Array.from({ length: 15 }, (_, i) => (
            <div key={i} className="whitespace-nowrap mb-1">
              {Math.random() > 0.5 
                ? `[${Math.random().toFixed(6)}]`
                : `${Math.floor(Math.random() * 16).toString(16)}${Math.floor(Math.random() * 16).toString(16)}${Math.floor(Math.random() * 16).toString(16)}${Math.floor(Math.random() * 16).toString(16)}`
              }
            </div>
          ))}
        </motion.div>
      ))}

      {/* Horizontal telemetry streams */}
      {horizontalStreams.map((stream) => (
        <motion.div
          key={`h-${stream.id}`}
          className="absolute text-[8px] opacity-0 whitespace-nowrap"
          style={{
            fontFamily: 'Space Mono, monospace',
            top: stream.top,
            left: '-20%',
            color: 'rgba(139, 92, 246, 0.3)',
          }}
          animate={{
            x: ['0vw', '120vw'],
            opacity: [0, 0.4, 0.6, 0.4, 0],
          }}
          transition={{
            duration: stream.duration,
            repeat: Infinity,
            delay: stream.delay,
            ease: 'linear',
          }}
        >
          {`PROC_${stream.id}_STATUS: NOMINAL | MEM_ADDR: 0x${Math.floor(Math.random() * 16777215).toString(16).toUpperCase()}`}
        </motion.div>
      ))}

      {/* AI annotations that fade in/out */}
      {annotations.map((annotation, i) => (
        <motion.div
          key={annotation}
          className="absolute text-[9px]"
          style={{
            fontFamily: 'Space Mono, monospace',
            left: `${20 + (i % 3) * 25}%`,
            top: `${25 + Math.floor(i / 3) * 20}%`,
            color: 'rgba(34, 211, 238, 0.5)',
          }}
          animate={{
            opacity: [0, 0.6, 0.6, 0],
          }}
          transition={{
            duration: 8,
            repeat: Infinity,
            delay: i * 2,
            repeatDelay: 6,
          }}
        >
          {annotation}
        </motion.div>
      ))}

      {/* Diagonal data streams */}
      {Array.from({ length: 4 }).map((_, i) => (
        <motion.div
          key={`diag-${i}`}
          className="absolute text-[7px] opacity-0"
          style={{
            fontFamily: 'Space Mono, monospace',
            left: `${-10 + i * 30}%`,
            top: '-10%',
            color: 'rgba(34, 197, 94, 0.3)',
            transform: 'rotate(45deg)',
          }}
          animate={{
            x: ['0vw', '50vw'],
            y: ['0vh', '50vh'],
            opacity: [0, 0.4, 0],
          }}
          transition={{
            duration: 25 + i * 5,
            repeat: Infinity,
            delay: i * 3,
            ease: 'linear',
          }}
        >
          {'·'.repeat(50)}
        </motion.div>
      ))}
    </div>
  );
}
