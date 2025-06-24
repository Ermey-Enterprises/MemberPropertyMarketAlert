import React, { useState, useEffect } from 'react';
import { Play, Pause, RotateCcw, Clock, Settings } from 'lucide-react';
import axios from 'axios';

const ScanControl = ({ connection }) => {
  const [isScanning, setIsScanning] = useState(false);
  const [scanSchedule, setScanSchedule] = useState({
    enabled: true,
    frequency: 'daily',
    time: '09:00',
    timezone: 'UTC'
  });
  const [lastScanTime, setLastScanTime] = useState(null);
  const [nextScanTime, setNextScanTime] = useState(null);
  const [scanStats, setScanStats] = useState({
    totalStates: 0,
    propertiesMonitored: 0,
    newListings: 0,
    matches: 0
  });

  useEffect(() => {
    // Load current schedule and stats
    loadScanSchedule();
    loadScanStats();
  }, []);

  const loadScanSchedule = async () => {
    try {
      const response = await axios.get('/api/scan/schedule');
      setScanSchedule(response.data);
      setNextScanTime(response.data.nextRun);
    } catch (error) {
      console.error('Failed to load scan schedule:', error);
    }
  };

  const loadScanStats = async () => {
    try {
      const response = await axios.get('/api/scan/stats');
      setScanStats(response.data);
      setLastScanTime(response.data.lastScanTime);
    } catch (error) {
      console.error('Failed to load scan stats:', error);
    }
  };

  const startManualScan = async () => {
    try {
      setIsScanning(true);
      await axios.post('/api/scan/start');
      
      if (connection) {
        // Listen for scan completion
        connection.on('ScanCompleted', (result) => {
          setIsScanning(false);
          setScanStats(result.stats);
          setLastScanTime(new Date().toISOString());
        });
      }
    } catch (error) {
      console.error('Failed to start scan:', error);
      setIsScanning(false);
    }
  };

  const stopScan = async () => {
    try {
      await axios.post('/api/scan/stop');
      setIsScanning(false);
    } catch (error) {
      console.error('Failed to stop scan:', error);
    }
  };

  const updateSchedule = async () => {
    try {
      const response = await axios.put('/api/scan/schedule', scanSchedule);
      setNextScanTime(response.data.nextRun);
    } catch (error) {
      console.error('Failed to update schedule:', error);
    }
  };

  const formatDateTime = (dateString) => {
    if (!dateString) return 'Never';
    return new Date(dateString).toLocaleString();
  };

  return (
    <div className="bg-white rounded-lg shadow p-6">
      <h2 className="text-lg font-semibold text-gray-900 mb-6">Scan Control</h2>
      
      {/* Manual Scan Controls */}
      <div className="mb-8">
        <h3 className="text-md font-medium text-gray-700 mb-4">Manual Scan</h3>
        <div className="flex items-center space-x-4">
          <button
            onClick={startManualScan}
            disabled={isScanning}
            className={`flex items-center space-x-2 px-4 py-2 rounded-md font-medium ${
              isScanning 
                ? 'bg-gray-300 text-gray-500 cursor-not-allowed' 
                : 'bg-blue-600 text-white hover:bg-blue-700'
            }`}
          >
            <Play size={16} />
            <span>{isScanning ? 'Scanning...' : 'Start Scan'}</span>
          </button>
          
          {isScanning && (
            <button
              onClick={stopScan}
              className="flex items-center space-x-2 px-4 py-2 bg-red-600 text-white rounded-md font-medium hover:bg-red-700"
            >
              <Pause size={16} />
              <span>Stop Scan</span>
            </button>
          )}
          
          <button
            onClick={loadScanStats}
            className="flex items-center space-x-2 px-4 py-2 bg-gray-600 text-white rounded-md font-medium hover:bg-gray-700"
          >
            <RotateCcw size={16} />
            <span>Refresh</span>
          </button>
        </div>
      </div>

      {/* Schedule Configuration */}
      <div className="mb-8">
        <h3 className="text-md font-medium text-gray-700 mb-4 flex items-center">
          <Clock size={16} className="mr-2" />
          Scheduled Scans
        </h3>
        
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Enable Scheduled Scans
            </label>
            <input
              type="checkbox"
              checked={scanSchedule.enabled}
              onChange={(e) => setScanSchedule({...scanSchedule, enabled: e.target.checked})}
              className="h-4 w-4 text-blue-600 focus:ring-blue-500 border-gray-300 rounded"
            />
          </div>
          
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Frequency
            </label>
            <select
              value={scanSchedule.frequency}
              onChange={(e) => setScanSchedule({...scanSchedule, frequency: e.target.value})}
              className="block w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
            >
              <option value="hourly">Every Hour</option>
              <option value="daily">Daily</option>
              <option value="weekly">Weekly</option>
            </select>
          </div>
          
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Time
            </label>
            <input
              type="time"
              value={scanSchedule.time}
              onChange={(e) => setScanSchedule({...scanSchedule, time: e.target.value})}
              className="block w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
            />
          </div>
          
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Timezone
            </label>
            <select
              value={scanSchedule.timezone}
              onChange={(e) => setScanSchedule({...scanSchedule, timezone: e.target.value})}
              className="block w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
            >
              <option value="UTC">UTC</option>
              <option value="America/New_York">Eastern Time</option>
              <option value="America/Chicago">Central Time</option>
              <option value="America/Denver">Mountain Time</option>
              <option value="America/Los_Angeles">Pacific Time</option>
            </select>
          </div>
        </div>
        
        <button
          onClick={updateSchedule}
          className="mt-4 flex items-center space-x-2 px-4 py-2 bg-green-600 text-white rounded-md font-medium hover:bg-green-700"
        >
          <Settings size={16} />
          <span>Update Schedule</span>
        </button>
      </div>

      {/* Scan Statistics */}
      <div>
        <h3 className="text-md font-medium text-gray-700 mb-4">Scan Statistics</h3>
        
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-4">
          <div className="bg-blue-50 p-4 rounded-lg">
            <div className="text-2xl font-bold text-blue-600">{scanStats.totalStates}</div>
            <div className="text-sm text-gray-600">States Monitored</div>
          </div>
          
          <div className="bg-green-50 p-4 rounded-lg">
            <div className="text-2xl font-bold text-green-600">{scanStats.propertiesMonitored.toLocaleString()}</div>
            <div className="text-sm text-gray-600">Properties Monitored</div>
          </div>
          
          <div className="bg-yellow-50 p-4 rounded-lg">
            <div className="text-2xl font-bold text-yellow-600">{scanStats.newListings}</div>
            <div className="text-sm text-gray-600">New Listings Today</div>
          </div>
          
          <div className="bg-red-50 p-4 rounded-lg">
            <div className="text-2xl font-bold text-red-600">{scanStats.matches}</div>
            <div className="text-sm text-gray-600">Member Matches</div>
          </div>
        </div>
        
        <div className="text-sm text-gray-600 space-y-1">
          <div><strong>Last Scan:</strong> {formatDateTime(lastScanTime)}</div>
          <div><strong>Next Scheduled:</strong> {formatDateTime(nextScanTime)}</div>
        </div>
      </div>
    </div>
  );
};

export default ScanControl;
