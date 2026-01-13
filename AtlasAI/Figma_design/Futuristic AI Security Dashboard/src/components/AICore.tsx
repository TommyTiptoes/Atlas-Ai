import { motion } from 'motion/react';
import { useEffect, useState } from 'react';
import aiHeadImage from 'figma:asset/aa50518bcf61c4208bde31ddf53e870646819d6a.png';

type AIVariant = 'idle' | 'thinking' | 'scanning';

interface AICoreProps {
  variant: AIVariant;
}

interface NeuralNode {
  id: number;
  x: number;
  y: number;
  size: number;
  delay: number;
  brightness: number;
}

export function AICore({ variant }: AICoreProps) {
  const [nodes, setNodes] = useState<NeuralNode[]>([]);

  useEffect(() => {
    // Generate many neural nodes - densely packed in brain, scattered in neck/shoulders
    const neuralNodes: NeuralNode[] = [];
    let id = 0;

    // Brain center - VERY BRIGHT core
    neuralNodes.push({ id: id++, x: 195, y: 110, size: 16, delay: 0, brightness: 1 });
    
    // Dense brain region nodes (head area)
    const brainCenterX = 195;
    const brainCenterY = 110;
    const brainRadius = 45;
    
    for (let i = 0; i < 40; i++) {
      const angle = (Math.PI * 2 * i) / 40;
      const distance = 15 + Math.random() * brainRadius;
      const x = brainCenterX + Math.cos(angle) * distance;
      const y = brainCenterY + Math.sin(angle) * distance;
      neuralNodes.push({
        id: id++,
        x,
        y,
        size: 3 + Math.random() * 4,
        delay: Math.random() * 2,
        brightness: 0.6 + Math.random() * 0.4
      });
    }

    // Additional scattered head nodes
    for (let i = 0; i < 25; i++) {
      neuralNodes.push({
        id: id++,
        x: 140 + Math.random() * 110,
        y: 70 + Math.random() * 100,
        size: 2 + Math.random() * 3,
        delay: Math.random() * 2,
        brightness: 0.5 + Math.random() * 0.3
      });
    }

    // Neck region nodes
    for (let i = 0; i < 15; i++) {
      neuralNodes.push({
        id: id++,
        x: 160 + Math.random() * 60,
        y: 180 + Math.random() * 50,
        size: 2 + Math.random() * 3,
        delay: Math.random() * 2,
        brightness: 0.4 + Math.random() * 0.3
      });
    }

    // Shoulder region nodes
    for (let i = 0; i < 20; i++) {
      neuralNodes.push({
        id: id++,
        x: 130 + Math.random() * 90,
        y: 230 + Math.random() * 60,
        size: 2 + Math.random() * 3,
        delay: Math.random() * 2,
        brightness: 0.3 + Math.random() * 0.3
      });
    }

    setNodes(neuralNodes);
  }, []);

  const glowIntensity = variant === 'scanning' ? 1 : variant === 'thinking' ? 0.7 : 0.5;
  const pulseSpeed = variant === 'scanning' ? 1.5 : variant === 'thinking' ? 2.5 : 4;

  return (
    <div className="relative w-full h-80 flex items-center justify-center">
      {/* Title */}
      <div className="absolute top-0 left-0 right-0 text-center">
        <h3 className="text-sm text-cyan-400/80 tracking-wider uppercase">Neural Core</h3>
      </div>

      {/* Main container */}
      <div className="relative w-full h-full">
        {/* AI Head Image - subtle, darker */}
        <motion.div
          className="absolute inset-0 flex items-center justify-center"
          animate={{
            opacity: [0.3, 0.4, 0.3],
          }}
          transition={{
            duration: pulseSpeed,
            repeat: Infinity,
            ease: "easeInOut"
          }}
        >
          <img 
            src={aiHeadImage} 
            alt="AI Head"
            className="w-full h-full object-contain"
            style={{
              filter: `brightness(0.6) drop-shadow(0 0 ${glowIntensity * 10}px rgba(255, 140, 0, ${glowIntensity * 0.3}))`,
            }}
          />
        </motion.div>

        {/* Bright orange glow from brain center */}
        <motion.div
          className="absolute inset-0 flex items-center justify-center pointer-events-none"
          animate={{
            opacity: [0.4, glowIntensity * 0.8, 0.4],
          }}
          transition={{
            duration: pulseSpeed,
            repeat: Infinity,
            ease: "easeInOut"
          }}
        >
          <div 
            className="w-full h-full"
            style={{
              background: `radial-gradient(circle at 50% 35%, rgba(255, 140, 0, ${glowIntensity * 0.4}) 0%, rgba(255, 100, 0, ${glowIntensity * 0.2}) 20%, transparent 50%)`,
            }}
          />
        </motion.div>

        {/* SVG overlay for neural nodes */}
        <svg 
          className="absolute inset-0 w-full h-full pointer-events-none"
          viewBox="0 0 400 320"
          preserveAspectRatio="xMidYMid meet"
        >
          <defs>
            <filter id={`orange-glow-${variant}`}>
              <feGaussianBlur stdDeviation={glowIntensity * 4} result="coloredBlur"/>
              <feMerge>
                <feMergeNode in="coloredBlur"/>
                <feMergeNode in="SourceGraphic"/>
              </feMerge>
            </filter>
          </defs>

          {/* Neural nodes (orange lights) */}
          {nodes.map((node) => {
            const orangeValue = Math.floor(140 + node.brightness * 115); // 140-255
            return (
              <g key={`node-${node.id}`}>
                {/* Outer glow */}
                <motion.circle
                  cx={node.x}
                  cy={node.y}
                  r={node.size * 1.5}
                  fill={`rgba(255, ${orangeValue}, 0, 0.2)`}
                  animate={{
                    r: [node.size * 1.5, node.size * 2.2, node.size * 1.5],
                    opacity: [0.2, 0.4 * node.brightness, 0.2]
                  }}
                  transition={{
                    duration: pulseSpeed * 0.8,
                    repeat: Infinity,
                    delay: node.delay,
                    ease: "easeInOut"
                  }}
                />

                {/* Core light */}
                <motion.circle
                  cx={node.x}
                  cy={node.y}
                  r={node.size * 0.5}
                  fill={`rgba(255, ${orangeValue}, 50, ${node.brightness})`}
                  filter={`url(#orange-glow-${variant})`}
                  animate={{
                    r: [node.size * 0.4, node.size * 0.7, node.size * 0.4],
                    opacity: [0.7 * node.brightness, node.brightness, 0.7 * node.brightness]
                  }}
                  transition={{
                    duration: pulseSpeed * 0.6,
                    repeat: Infinity,
                    delay: node.delay,
                    ease: "easeInOut"
                  }}
                />
              </g>
            );
          })}

          {/* Scanning waves - orange */}
          {variant === 'scanning' && (
            <>
              {[0, 1, 2].map((i) => (
                <motion.circle
                  key={`wave-${i}`}
                  cx="195"
                  cy="110"
                  r="0"
                  fill="none"
                  stroke="rgba(255, 140, 0, 0.6)"
                  strokeWidth="2"
                  animate={{
                    r: [0, 150],
                    opacity: [0.8, 0]
                  }}
                  transition={{
                    duration: 2,
                    repeat: Infinity,
                    delay: i * 0.7,
                    ease: "easeOut"
                  }}
                />
              ))}
            </>
          )}
        </svg>

        {/* Floating orange particles around the head */}
        <div className="absolute inset-0 pointer-events-none">
          {Array.from({ length: 25 }).map((_, i) => {
            const size = 1 + Math.random() * 2;
            return (
              <motion.div
                key={`particle-${i}`}
                className="absolute rounded-full"
                style={{
                  width: `${size}px`,
                  height: `${size}px`,
                  left: `${25 + Math.random() * 50}%`,
                  top: `${15 + Math.random() * 70}%`,
                  backgroundColor: `rgba(255, ${140 + Math.floor(Math.random() * 60)}, 0, 0.8)`,
                  filter: `drop-shadow(0 0 ${3 + Math.random() * 3}px rgba(255, 140, 0, 0.8))`,
                  boxShadow: `0 0 ${4 + Math.random() * 4}px rgba(255, 140, 0, 0.6)`
                }}
                animate={{
                  y: [0, -15 - Math.random() * 10, 0],
                  x: [0, (Math.random() - 0.5) * 15, 0],
                  opacity: [0.3, 0.9, 0.3],
                  scale: [1, 1.4, 1]
                }}
                transition={{
                  duration: 3 + Math.random() * 3,
                  repeat: Infinity,
                  delay: i * 0.15,
                  ease: "easeInOut"
                }}
              />
            );
          })}
        </div>
      </div>

      {/* Status text */}
      <div className="absolute bottom-0 left-0 right-0 text-center">
        <motion.p
          className="text-xs text-cyan-400/80 uppercase tracking-wider"
          animate={{
            opacity: [0.5, 1, 0.5]
          }}
          transition={{
            duration: 2,
            repeat: Infinity,
            ease: "easeInOut"
          }}
        >
          {variant === 'scanning' ? 'Deep Scan Active' : variant === 'thinking' ? 'Analyzing Patterns' : 'Neural Net Standby'}
        </motion.p>
      </div>
    </div>
  );
}