import React, { useState, useEffect } from 'react';
import { Plus, Edit, Trash2, Building, Settings, Bell, Globe, Mail, FileText, Webhook } from 'lucide-react';
import axios from 'axios';

const InstitutionManager = () => {
  const [institutions, setInstitutions] = useState([]);
  const [showAddForm, setShowAddForm] = useState(false);
  const [editingInstitution, setEditingInstitution] = useState(null);
  const [showSettingsModal, setShowSettingsModal] = useState(false);
  const [selectedInstitution, setSelectedInstitution] = useState(null);
  const [newInstitution, setNewInstitution] = useState({
    name: '',
    contactEmail: '',
    notificationSettings: {
      deliveryMethods: ['webhook'],
      webhookSettings: {
        url: '',
        authHeader: '',
        customHeaders: {},
        retryPolicy: {
          maxRetries: 3,
          backoffSeconds: [30, 60, 120]
        },
        timeoutSeconds: 30,
        verifySSL: true
      },
      emailSettings: {
        recipients: [],
        subject: 'Property Alert Notification',
        format: 'Html',
        includeAttachments: false
      },
      csvSettings: {
        deliveryMethod: 'email',
        delimiter: ',',
        includeHeaders: true,
        dateFormat: 'yyyy-MM-dd HH:mm:ss',
        includeFields: []
      },
      enableBatching: true,
      batchSize: 10,
      batchTimeoutMinutes: 5
    },
    configuration: {
      useMockServices: false,
      rentCastApiKey: '',
      scanSettings: {
        maxConcurrentScans: 5,
        rateLimitDelayMs: 1000,
        timeoutSeconds: 30,
        enableCaching: true,
        cacheDurationMinutes: 15,
        excludedStates: []
      },
      alertSettings: {
        minPrice: null,
        maxPrice: null,
        minBedrooms: null,
        maxBedrooms: null,
        propertyTypes: [],
        maxDaysOnMarket: 30,
        onlyNewListings: true
      }
    }
  });

  useEffect(() => {
    loadInstitutions();
  }, []);

  const loadInstitutions = async () => {
    try {
      const response = await axios.get('/api/institutions');
      setInstitutions(response.data);
    } catch (error) {
      console.error('Failed to load institutions:', error);
    }
  };

  const addInstitution = async () => {
    try {
      await axios.post('/api/institutions', newInstitution);
      resetNewInstitution();
      setShowAddForm(false);
      loadInstitutions();
    } catch (error) {
      console.error('Failed to add institution:', error);
    }
  };

  const deleteInstitution = async (id) => {
    if (window.confirm('Are you sure you want to delete this institution?')) {
      try {
        await axios.delete(`/api/institutions/${id}`);
        loadInstitutions();
      } catch (error) {
        console.error('Failed to delete institution:', error);
      }
    }
  };

  const openSettingsModal = (institution) => {
    setSelectedInstitution(institution);
    setShowSettingsModal(true);
  };

  const resetNewInstitution = () => {
    setNewInstitution({
      name: '',
      contactEmail: '',
      notificationSettings: {
        deliveryMethods: ['webhook'],
        webhookSettings: {
          url: '',
          authHeader: '',
          customHeaders: {},
          retryPolicy: {
            maxRetries: 3,
            backoffSeconds: [30, 60, 120]
          },
          timeoutSeconds: 30,
          verifySSL: true
        },
        emailSettings: {
          recipients: [],
          subject: 'Property Alert Notification',
          format: 'Html',
          includeAttachments: false
        },
        csvSettings: {
          deliveryMethod: 'email',
          delimiter: ',',
          includeHeaders: true,
          dateFormat: 'yyyy-MM-dd HH:mm:ss',
          includeFields: []
        },
        enableBatching: true,
        batchSize: 10,
        batchTimeoutMinutes: 5
      },
      configuration: {
        useMockServices: false,
        rentCastApiKey: '',
        scanSettings: {
          maxConcurrentScans: 5,
          rateLimitDelayMs: 1000,
          timeoutSeconds: 30,
          enableCaching: true,
          cacheDurationMinutes: 15,
          excludedStates: []
        },
        alertSettings: {
          minPrice: null,
          maxPrice: null,
          minBedrooms: null,
          maxBedrooms: null,
          propertyTypes: [],
          maxDaysOnMarket: 30,
          onlyNewListings: true
        }
      }
    });
  };

  const updateInstitutionSettings = async (settings) => {
    try {
      await axios.put(`/api/institutions/${selectedInstitution.id}`, settings);
      setShowSettingsModal(false);
      setSelectedInstitution(null);
      loadInstitutions();
    } catch (error) {
      console.error('Failed to update institution settings:', error);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <div>
          <h2 className="text-2xl font-bold text-gray-900">Institution Management</h2>
          <p className="text-gray-600">Manage financial institutions and their settings</p>
        </div>
        <button
          onClick={() => setShowAddForm(true)}
          className="flex items-center space-x-2 px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700"
        >
          <Plus size={16} />
          <span>Add Institution</span>
        </button>
      </div>

      {/* Add Institution Form */}
      {showAddForm && (
        <div className="bg-white p-6 rounded-lg shadow">
          <h3 className="text-lg font-medium text-gray-900 mb-4">Add New Institution</h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                Institution Name
              </label>
              <input
                type="text"
                value={newInstitution.name}
                onChange={(e) => setNewInstitution({...newInstitution, name: e.target.value})}
                className="block w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
                placeholder="e.g., First National Credit Union"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                Contact Email
              </label>
              <input
                type="email"
                value={newInstitution.contactEmail}
                onChange={(e) => setNewInstitution({...newInstitution, contactEmail: e.target.value})}
                className="block w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
                placeholder="alerts@institution.com"
              />
            </div>
          </div>
          <div className="mt-4 flex space-x-4">
            <button
              onClick={addInstitution}
              className="px-4 py-2 bg-green-600 text-white rounded-md hover:bg-green-700"
            >
              Add Institution
            </button>
            <button
              onClick={() => setShowAddForm(false)}
              className="px-4 py-2 bg-gray-600 text-white rounded-md hover:bg-gray-700"
            >
              Cancel
            </button>
          </div>
        </div>
      )}

      {/* Institutions List */}
      <div className="bg-white rounded-lg shadow">
        <div className="px-6 py-4 border-b border-gray-200">
          <h3 className="text-lg font-medium text-gray-900">Institutions</h3>
        </div>
        <div className="p-6">
          {institutions.length === 0 ? (
            <div className="text-center py-8">
              <Building className="mx-auto h-12 w-12 text-gray-400" />
              <h3 className="mt-2 text-sm font-medium text-gray-900">No institutions</h3>
              <p className="mt-1 text-sm text-gray-500">Get started by adding a financial institution.</p>
            </div>
          ) : (
            <div className="space-y-4">
              {institutions.map((institution) => (
                <div key={institution.id} className="border border-gray-200 rounded-lg p-4">
                  <div className="flex items-center justify-between">
                    <div>
                      <h4 className="text-lg font-medium text-gray-900">{institution.name}</h4>
                      <p className="text-sm text-gray-600">{institution.contactEmail}</p>
                      <div className="mt-2 flex items-center space-x-4 text-sm text-gray-500">
                        <span>Created: {new Date(institution.createdAt).toLocaleDateString()}</span>
                        <span>Properties: {institution.propertyCount || 0}</span>
                        <span className={`px-2 py-1 rounded-full text-xs ${
                          institution.isActive ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'
                        }`}>
                          {institution.isActive ? 'Active' : 'Inactive'}
                        </span>
                      </div>
                    </div>
                    <div className="flex items-center space-x-2">
                      <button 
                        onClick={() => openSettingsModal(institution)}
                        className="p-2 text-gray-500 hover:text-blue-600"
                        title="Institution Settings"
                      >
                        <Settings size={16} />
                      </button>
                      <button 
                        onClick={() => setEditingInstitution(institution)}
                        className="p-2 text-gray-500 hover:text-green-600"
                        title="Edit Institution"
                      >
                        <Edit size={16} />
                      </button>
                      <button 
                        onClick={() => deleteInstitution(institution.id)}
                        className="p-2 text-gray-500 hover:text-red-600"
                        title="Delete Institution"
                      >
                        <Trash2 size={16} />
                      </button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default InstitutionManager;
