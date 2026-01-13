import { motion } from 'motion/react';
import { ReactNode } from 'react';

interface ActionButtonProps {
  children: ReactNode;
  variant?: 'default' | 'primary';
  size?: 'sm' | 'lg';
  onClick?: () => void;
  className?: string;
}

export function ActionButton({ children, variant = 'default', size = 'sm', onClick, className = '' }: ActionButtonProps) {
  const isPrimary = variant === 'primary';
  const isLarge = size === 'lg';

  return (
    <motion.button
      className={`
        relative overflow-hidden
        ${isLarge ? 'px-12 py-4 text-base' : 'px-4 py-2 text-xs'}
        font-bold uppercase tracking-wider
        ${isPrimary ? 'text-cyan-900' : 'text-cyan-400'}
        border ${isPrimary ? 'border-cyan-400' : 'border-cyan-400/50'}
        bg-transparent
        backdrop-blur-sm
        transition-all duration-300
        ${className}
      `}
      style={{
        clipPath: 'polygon(8px 0, 100% 0, 100% calc(100% - 8px), calc(100% - 8px) 100%, 0 100%, 0 8px)',
        boxShadow: isPrimary 
          ? '0 0 20px rgba(0, 230, 255, 0.4), inset 0 0 20px rgba(0, 230, 255, 0.1)' 
          : '0 0 10px rgba(0, 230, 255, 0.2), inset 0 0 10px rgba(0, 230, 255, 0.05)'
      }}
      onClick={onClick}
      whileHover={{ 
        scale: 1.03,
        boxShadow: isPrimary
          ? '0 0 30px rgba(0, 230, 255, 0.6), inset 0 0 30px rgba(0, 230, 255, 0.2)'
          : '0 0 20px rgba(0, 230, 255, 0.4), inset 0 0 20px rgba(0, 230, 255, 0.1)'
      }}
      whileTap={{ 
        scale: 0.98,
        boxShadow: isPrimary
          ? '0 0 40px rgba(0, 230, 255, 0.8), inset 0 0 40px rgba(0, 230, 255, 0.3)'
          : '0 0 25px rgba(0, 230, 255, 0.5), inset 0 0 25px rgba(0, 230, 255, 0.15)'
      }}
      transition={{
        duration: 0.15,
        ease: "easeOut"
      }}
    >
      {/* Animated background */}
      {isPrimary && (
        <>
          <motion.div
            className="absolute inset-0 bg-gradient-to-r from-transparent via-cyan-400/30 to-transparent"
            animate={{
              x: ['-200%', '200%']
            }}
            transition={{
              duration: 3,
              repeat: Infinity,
              ease: "linear"
            }}
          />
          <motion.div
            className="absolute inset-0 bg-cyan-400"
            animate={{
              opacity: [0.3, 0.5, 0.3]
            }}
            transition={{
              duration: 2,
              repeat: Infinity,
              ease: "easeInOut"
            }}
          />
        </>
      )}

      {/* Corner accents */}
      <div className="absolute top-0 left-0 w-2 h-2 border-t-2 border-l-2 border-cyan-400" />
      <div className="absolute top-0 right-0 w-2 h-2 border-t-2 border-r-2 border-cyan-400" />
      <div className="absolute bottom-0 left-0 w-2 h-2 border-b-2 border-l-2 border-cyan-400" />
      <div className="absolute bottom-0 right-0 w-2 h-2 border-b-2 border-r-2 border-cyan-400" />

      {/* Glow effect on hover */}
      <motion.div
        className="absolute inset-0"
        style={{
          background: 'radial-gradient(circle at center, rgba(0, 230, 255, 0.2) 0%, transparent 70%)',
          opacity: 0
        }}
        whileHover={{ opacity: 1 }}
        transition={{ duration: 0.2 }}
      />

      {/* Content */}
      <span className="relative z-10">{children}</span>

      {/* Pulse effect for primary button */}
      {isPrimary && (
        <motion.div
          className="absolute inset-0 border-2 border-cyan-400"
          style={{
            clipPath: 'polygon(8px 0, 100% 0, 100% calc(100% - 8px), calc(100% - 8px) 100%, 0 100%, 0 8px)'
          }}
          animate={{
            opacity: [0, 0.5, 0],
            scale: [1, 1.05, 1.1]
          }}
          transition={{
            duration: 2,
            repeat: Infinity,
            ease: "easeOut"
          }}
        />
      )}

      {/* Scan line effect */}
      {!isPrimary && (
        <motion.div
          className="absolute inset-0 bg-gradient-to-b from-transparent via-cyan-400/20 to-transparent h-8"
          animate={{
            y: ['-100%', '200%']
          }}
          transition={{
            duration: 4,
            repeat: Infinity,
            ease: "linear"
          }}
        />
      )}
    </motion.button>
  );
}
