import { motion, AnimatePresence } from 'motion/react';
import { X, Command, Keyboard } from 'lucide-react';

interface ShortcutsOverlayProps {
  isOpen: boolean;
  onClose: () => void;
}

const shortcuts = [
  { category: 'General', items: [
    { keys: ['Ctrl', 'P'], description: 'Quick Open, Go to File' },
    { keys: ['Ctrl', 'Shift', 'P'], description: 'Command Palette' },
    { keys: ['Ctrl', 'B'], description: 'Toggle Sidebar' },
    { keys: ['Ctrl', '`'], description: 'Toggle Terminal' },
  ]},
  { category: 'Editor', items: [
    { keys: ['Ctrl', 'S'], description: 'Save' },
    { keys: ['Ctrl', 'F'], description: 'Find' },
    { keys: ['Ctrl', 'H'], description: 'Replace' },
    { keys: ['Ctrl', '/'], description: 'Toggle Comment' },
    { keys: ['Alt', '↑/↓'], description: 'Move Line Up/Down' },
  ]},
  { category: 'Debug', items: [
    { keys: ['F5'], description: 'Start/Continue Debugging' },
    { keys: ['F9'], description: 'Toggle Breakpoint' },
    { keys: ['F10'], description: 'Step Over' },
    { keys: ['F11'], description: 'Step Into' },
  ]},
  { category: 'Navigation', items: [
    { keys: ['Ctrl', 'Tab'], description: 'Switch Tabs' },
    { keys: ['Ctrl', 'W'], description: 'Close Tab' },
    { keys: ['Ctrl', 'G'], description: 'Go to Line' },
  ]},
];

export function KeyboardShortcuts({ isOpen, onClose }: ShortcutsOverlayProps) {
  return (
    <AnimatePresence>
      {isOpen && (
        <>
          {/* Backdrop */}
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50"
            onClick={onClose}
          />

          {/* Modal */}
          <motion.div
            initial={{ opacity: 0, scale: 0.9, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.9, y: 20 }}
            transition={{ type: 'spring', damping: 25, stiffness: 300 }}
            className="fixed top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[800px] max-h-[80vh] bg-[#0f0f1a] border border-[rgba(0,212,255,0.3)] rounded-2xl overflow-hidden z-50"
            style={{
              boxShadow: '0 0 40px rgba(0, 212, 255, 0.3), 0 0 80px rgba(185, 103, 255, 0.2)',
              backdropFilter: 'blur(20px)',
            }}
          >
            {/* Header */}
            <div className="p-6 border-b border-[rgba(0,212,255,0.2)] bg-gradient-to-r from-[rgba(0,212,255,0.1)] to-[rgba(185,103,255,0.1)]">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className="w-10 h-10 bg-gradient-to-br from-[#00d4ff] to-[#b967ff] rounded-xl flex items-center justify-center">
                    <Keyboard className="w-5 h-5 text-[#0a0a0f]" />
                  </div>
                  <div>
                    <h2 className="text-xl font-semibold text-[#e0e0e8]">
                      Keyboard Shortcuts
                    </h2>
                    <p className="text-sm text-[#8b8b9a]">
                      Master your workflow with these shortcuts
                    </p>
                  </div>
                </div>
                <motion.button
                  onClick={onClose}
                  className="w-8 h-8 flex items-center justify-center rounded-lg hover:bg-white/10 transition-colors"
                  whileHover={{ scale: 1.1 }}
                  whileTap={{ scale: 0.9 }}
                >
                  <X className="w-5 h-5 text-[#8b8b9a]" />
                </motion.button>
              </div>
            </div>

            {/* Content */}
            <div className="p-6 overflow-auto max-h-[calc(80vh-120px)]">
              <div className="grid grid-cols-2 gap-6">
                {shortcuts.map((category, idx) => (
                  <motion.div
                    key={category.category}
                    initial={{ opacity: 0, y: 20 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ delay: idx * 0.1 }}
                  >
                    <h3 className="text-sm uppercase tracking-wider text-[#00d4ff] mb-3 flex items-center gap-2">
                      <div className="w-1 h-4 bg-gradient-to-b from-[#00d4ff] to-[#b967ff] rounded" />
                      {category.category}
                    </h3>
                    <div className="space-y-2">
                      {category.items.map((shortcut, i) => (
                        <motion.div
                          key={i}
                          className="flex items-center justify-between p-2 rounded-lg hover:bg-white/5 transition-colors"
                          whileHover={{ x: 4 }}
                        >
                          <span className="text-sm text-[#e0e0e8]">
                            {shortcut.description}
                          </span>
                          <div className="flex gap-1">
                            {shortcut.keys.map((key, j) => (
                              <span
                                key={j}
                                className="px-2 py-1 bg-[#1a1a24] border border-[rgba(0,212,255,0.2)] rounded text-xs text-[#00d4ff] font-mono"
                                style={{
                                  boxShadow: '0 0 10px rgba(0, 212, 255, 0.1)'
                                }}
                              >
                                {key}
                              </span>
                            ))}
                          </div>
                        </motion.div>
                      ))}
                    </div>
                  </motion.div>
                ))}
              </div>
            </div>

            {/* Footer */}
            <div className="p-4 border-t border-[rgba(0,212,255,0.1)] bg-[rgba(8,8,16,0.8)] flex items-center justify-center">
              <p className="text-xs text-[#8b8b9a]">
                Press <kbd className="px-2 py-0.5 bg-[#1a1a24] border border-[rgba(0,212,255,0.2)] rounded text-[#00d4ff] mx-1">?</kbd> to toggle this panel
              </p>
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}
