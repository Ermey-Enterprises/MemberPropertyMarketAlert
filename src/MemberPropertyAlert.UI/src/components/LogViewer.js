import React, { useRef, useEffect } from 'react';
import { Trash2, Download, Filter } from 'lucide-react';

const LogViewer = ({ logs, onClearLogs, isConnected }) => {
  const logContainerRef = useRef(null);

  useEffect(() => {
    // Auto-scroll to bottom when new logs arrive
    if (logContainerRef.current) {
      logContainerRef.current.scrollTop = logContainerRef.current.scrollHeight;
    }
  }, [logs]);

  const getLogLevelColor = (level) => {
    switch (level?.toLowerCase()) {
      case 'error':
        return 'text-red-600 bg-red-50';
      case 'warning':
        return 'text-yellow-600 bg-yellow-50';
      case 'info':
        return 'text-blue-600 bg-blue-50';
      case 'debug':
        return 'text-gray-600 bg-gray-50';
      default:
        return 'text-gray-800 bg-white';
    }
  };

  const formatTimestamp = (timestamp) => {
    return new Date(timestamp).toLocaleTimeString();
  };

  const downloadLogs = () => {
    const logText = logs.map(log => 
      `[${formatTimestamp(log.timestamp)}] ${log.level?.toUpperCase() || 'INFO'}: ${log.message}`
    ).join('\n');
    
    const blob = new Blob([logText], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `property-alert-logs-${new Date().toISOString().split('T')[0]}.txt`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  };

  return (
    <div className="bg-white rounded-lg shadow h-96 flex flex-col">
      {/* Header */}
      <div className="flex items-center justify-between p-4 border-b border-gray-200">
        <div className="flex items-center space-x-2">
          <h3 className="text-lg font-medium text-gray-900">Activity Logs</h3>
          <div className={`w-2 h-2 rounded-full ${isConnected ? 'bg-green-500' : 'bg-red-500'}`}></div>
        </div>
        
        <div className="flex items-center space-x-2">
          <button
            onClick={downloadLogs}
            disabled={logs.length === 0}
            className="p-2 text-gray-500 hover:text-gray-700 disabled:opacity-50 disabled:cursor-not-allowed"
            title="Download Logs"
          >
            <Download size={16} />
          </button>
          
          <button
            onClick={onClearLogs}
            disabled={logs.length === 0}
            className="p-2 text-gray-500 hover:text-red-600 disabled:opacity-50 disabled:cursor-not-allowed"
            title="Clear Logs"
          >
            <Trash2 size={16} />
          </button>
        </div>
      </div>

      {/* Log Content */}
      <div 
        ref={logContainerRef}
        className="flex-1 overflow-y-auto p-4 space-y-2 font-mono text-sm"
      >
        {logs.length === 0 ? (
          <div className="text-center text-gray-500 py-8">
            <div className="text-gray-400 mb-2">No logs yet</div>
            <div className="text-xs">
              {isConnected ? 'Waiting for activity...' : 'Disconnected from log stream'}
            </div>
          </div>
        ) : (
          logs.map((log, index) => (
            <div
              key={index}
              className={`p-2 rounded text-xs border-l-4 ${getLogLevelColor(log.level)}`}
              style={{ borderLeftColor: getLogLevelColor(log.level).includes('red') ? '#dc2626' : 
                                        getLogLevelColor(log.level).includes('yellow') ? '#d97706' :
                                        getLogLevelColor(log.level).includes('blue') ? '#2563eb' : '#6b7280' }}
            >
              <div className="flex items-start justify-between">
                <div className="flex-1">
                  <div className="font-medium">
                    [{formatTimestamp(log.timestamp)}] {log.level?.toUpperCase() || 'INFO'}
                  </div>
                  <div className="mt-1 whitespace-pre-wrap">{log.message}</div>
                  {log.details && (
                    <div className="mt-1 text-xs opacity-75">
                      {typeof log.details === 'string' ? log.details : JSON.stringify(log.details, null, 2)}
                    </div>
                  )}
                </div>
              </div>
            </div>
          ))
        )}
      </div>

      {/* Footer */}
      <div className="px-4 py-2 border-t border-gray-200 text-xs text-gray-500">
        {logs.length} log entries • {isConnected ? 'Live' : 'Offline'}
      </div>
    </div>
  );
};

export default LogViewer;
