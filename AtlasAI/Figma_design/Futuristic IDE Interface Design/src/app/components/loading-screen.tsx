import { useEffect, useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { Zap, Code2 } from 'lucide-react';

interface LoadingScreenProps {
  onComplete: () => void;
}

export function LoadingScreen({ onComplete }: LoadingScreenProps) {
  const [progress, setProgress] = useState(0);
  const [loadingText, setLoadingText] = useState('Initializing...');

  useEffect(() => {
    const messages = [
      'Initializing workspace...',
      'Loading extensions...',
      'Configuring environment...',
      'Ready to code!'
    ];

    let currentMessage = 0;
    const messageInterval = setInterval(() => {
      if (currentMessage < messages.length - 1) {
        currentMessage++;
        setLoadingText(messages[currentMessage]);
      }
    }, 600);

    const progressInterval = setInterval(() => {
      setProgress(prev => {
        if (prev >= 100) {
          clearInterval(progressInterval);
          clearInterval(messageInterval);
          setTimeout(onComplete, 500);
          return 100;
        }
        return prev + 2;
      });
    }, 30);

    return () => {
      clearInterval(progressInterval);
      clearInterval(messageInterval);
    };
  }, [onComplete]);

  return (
    <AnimatePresence>
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        className="fixed inset-0 z-[100] flex items-center justify-center bg-[#0a0a0f]"
      >
        {/* Animated background */}
        <div className="absolute inset-0 overflow-hidden">
          <motion.div
            className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] bg-[#00d4ff] rounded-full opacity-10 blur-[120px]"
            animate={{
              scale: [1, 1.2, 1],
              opacity: [0.1, 0.15, 0.1]
            }}
            transition={{
              duration: 3,
              repeat: Infinity,
              ease: "easeInOut"
            }}
          />
          <motion.div
            className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[500px] h-[500px] bg-[#b967ff] rounded-full opacity-10 blur-[120px]"
            animate={{
              scale: [1.2, 1, 1.2],
              opacity: [0.1, 0.15, 0.1]
            }}
            transition={{
              duration: 3,
              repeat: Infinity,
              ease: "easeInOut",
              delay: 1.5
            }}
          />
        </div>

        {/* Content */}
        <div className="relative z-10 flex flex-col items-center gap-8">
          {/* Logo */}
          <motion.div
            initial={{ scale: 0, rotate: -180 }}
            animate={{ scale: 1, rotate: 0 }}
            transition={{
              type: 'spring',
              stiffness: 200,
              damping: 20,
              delay: 0.2
            }}
            className="relative"
          >
            <div 
              className="w-24 h-24 bg-gradient-to-br from-[#00d4ff] to-[#b967ff] rounded-3xl flex items-center justify-center"
              style={{
                boxShadow: '0 0 60px rgba(0, 212, 255, 0.4), 0 0 100px rgba(185, 103, 255, 0.3)'
              }}
            >
              <Code2 className="w-12 h-12 text-[#0a0a0f]" />
            </div>
            <motion.div
              className="absolute inset-0 bg-gradient-to-br from-[#00d4ff] to-[#b967ff] rounded-3xl blur-xl opacity-50"
              animate={{
                scale: [1, 1.1, 1],
                opacity: [0.5, 0.7, 0.5]
              }}
              transition={{
                duration: 2,
                repeat: Infinity,
                ease: "easeInOut"
              }}
            />
          </motion.div>

          {/* Title */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.4 }}
            className="text-center"
          >
            <h1 className="text-4xl font-bold bg-gradient-to-r from-[#00d4ff] via-[#00ffff] to-[#b967ff] bg-clip-text text-transparent mb-2">
              NEXUS IDE
            </h1>
            <p className="text-[#8b8b9a] text-sm">
              Next-Generation Development Environment
            </p>
          </motion.div>

          {/* Progress Bar */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.6 }}
            className="w-80"
          >
            <div className="relative h-2 bg-[#1a1a24] rounded-full overflow-hidden border border-[rgba(0,212,255,0.2)]">
              <motion.div
                className="absolute inset-y-0 left-0 bg-gradient-to-r from-[#00d4ff] to-[#b967ff] rounded-full"
                style={{ width: `${progress}%` }}
                initial={{ width: 0 }}
              >
                <div 
                  className="absolute inset-0 bg-white opacity-20"
                  style={{
                    background: 'linear-gradient(90deg, transparent, rgba(255,255,255,0.3), transparent)',
                    animation: 'shimmer 1.5s infinite'
                  }}
                />
              </motion.div>
              <div
                className="absolute inset-0 rounded-full"
                style={{
                  boxShadow: 'inset 0 0 10px rgba(0, 212, 255, 0.3)'
                }}
              />
            </div>
            
            <motion.p
              className="text-center text-xs text-[#8b8b9a] mt-3"
              animate={{ opacity: [0.6, 1, 0.6] }}
              transition={{ duration: 1.5, repeat: Infinity }}
            >
              {loadingText}
            </motion.p>
          </motion.div>

          {/* Powered by AI badge */}
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ delay: 0.8 }}
            className="flex items-center gap-2 px-4 py-2 bg-[rgba(0,255,159,0.1)] border border-[rgba(0,255,159,0.2)] rounded-full"
          >
            <motion.div
              animate={{
                rotate: [0, 360]
              }}
              transition={{
                duration: 3,
                repeat: Infinity,
                ease: "linear"
              }}
            >
              <Zap className="w-4 h-4 text-[#00ff9f]" />
            </motion.div>
            <span className="text-xs text-[#00ff9f]">Powered by AI</span>
          </motion.div>
        </div>

        <style>{`
          @keyframes shimmer {
            0% { transform: translateX(-100%); }
            100% { transform: translateX(100%); }
          }
        `}</style>
      </motion.div>
    </AnimatePresence>
  );
}
