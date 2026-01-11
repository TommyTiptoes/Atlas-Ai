import { motion } from 'motion/react';
import { useState, useEffect } from 'react';

export function AIDecisionPipeline() {
  const [activeStep, setActiveStep] = useState(0);

  const steps = [
    'INPUT MONITORING',
    'CONTEXT ANALYSIS',
    'RISK EVALUATION',
    'DECISION GATE',
    'ACTION AUTH',
  ];

  useEffect(() => {
    const interval = setInterval(() => {
      setActiveStep((prev) => (prev + 1) % steps.length);
    }, 1500);
    return () => clearInterval(interval);
  }, [steps.length]);

  return (
    <motion.div
      className="relative bg-[#12121A]/70 backdrop-blur-md border border-cyan-400/20 rounded-lg p-3"
      initial={{ opacity: 0, y: -20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.8, delay: 0.1 }}
    >
      {/* Corner brackets */}
      <div className="absolute top-0 left-0 w-3 h-3 border-l border-t border-cyan-400/40"></div>
      <div className="absolute top-0 right-0 w-3 h-3 border-r border-t border-cyan-400/40"></div>

      <div className="text-[10px] tracking-widest text-cyan-400/80 mb-3" style={{ fontFamily: 'Orbitron, sans-serif' }}>
        AI DECISION PIPELINE
      </div>

      <div className="flex items-center justify-between gap-2">
        {steps.map((step, i) => (
          <div key={i} className="flex items-center flex-1">
            {/* Step node */}
            <div className="flex flex-col items-center">
              <motion.div
                className="relative w-6 h-6 rounded-full border-2 flex items-center justify-center"
                style={{
                  borderColor: activeStep === i ? 'rgba(34, 211, 238, 1)' : 'rgba(34, 211, 238, 0.3)',
                  backgroundColor: activeStep === i ? 'rgba(34, 211, 238, 0.2)' : 'rgba(10, 12, 20, 0.5)',
                }}
                animate={{
                  scale: activeStep === i ? [1, 1.2, 1] : 1,
                  boxShadow: activeStep === i 
                    ? ['0 0 10px rgba(34, 211, 238, 0.6)', '0 0 20px rgba(34, 211, 238, 0.8)', '0 0 10px rgba(34, 211, 238, 0.6)']
                    : '0 0 0px rgba(34, 211, 238, 0)',
                }}
                transition={{ duration: 0.8, repeat: activeStep === i ? Infinity : 0 }}
              >
                <div 
                  className="w-2 h-2 rounded-full"
                  style={{
                    backgroundColor: activeStep === i ? 'rgba(34, 211, 238, 1)' : 'rgba(34, 211, 238, 0.4)',
                  }}
                />
              </motion.div>

              <div 
                className="text-[7px] text-center mt-1 tracking-wider leading-tight max-w-[50px]"
                style={{
                  fontFamily: 'Space Mono, monospace',
                  color: activeStep === i ? 'rgba(34, 211, 238, 1)' : 'rgba(107, 114, 128, 1)',
                }}
              >
                {step}
              </div>
            </div>

            {/* Connection line */}
            {i < steps.length - 1 && (
              <div className="flex-1 h-[2px] mx-1 relative">
                <div 
                  className="absolute inset-0 rounded-full"
                  style={{ backgroundColor: 'rgba(34, 211, 238, 0.2)' }}
                />
                <motion.div
                  className="absolute inset-0 rounded-full"
                  style={{ backgroundColor: 'rgba(34, 211, 238, 1)' }}
                  initial={{ scaleX: 0, originX: 0 }}
                  animate={{
                    scaleX: activeStep > i ? 1 : activeStep === i ? [0, 1] : 0,
                  }}
                  transition={{
                    duration: activeStep === i ? 1.5 : 0.3,
                  }}
                />
                
                {/* Animated data particles */}
                {activeStep === i && (
                  <motion.div
                    className="absolute w-1 h-1 rounded-full"
                    style={{ backgroundColor: 'rgba(34, 211, 238, 1)', top: '0', left: '0' }}
                    animate={{
                      x: ['0%', '100%'],
                    }}
                    transition={{
                      duration: 1.5,
                      repeat: Infinity,
                    }}
                  />
                )}
              </div>
            )}
          </div>
        ))}
      </div>

      <motion.div 
        className="mt-3 text-[9px] text-cyan-400/70 text-center"
        style={{ fontFamily: 'Space Mono, monospace' }}
        animate={{ opacity: [0.5, 1, 0.5] }}
        transition={{ duration: 2, repeat: Infinity }}
      >
        AUTONOMOUS PROCESSING CYCLE ACTIVE
      </motion.div>
    </motion.div>
  );
}
