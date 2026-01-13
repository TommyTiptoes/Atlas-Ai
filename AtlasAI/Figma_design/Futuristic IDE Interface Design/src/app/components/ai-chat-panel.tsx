import { useState, useRef, useEffect } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { 
  Plus, 
  ChevronDown, 
  Settings, 
  MoreHorizontal, 
  Maximize2, 
  X, 
  Sparkles,
  Send,
  Paperclip,
  User,
  Bot
} from 'lucide-react';

interface Message {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
}

interface AIChatPanelProps {
  isOpen: boolean;
  onClose: () => void;
}

export function AIChatPanel({ isOpen, onClose }: AIChatPanelProps) {
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState('');
  const [context, setContext] = useState('');
  const [isThinking, setIsThinking] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  const handleSend = async () => {
    if (!input.trim()) return;

    const userMessage: Message = {
      id: Date.now().toString(),
      role: 'user',
      content: input,
      timestamp: new Date()
    };

    setMessages(prev => [...prev, userMessage]);
    setInput('');
    setIsThinking(true);

    // Simulate AI response
    setTimeout(() => {
      const aiMessage: Message = {
        id: (Date.now() + 1).toString(),
        role: 'assistant',
        content: `I understand you want to: "${input}". I can help you with that! Here's what I suggest:\n\n1. First, let's analyze the current code structure\n2. Then, we'll implement the necessary changes\n3. Finally, we'll test the new functionality\n\nWould you like me to proceed?`,
        timestamp: new Date()
      };
      setMessages(prev => [...prev, aiMessage]);
      setIsThinking(false);
    }, 2000);
  };

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  return (
    <AnimatePresence>
      {isOpen && (
        <motion.div
          initial={{ x: '100%' }}
          animate={{ x: 0 }}
          exit={{ x: '100%' }}
          transition={{ duration: 0.3, ease: 'easeInOut' }}
          className="fixed right-0 top-0 h-screen w-[380px] z-40 flex flex-col border-l border-[rgba(0,212,255,0.2)] overflow-hidden"
          style={{
            background: 'linear-gradient(180deg, rgba(10, 10, 15, 0.98) 0%, rgba(15, 15, 25, 0.98) 100%)',
            backdropFilter: 'blur(20px)',
            boxShadow: 'inset 1px 0 0 rgba(0, 212, 255, 0.1), -4px 0 20px rgba(0, 0, 0, 0.5)'
          }}
        >
          {/* Header */}
          <div 
            className="flex-shrink-0 border-b border-[rgba(0,212,255,0.2)] relative"
            style={{
              background: 'linear-gradient(180deg, rgba(0, 212, 255, 0.05) 0%, transparent 100%)'
            }}
          >
            <div className="flex items-center justify-between px-4 py-3">
              <div className="flex items-center gap-2">
                <Sparkles className="w-4 h-4 text-[#00d4ff]" />
                <h3 
                  className="text-sm font-semibold tracking-wider uppercase"
                  style={{ color: '#e0e0e0' }}
                >
                  AI Assistant
                </h3>
              </div>
              
              <div className="flex items-center gap-1">
                <motion.button
                  whileHover={{ scale: 1.1 }}
                  whileTap={{ scale: 0.9 }}
                  className="w-7 h-7 rounded-lg flex items-center justify-center hover:bg-[rgba(0,212,255,0.1)] transition-colors"
                  onClick={() => setMessages([])}
                  title="New chat"
                >
                  <Plus className="w-4 h-4 text-[#00d4ff]" />
                </motion.button>
                
                <motion.button
                  whileHover={{ scale: 1.1 }}
                  whileTap={{ scale: 0.9 }}
                  className="w-7 h-7 rounded-lg flex items-center justify-center hover:bg-[rgba(0,212,255,0.1)] transition-colors"
                >
                  <Settings className="w-4 h-4 text-[#9ca3af]" />
                </motion.button>
                
                <motion.button
                  whileHover={{ scale: 1.1 }}
                  whileTap={{ scale: 0.9 }}
                  className="w-7 h-7 rounded-lg flex items-center justify-center hover:bg-[rgba(0,212,255,0.1)] transition-colors"
                >
                  <MoreHorizontal className="w-4 h-4 text-[#9ca3af]" />
                </motion.button>
                
                <div className="w-px h-4 bg-[rgba(0,212,255,0.2)] mx-1" />
                
                <motion.button
                  whileHover={{ scale: 1.1 }}
                  whileTap={{ scale: 0.9 }}
                  className="w-7 h-7 rounded-lg flex items-center justify-center hover:bg-[rgba(0,212,255,0.1)] transition-colors"
                >
                  <Maximize2 className="w-4 h-4 text-[#9ca3af]" />
                </motion.button>
              </div>
            </div>
            
            {/* Blue underline accent */}
            <motion.div
              className="h-0.5 bg-gradient-to-r from-transparent via-[#00d4ff] to-transparent"
              initial={{ scaleX: 0 }}
              animate={{ scaleX: 1 }}
              transition={{ duration: 0.5, delay: 0.2 }}
            />
          </div>

          {/* Chat Messages Area */}
          <div className="flex-1 overflow-y-auto px-4 py-4 space-y-4">
            {messages.length === 0 ? (
              <div className="h-full flex flex-col items-center justify-center text-center px-6">
                <motion.div
                  initial={{ scale: 0.8, opacity: 0 }}
                  animate={{ scale: 1, opacity: 1 }}
                  transition={{ duration: 0.5 }}
                  className="relative mb-6"
                >
                  <div 
                    className="w-20 h-20 rounded-2xl flex items-center justify-center relative"
                    style={{
                      background: 'linear-gradient(135deg, rgba(0, 212, 255, 0.1) 0%, rgba(185, 103, 255, 0.1) 100%)',
                      border: '1px solid rgba(0, 212, 255, 0.3)',
                      boxShadow: '0 0 30px rgba(0, 212, 255, 0.2)'
                    }}
                  >
                    <Sparkles className="w-10 h-10 text-[#00d4ff]" />
                    
                    {/* Animated sparkle */}
                    <motion.div
                      className="absolute -top-1 -right-1 w-3 h-3"
                      animate={{
                        scale: [1, 1.5, 1],
                        opacity: [0.5, 1, 0.5]
                      }}
                      transition={{
                        duration: 2,
                        repeat: Infinity,
                        ease: 'easeInOut'
                      }}
                    >
                      <Sparkles className="w-3 h-3 text-[#b967ff]" />
                    </motion.div>
                  </div>
                </motion.div>
                
                <h3 
                  className="text-xl font-semibold mb-2"
                  style={{ color: '#e0e0e0' }}
                >
                  Build with Atlas AI
                </h3>
                <p className="text-sm text-[#9ca3af] mb-1">
                  AI responses may be inaccurate.
                </p>
                <p className="text-xs text-[#6b7280] mt-4 max-w-xs">
                  Describe what you want to build, and I'll help you code it step by step.
                </p>
              </div>
            ) : (
              <>
                {messages.map((message) => (
                  <motion.div
                    key={message.id}
                    initial={{ opacity: 0, y: 10 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ duration: 0.3 }}
                    className={`flex gap-3 ${message.role === 'user' ? 'flex-row-reverse' : 'flex-row'}`}
                  >
                    {/* Avatar */}
                    <div 
                      className="flex-shrink-0 w-8 h-8 rounded-lg flex items-center justify-center"
                      style={{
                        background: message.role === 'user' 
                          ? 'linear-gradient(135deg, rgba(185, 103, 255, 0.2) 0%, rgba(185, 103, 255, 0.1) 100%)'
                          : 'linear-gradient(135deg, rgba(0, 212, 255, 0.2) 0%, rgba(0, 212, 255, 0.1) 100%)',
                        border: message.role === 'user'
                          ? '1px solid rgba(185, 103, 255, 0.3)'
                          : '1px solid rgba(0, 212, 255, 0.3)',
                        boxShadow: message.role === 'user'
                          ? '0 0 10px rgba(185, 103, 255, 0.2)'
                          : '0 0 10px rgba(0, 212, 255, 0.2)'
                      }}
                    >
                      {message.role === 'user' ? (
                        <User className="w-4 h-4 text-[#b967ff]" />
                      ) : (
                        <Bot className="w-4 h-4 text-[#00d4ff]" />
                      )}
                    </div>

                    {/* Message bubble */}
                    <div 
                      className={`flex-1 rounded-xl px-4 py-3 ${
                        message.role === 'user' ? 'text-right' : 'text-left'
                      }`}
                      style={{
                        background: message.role === 'user'
                          ? 'linear-gradient(135deg, rgba(185, 103, 255, 0.15) 0%, rgba(185, 103, 255, 0.05) 100%)'
                          : 'linear-gradient(135deg, rgba(0, 212, 255, 0.1) 0%, rgba(0, 212, 255, 0.05) 100%)',
                        border: message.role === 'user'
                          ? '1px solid rgba(185, 103, 255, 0.2)'
                          : '1px solid rgba(0, 212, 255, 0.2)',
                        backdropFilter: 'blur(10px)'
                      }}
                    >
                      <p className="text-sm text-[#e0e0e0] whitespace-pre-wrap">
                        {message.content}
                      </p>
                      <p className="text-xs text-[#6b7280] mt-2">
                        {message.timestamp.toLocaleTimeString([], { 
                          hour: '2-digit', 
                          minute: '2-digit' 
                        })}
                      </p>
                    </div>
                  </motion.div>
                ))}

                {/* Thinking indicator */}
                {isThinking && (
                  <motion.div
                    initial={{ opacity: 0, y: 10 }}
                    animate={{ opacity: 1, y: 0 }}
                    className="flex gap-3"
                  >
                    <div 
                      className="flex-shrink-0 w-8 h-8 rounded-lg flex items-center justify-center"
                      style={{
                        background: 'linear-gradient(135deg, rgba(0, 212, 255, 0.2) 0%, rgba(0, 212, 255, 0.1) 100%)',
                        border: '1px solid rgba(0, 212, 255, 0.3)',
                        boxShadow: '0 0 10px rgba(0, 212, 255, 0.2)'
                      }}
                    >
                      <Bot className="w-4 h-4 text-[#00d4ff]" />
                    </div>
                    
                    <div 
                      className="flex-1 rounded-xl px-4 py-3"
                      style={{
                        background: 'linear-gradient(135deg, rgba(0, 212, 255, 0.1) 0%, rgba(0, 212, 255, 0.05) 100%)',
                        border: '1px solid rgba(0, 212, 255, 0.2)',
                        backdropFilter: 'blur(10px)'
                      }}
                    >
                      <div className="flex gap-1">
                        {[0, 1, 2].map((i) => (
                          <motion.div
                            key={i}
                            className="w-2 h-2 rounded-full bg-[#00d4ff]"
                            animate={{
                              scale: [1, 1.5, 1],
                              opacity: [0.3, 1, 0.3]
                            }}
                            transition={{
                              duration: 1,
                              repeat: Infinity,
                              delay: i * 0.2
                            }}
                          />
                        ))}
                      </div>
                    </div>
                  </motion.div>
                )}
                
                <div ref={messagesEndRef} />
              </>
            )}
          </div>

          {/* Input Area */}
          <div 
            className="flex-shrink-0 border-t border-[rgba(0,212,255,0.2)] p-4"
            style={{
              background: 'linear-gradient(0deg, rgba(0, 212, 255, 0.03) 0%, transparent 100%)'
            }}
          >
            {/* Context input */}
            <div className="mb-3">
              <div 
                className="flex items-center gap-2 px-3 py-2 rounded-lg"
                style={{
                  background: 'rgba(0, 212, 255, 0.05)',
                  border: '1px solid rgba(0, 212, 255, 0.2)'
                }}
              >
                <Paperclip className="w-4 h-4 text-[#6b7280]" />
                <input
                  type="text"
                  placeholder="Add Context..."
                  value={context}
                  onChange={(e) => setContext(e.target.value)}
                  className="flex-1 bg-transparent text-sm text-[#9ca3af] outline-none placeholder:text-[#6b7280]"
                  style={{ fontFamily: "'Inter', sans-serif" }}
                />
              </div>
            </div>

            {/* Main input */}
            <div 
              className="flex items-end gap-2 p-3 rounded-xl"
              style={{
                background: 'linear-gradient(135deg, rgba(0, 212, 255, 0.08) 0%, rgba(185, 103, 255, 0.08) 100%)',
                border: '1px solid rgba(0, 212, 255, 0.3)',
                boxShadow: '0 0 20px rgba(0, 212, 255, 0.1)'
              }}
            >
              <div className="flex-1">
                <textarea
                  placeholder="Describe what to build next"
                  value={input}
                  onChange={(e) => setInput(e.target.value)}
                  onKeyPress={handleKeyPress}
                  rows={2}
                  className="w-full bg-transparent text-sm text-[#e0e0e0] outline-none resize-none placeholder:text-[#6b7280]"
                  style={{ fontFamily: "'Inter', sans-serif" }}
                />
                
                <div className="flex items-center gap-2 mt-2">
                  <button 
                    className="px-2 py-1 rounded text-xs flex items-center gap-1"
                    style={{
                      background: 'rgba(0, 212, 255, 0.1)',
                      color: '#00d4ff',
                      border: '1px solid rgba(0, 212, 255, 0.3)'
                    }}
                  >
                    Agent <ChevronDown className="w-3 h-3" />
                  </button>
                  
                  <button 
                    className="px-2 py-1 rounded text-xs flex items-center gap-1"
                    style={{
                      background: 'rgba(185, 103, 255, 0.1)',
                      color: '#b967ff',
                      border: '1px solid rgba(185, 103, 255, 0.3)'
                    }}
                  >
                    Auto <ChevronDown className="w-3 h-3" />
                  </button>
                </div>
              </div>

              <motion.button
                whileHover={{ 
                  scale: 1.05,
                  boxShadow: '0 0 20px rgba(0, 212, 255, 0.4)'
                }}
                whileTap={{ scale: 0.95 }}
                onClick={handleSend}
                disabled={!input.trim()}
                className="w-10 h-10 rounded-lg flex items-center justify-center disabled:opacity-50 disabled:cursor-not-allowed"
                style={{
                  background: input.trim() 
                    ? 'linear-gradient(135deg, #00d4ff 0%, #b967ff 100%)'
                    : 'rgba(0, 212, 255, 0.2)',
                  boxShadow: input.trim() 
                    ? '0 0 15px rgba(0, 212, 255, 0.3)'
                    : 'none'
                }}
              >
                <Send className="w-4 h-4 text-white" />
              </motion.button>
            </div>
          </div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}