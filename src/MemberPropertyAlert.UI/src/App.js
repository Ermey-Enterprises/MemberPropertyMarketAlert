import React, { useState, useEffect } from 'react';
import { BrowserRouter as Router, Routes, Route, Link } from 'react-router-dom';
import { HubConnectionBuilder } from '@microsoft/signalr';
import Dashboard from './components/Dashboard';
import ScanControl from './components/ScanControl';
import LogViewer from './components/LogViewer';
import InstitutionManager from './components/InstitutionManager';
import './App.css';

function App() {
  const [connection, setConnection] = useState(null);
  const [logs, setLogs] = useState([]);
  const [isConnected, setIsConnected] = useState(false);

  useEffect(() => {
    // Initialize SignalR connection for real-time logs
    const newConnection = new HubConnectionBuilder()
      .withUrl('/api/loghub')
      .build();

    setConnection(newConnection);

    newConnection.start()
      .then(() => {
        console.log('SignalR Connected');
        setIsConnected(true);
        
        // Listen for log messages
        newConnection.on('LogMessage', (logEntry) => {
          setLogs(prevLogs => [...prevLogs, {
            ...logEntry,
            timestamp: new Date().toISOString()
          }]);
        });

        // Listen for scan status updates
        newConnection.on('ScanStatusUpdate', (status) => {
          console.log('Scan status update:', status);
        });
      })
      .catch(err => {
        console.error('SignalR Connection Error:', err);
        setIsConnected(false);
      });

    return () => {
      if (newConnection) {
        newConnection.stop();
      }
    };
  }, []);

  const clearLogs = () => {
    setLogs([]);
  };

  return (
    <Router>
      <div className="min-h-screen bg-gray-100">
        {/* Navigation Header */}
        <nav className="bg-white shadow-lg">
          <div className="max-w-7xl mx-auto px-4">
            <div className="flex justify-between h-16">
              <div className="flex items-center">
                <h1 className="text-xl font-bold text-gray-900">
                  Member Property Alert - Admin Dashboard
                </h1>
              </div>
              <div className="flex items-center space-x-4">
                <div className={`flex items-center space-x-2 ${isConnected ? 'text-green-600' : 'text-red-600'}`}>
                  <div className={`w-2 h-2 rounded-full ${isConnected ? 'bg-green-500' : 'bg-red-500'}`}></div>
                  <span className="text-sm">{isConnected ? 'Connected' : 'Disconnected'}</span>
                </div>
              </div>
            </div>
          </div>
        </nav>

        {/* Main Content */}
        <div className="max-w-7xl mx-auto py-6 px-4">
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Left Column - Controls */}
            <div className="lg:col-span-2 space-y-6">
              <Routes>
                <Route path="/" element={<Dashboard />} />
                <Route path="/institutions" element={<InstitutionManager />} />
              </Routes>
              
              {/* Scan Controls */}
              <ScanControl connection={connection} />
            </div>

            {/* Right Column - Log Viewer */}
            <div className="lg:col-span-1">
              <LogViewer 
                logs={logs} 
                onClearLogs={clearLogs}
                isConnected={isConnected}
              />
            </div>
          </div>
        </div>

        {/* Navigation Tabs */}
        <div className="fixed bottom-0 left-0 right-0 bg-white border-t border-gray-200">
          <div className="max-w-7xl mx-auto px-4">
            <div className="flex space-x-8">
              <Link 
                to="/" 
                className="py-4 px-1 border-b-2 border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300 font-medium text-sm"
              >
                Dashboard
              </Link>
              <Link 
                to="/institutions" 
                className="py-4 px-1 border-b-2 border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300 font-medium text-sm"
              >
                Institutions
              </Link>
            </div>
          </div>
        </div>
      </div>
    </Router>
  );
}

export default App;
