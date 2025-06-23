<!-- Use this file to provide workspace-specific custom instructions to Copilot. For more details, visit https://code.visualstudio.com/docs/copilot/copilot-customization#_use-a-githubcopilotinstructionsmd-file -->

**Project:** Member Property Market Alert - Azure-native property monitoring service for financial institutions

**Architecture:**
- Backend: Azure Functions (.NET 8 Isolated) with RESTful API
- Frontend: React SPA with modern JavaScript
- Database: Azure Cosmos DB (Serverless SQL API)
- Infrastructure: Azure Bicep templates for IaC
- Monitoring: Application Insights with comprehensive logging

**Development Guidelines:**
- Prioritize best practices for:
  - Azure Functions with .NET 8 isolated worker model
  - Azure Cosmos DB with proper partition key strategies
  - React functional components with hooks
  - RESTful API design with proper HTTP status codes
  - Comprehensive error handling and logging
  - Security with API key authentication
- Use dependency injection and async/await throughout backend code
- Maintain clean separation between Core business logic and Functions API layer
- All code should be production-ready with proper error handling
- Use PowerShell for Azure deployment scripts, not bash
- Follow Azure naming conventions for resources
- Implement responsive design for React components
- Use Application Insights for telemetry and monitoring

**Key Business Context:**
- Financial institutions monitor member properties for loan opportunities
- State-level scanning reduces API costs by 99%+ vs individual property monitoring
- Real-time alerts when member homes are listed for sale
- Admin dashboard for self-service management

**Testing Standards:**
- Unit tests for business logic in Core project
- Integration tests for Azure Functions
- React component testing with Jest/React Testing Library
- End-to-end health checks in deployment pipeline

**Security Requirements:**
- API key authentication for all endpoints
- No PII stored - use anonymous member IDs only
- All data encrypted at rest and in transit
- Secure Azure resource configuration
