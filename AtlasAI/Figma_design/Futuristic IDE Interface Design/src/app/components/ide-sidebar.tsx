import { useState } from 'react';
import { 
  Files, 
  Search, 
  GitBranch, 
  Package, 
  Bug,
  ChevronRight,
  ChevronDown,
  FileCode,
  Folder,
  FolderOpen,
  Circle
} from 'lucide-react';
import { motion } from 'motion/react';

type Panel = 'explorer' | 'search' | 'git' | 'extensions' | 'debug';

interface FileNode {
  name: string;
  type: 'file' | 'folder';
  gitStatus?: 'modified' | 'added' | 'untracked';
  children?: FileNode[];
}

const fileTree: FileNode[] = [
  {
    name: 'src',
    type: 'folder',
    children: [
      {
        name: 'components',
        type: 'folder',
        children: [
          { name: 'Header.tsx', type: 'file', gitStatus: 'modified' },
          { name: 'Sidebar.tsx', type: 'file' },
          { name: 'CodeEditor.tsx', type: 'file', gitStatus: 'modified' },
        ]
      },
      {
        name: 'utils',
        type: 'folder',
        children: [
          { name: 'helpers.ts', type: 'file' },
          { name: 'api.ts', type: 'file', gitStatus: 'added' },
        ]
      },
      { name: 'App.tsx', type: 'file' },
      { name: 'index.tsx', type: 'file' },
    ]
  },
  {
    name: 'public',
    type: 'folder',
    children: [
      { name: 'index.html', type: 'file' },
      { name: 'favicon.ico', type: 'file' },
    ]
  },
  { name: 'package.json', type: 'file' },
  { name: 'tsconfig.json', type: 'file' },
  { name: 'README.md', type: 'file', gitStatus: 'untracked' },
];

function FileTreeNode({ node, depth = 0 }: { node: FileNode; depth?: number }) {
  const [isOpen, setIsOpen] = useState(depth === 0);

  const handleClick = () => {
    if (node.type === 'folder') {
      setIsOpen(!isOpen);
    }
  };

  const statusColor = {
    modified: 'text-[#00d4ff]',
    added: 'text-[#00ff9f]',
    untracked: 'text-[#b967ff]'
  };

  return (
    <div>
      <div
        className={`flex items-center gap-2 px-2 py-1 cursor-pointer hover:bg-white/5 rounded-lg transition-all group ${
          node.gitStatus ? 'hover:shadow-[0_0_15px_rgba(0,212,255,0.2)]' : ''
        }`}
        style={{ paddingLeft: `${depth * 12 + 8}px` }}
        onClick={handleClick}
      >
        {node.type === 'folder' && (
          <motion.div
            animate={{ rotate: isOpen ? 90 : 0 }}
            transition={{ duration: 0.2 }}
          >
            <ChevronRight className="w-3 h-3 text-[#8b8b9a]" />
          </motion.div>
        )}
        {node.type === 'folder' ? (
          isOpen ? (
            <FolderOpen className="w-4 h-4 text-[#00d4ff]" />
          ) : (
            <Folder className="w-4 h-4 text-[#8b8b9a]" />
          )
        ) : (
          <FileCode className="w-4 h-4 text-[#8b8b9a]" />
        )}
        <span className={`text-sm ${node.gitStatus ? statusColor[node.gitStatus] : 'text-[#e0e0e8]'}`}>
          {node.name}
        </span>
        {node.gitStatus && (
          <Circle className="w-2 h-2 ml-auto text-current fill-current opacity-60" />
        )}
      </div>
      {node.type === 'folder' && isOpen && node.children && (
        <motion.div
          initial={{ opacity: 0, height: 0 }}
          animate={{ opacity: 1, height: 'auto' }}
          exit={{ opacity: 0, height: 0 }}
          transition={{ duration: 0.2 }}
        >
          {node.children.map((child, idx) => (
            <FileTreeNode key={idx} node={child} depth={depth + 1} />
          ))}
        </motion.div>
      )}
    </div>
  );
}

export function IDESidebar() {
  const [activePanel, setActivePanel] = useState<Panel>('explorer');
  const [isExpanded, setIsExpanded] = useState(true);

  const panels = [
    { id: 'explorer' as Panel, icon: Files, label: 'Explorer' },
    { id: 'search' as Panel, icon: Search, label: 'Search' },
    { id: 'git' as Panel, icon: GitBranch, label: 'Source Control' },
    { id: 'extensions' as Panel, icon: Package, label: 'Extensions' },
    { id: 'debug' as Panel, icon: Bug, label: 'Debug' },
  ];

  return (
    <div className="flex h-full bg-[var(--near-black)]">
      {/* Icon Bar */}
      <div className="w-14 bg-[#080810] border-r border-[rgba(0,212,255,0.1)] flex flex-col items-center py-4 gap-2">
        {panels.map((panel) => {
          const Icon = panel.icon;
          const isActive = activePanel === panel.id;
          return (
            <motion.button
              key={panel.id}
              className={`w-10 h-10 flex items-center justify-center rounded-xl transition-all relative group ${
                isActive ? 'bg-[rgba(0,212,255,0.15)]' : 'hover:bg-white/5'
              }`}
              onClick={() => {
                setActivePanel(panel.id);
                setIsExpanded(true);
              }}
              whileHover={{ scale: 1.05 }}
              whileTap={{ scale: 0.95 }}
              style={{
                boxShadow: isActive ? '0 0 20px rgba(0, 212, 255, 0.3)' : 'none'
              }}
            >
              <Icon 
                className={`w-5 h-5 ${isActive ? 'text-[#00d4ff]' : 'text-[#8b8b9a]'}`}
              />
              {isActive && (
                <motion.div
                  className="absolute left-0 w-0.5 h-8 bg-[#00d4ff] rounded-r"
                  layoutId="activeIndicator"
                  transition={{ type: 'spring', stiffness: 300, damping: 30 }}
                  style={{
                    boxShadow: '0 0 10px rgba(0, 212, 255, 0.6)'
                  }}
                />
              )}
            </motion.button>
          );
        })}
      </div>

      {/* Panel Content */}
      {isExpanded && (
        <motion.div
          initial={{ width: 0, opacity: 0 }}
          animate={{ width: 280, opacity: 1 }}
          exit={{ width: 0, opacity: 0 }}
          transition={{ duration: 0.3 }}
          className="bg-[var(--deep-charcoal)] border-r border-[rgba(0,212,255,0.1)] overflow-hidden"
          style={{
            backdropFilter: 'blur(10px)',
            backgroundColor: 'rgba(15, 15, 26, 0.8)'
          }}
        >
          <div className="p-4">
            <h2 className="text-sm uppercase tracking-wider text-[#8b8b9a] mb-4">
              {panels.find(p => p.id === activePanel)?.label}
            </h2>

            {activePanel === 'explorer' && (
              <div className="space-y-1">
                {fileTree.map((node, idx) => (
                  <FileTreeNode key={idx} node={node} />
                ))}
              </div>
            )}

            {activePanel === 'search' && (
              <div className="space-y-4">
                <input
                  type="text"
                  placeholder="Search..."
                  className="w-full bg-[#1a1a24] border border-[rgba(0,212,255,0.2)] rounded-lg px-3 py-2 text-sm text-[#e0e0e8] focus:outline-none focus:border-[#00d4ff] focus:shadow-[0_0_15px_rgba(0,212,255,0.3)] transition-all"
                />
                <div className="text-sm text-[#8b8b9a]">
                  No results found
                </div>
              </div>
            )}

            {activePanel === 'git' && (
              <div className="space-y-3">
                <div className="text-sm space-y-2">
                  <div className="text-[#8b8b9a] uppercase text-xs tracking-wider">Changes (3)</div>
                  <div className="space-y-1">
                    <div className="flex items-center gap-2 px-2 py-1 hover:bg-white/5 rounded cursor-pointer">
                      <span className="text-[#00d4ff]">M</span>
                      <span className="text-sm text-[#e0e0e8]">Header.tsx</span>
                    </div>
                    <div className="flex items-center gap-2 px-2 py-1 hover:bg-white/5 rounded cursor-pointer">
                      <span className="text-[#00d4ff]">M</span>
                      <span className="text-sm text-[#e0e0e8]">CodeEditor.tsx</span>
                    </div>
                    <div className="flex items-center gap-2 px-2 py-1 hover:bg-white/5 rounded cursor-pointer">
                      <span className="text-[#00ff9f]">A</span>
                      <span className="text-sm text-[#e0e0e8]">api.ts</span>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {activePanel === 'extensions' && (
              <div className="text-sm text-[#8b8b9a]">
                Extensions panel
              </div>
            )}

            {activePanel === 'debug' && (
              <div className="text-sm text-[#8b8b9a]">
                Debug panel
              </div>
            )}
          </div>
        </motion.div>
      )}
    </div>
  );
}
