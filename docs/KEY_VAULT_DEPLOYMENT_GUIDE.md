# Key Vault-Based Deployment Guide

## Overview

This guide explains the new automated Key Vault-based secret management system for the Member Property Alert infrastructure. The system automatically populates Azure Key Vault with deployment secrets and transitions from GitHub Secrets to Key Vault-based deployments.

## 🎯 How It Works

### Two-Phase Deployment System

**Phase 1: Initial Deployment (GitHub Secrets Mode)**
- Uses GitHub Secrets to deploy infrastructure
- Bicep template automatically creates Key Vault secrets from GitHub Secret values
- All secrets are populated in Key Vault during deployment

**Phase 2: Key Vault Mode (Ongoing Deployments)**
- Workflow automatically detects existing Key Vault with secrets
- Retrieves all secrets from Key Vault for deployment
- GitHub Secrets become optional (used only for initial authentication)

## 🔧 Setup Instructions

### Step 1: Configure GitHub Secrets

Ensure these GitHub Secrets are configured in your repository:

**Required GitHub Secrets:**
- `AZURE_CLIENT_ID` - Service principal client ID
- `AZURE_TENANT_ID` - Azure tenant ID
- `AZURE_SUBSCRIPTION_ID` - Azure subscription ID
- `RENTCAST_API_KEY` - RentCast API key
- `ADMIN_API_KEY` - Admin API key for secure endpoints

### Step 2: Initial Deployment

Run the GitHub Actions workflow (either via push or manual trigger):

```bash
# The workflow will automatically:
# 1. Detect no existing Key Vault
# 2. Use GitHub Secrets mode
# 3. Deploy infrastructure including Key Vault
# 4. Populate Key Vault with all secrets automatically
```

### Step 3: Verify Key Vault Population

After deployment, check your Key Vault in Azure Portal:

**Expected Secrets in Key Vault:**
- `APPLICATION-INSIGHTS-CONNECTION-STRING` (auto-generated, for reference)
- `COSMOS-CONNECTION-STRING` (auto-generated, for reference)
- `STORAGE-CONNECTION-STRING` (auto-generated, for reference)
- `RENTCAST-API-KEY` (from GitHub Secret)
- `ADMIN-API-KEY` (from GitHub Secret)
- `AZURE-CLIENT-ID` (from GitHub Secret)
- `AZURE-TENANT-ID` (from GitHub Secret)
- `AZURE-SUBSCRIPTION-ID` (from GitHub Secret)

**Note:** Infrastructure connection strings (Application Insights, Cosmos DB, Storage) are stored in Key Vault for reference but the Function App uses direct connections for reliability.

### Step 4: Automatic Transition

On subsequent deployments, the workflow will:
1. Detect existing Key Vault
2. Retrieve secrets from Key Vault
3. Use Key Vault mode automatically
4. Deploy using retrieved secrets

## 🔄 Workflow Logic

### Automatic Mode Detection

```yaml
# Workflow automatically detects deployment mode:
if Key Vault exists AND has required secrets:
  use Key Vault mode
else:
  use GitHub Secrets mode (initial deployment)
```

### Key Vault Mode Requirements

For Key Vault mode to activate, these secrets must exist:
- `RENTCAST-API-KEY`
- `ADMIN-API-KEY`
- `AZURE-CLIENT-ID`

If any are missing, workflow falls back to GitHub Secrets mode.

## 🔐 Security Benefits

### Enhanced Security Posture

**Before (GitHub Secrets Only):**
- All secrets stored in GitHub
- Secrets visible in workflow logs (masked but present)
- Manual secret rotation required
- No audit trail for secret access

**After (Key Vault Integration):**
- Secrets centralized in Azure Key Vault
- Complete audit trail for all secret access
- Automatic secret rotation capabilities
- Secrets never visible in workflow logs
- Enterprise-grade secret management

### Access Control

**Key Vault RBAC:**
- Function App: Key Vault Secrets User role
- Web App: Key Vault Secrets User role
- GitHub Actions: Key Vault Secrets User role (via service principal)

## 📊 Deployment Modes Comparison

| Aspect | GitHub Secrets Mode | Key Vault Mode |
|--------|-------------------|----------------|
| **When Used** | Initial deployment | Ongoing deployments |
| **Secret Source** | GitHub repository secrets | Azure Key Vault |
| **Security Level** | Standard | Enterprise-grade |
| **Audit Trail** | GitHub only | Complete Azure audit |
| **Secret Rotation** | Manual | Automated capability |
| **Visibility** | Masked in logs | Never in logs |

## 🛠️ Manual Operations

### Force GitHub Secrets Mode

To force GitHub Secrets mode (bypass Key Vault detection):

1. Temporarily rename or delete Key Vault secrets
2. Run deployment
3. Workflow will detect missing secrets and use GitHub mode

### Update Secrets

**To update secrets in Key Vault:**

1. **Via Azure Portal:**
   - Navigate to Key Vault
   - Update secret values directly
   - Next deployment will use updated values

2. **Via Azure CLI:**
   ```bash
   az keyvault secret set --vault-name "kv-mpa-dev-eus2-xxxx" --name "RENTCAST-API-KEY" --value "new-api-key"
   ```

3. **Via GitHub Secrets (will update Key Vault):**
   - Update GitHub Secret
   - Delete corresponding Key Vault secret
   - Run deployment (will recreate Key Vault secret with new value)

## 🔍 Troubleshooting

### Common Issues

**1. Key Vault Mode Not Activating**
- **Cause**: Missing required secrets in Key Vault
- **Solution**: Check that `RENTCAST-API-KEY`, `ADMIN-API-KEY`, and `AZURE-CLIENT-ID` exist
- **Verification**: Workflow logs will show "falling back to GitHub secrets mode"

**2. Deployment Fails in Key Vault Mode**
- **Cause**: Service principal lacks Key Vault access
- **Solution**: Verify service principal has "Key Vault Secrets User" role
- **Check**: Azure Portal → Key Vault → Access Control (IAM)

**3. Secrets Not Updating**
- **Cause**: Key Vault secrets exist and aren't being overwritten
- **Solution**: Delete Key Vault secret to force recreation from GitHub Secret

### Debug Information

**Workflow logs show deployment mode:**
```
🔑 Key Vault found: kv-mpa-dev-eus2-1234 - attempting Key Vault mode
✅ Using Key Vault mode - secrets retrieved from kv-mpa-dev-eus2-1234
```

**Or for GitHub mode:**
```
🆕 No existing Key Vault found - using GitHub secrets mode for initial deployment
```

## 📋 Migration Checklist

### For Existing Deployments

- [ ] Verify all GitHub Secrets are configured
- [ ] Run initial deployment to create Key Vault
- [ ] Verify Key Vault contains all expected secrets
- [ ] Test subsequent deployment uses Key Vault mode
- [ ] Optionally remove GitHub Secrets (keep authentication ones)

### For New Deployments

- [ ] Configure GitHub Secrets
- [ ] Run deployment workflow
- [ ] Verify Key Vault creation and population
- [ ] Test Key Vault mode on second deployment

## 🎉 Benefits Realized

### Operational Excellence

1. **Automated Secret Management**: No manual Key Vault population required
2. **Seamless Transition**: Automatic mode detection and switching
3. **Zero Downtime**: Smooth migration from GitHub to Key Vault secrets
4. **Enterprise Security**: Centralized secret management with audit trails

### Developer Experience

1. **Simplified Setup**: Just configure GitHub Secrets once
2. **Automatic Population**: Key Vault secrets created automatically
3. **Transparent Operation**: Workflow handles mode switching automatically
4. **Easy Updates**: Multiple ways to update secrets as needed

## 🔮 Future Enhancements

### Potential Improvements

1. **Secret Rotation**: Automated secret rotation schedules
2. **Multi-Environment**: Cross-environment secret sharing
3. **Backup Secrets**: Automatic secret backup and recovery
4. **Monitoring**: Alerts for secret expiration and access

---

## ✅ Summary

The new Key Vault-based deployment system provides:

- **Automated secret population** during infrastructure deployment
- **Intelligent mode detection** for seamless operation
- **Enterprise-grade security** with complete audit trails
- **Zero manual configuration** of Key Vault secrets
- **Backward compatibility** with existing GitHub Secrets

Your deployment pipeline now automatically transitions from GitHub Secrets to Key Vault-based secret management, providing enhanced security while maintaining operational simplicity.
