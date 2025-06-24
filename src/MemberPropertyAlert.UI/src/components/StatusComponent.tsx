import React, { useState, useEffect } from 'react';

interface StatusInfo {
  version: string;
  status: string;
  timestamp: string;
  environment: string;
  healthMonitoring: string;
}

const StatusComponent: React.FC = () => {
  const [status, setStatus] = useState<StatusInfo | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchStatus = async () => {
      try {
        const apiBaseUrl = process.env.REACT_APP_API_BASE_URL || '';
        const response = await fetch(`${apiBaseUrl}/status`);
        
        if (!response.ok) {
          throw new Error('Failed to fetch status');
        }
        
        const data = await response.json();
        setStatus(data);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Unknown error');
      } finally {
        setLoading(false);
      }
    };

    fetchStatus();
  }, []);

  if (loading) {
    return (
      <div className="flex items-center justify-center p-4">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
        <span className="ml-2">Loading status...</span>
      </div>
    );
  }

  if (error) {
    return (
      <div className="bg-red-50 border border-red-200 rounded-md p-4">
        <div className="flex">
          <div className="flex-shrink-0">
            <svg className="h-5 w-5 text-red-400" viewBox="0 0 20 20" fill="currentColor">
              <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
            </svg>
          </div>
          <div className="ml-3">
            <h3 className="text-sm font-medium text-red-800">Error loading status</h3>
            <p className="text-sm text-red-700 mt-1">{error}</p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="bg-white shadow rounded-lg p-6">
      <h2 className="text-lg font-medium text-gray-900 mb-4">System Status</h2>
      
      {status && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="bg-gray-50 p-4 rounded-lg">
            <dt className="text-sm font-medium text-gray-500">Version</dt>
            <dd className="mt-1 text-sm text-gray-900">{status.version}</dd>
          </div>
          
          <div className="bg-gray-50 p-4 rounded-lg">
            <dt className="text-sm font-medium text-gray-500">Status</dt>
            <dd className="mt-1">
              <span className={`inline-flex px-2 py-1 text-xs font-semibold rounded-full ${
                status.status === 'Healthy' 
                  ? 'bg-green-100 text-green-800' 
                  : 'bg-red-100 text-red-800'
              }`}>
                {status.status}
              </span>
            </dd>
          </div>
          
          <div className="bg-gray-50 p-4 rounded-lg">
            <dt className="text-sm font-medium text-gray-500">Environment</dt>
            <dd className="mt-1 text-sm text-gray-900">{status.environment}</dd>
          </div>
          
          <div className="bg-gray-50 p-4 rounded-lg">
            <dt className="text-sm font-medium text-gray-500">Health Monitoring</dt>
            <dd className="mt-1 text-sm text-gray-900">
              {status.healthMonitoring === 'true' ? 'Enabled' : 'Disabled'}
            </dd>
          </div>
          
          <div className="bg-gray-50 p-4 rounded-lg md:col-span-2">
            <dt className="text-sm font-medium text-gray-500">Last Updated</dt>
            <dd className="mt-1 text-sm text-gray-900">
              {new Date(status.timestamp).toLocaleString()}
            </dd>
          </div>
        </div>
      )}
    </div>
  );
};

export default StatusComponent;
