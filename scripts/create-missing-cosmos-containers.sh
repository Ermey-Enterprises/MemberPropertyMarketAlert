#!/bin/bash

# CosmosDB Container Creation Script (Azure CLI version)
# Creates missing Alerts and ScanLogs containers for the Member Property Alert application

set -e

# Default values
DATABASE_NAME="MemberPropertyAlert"
DRY_RUN=false

# Function to display usage
usage() {
    echo "Usage: $0 -g <resource-group> -a <cosmos-account> [-d <database-name>] [--dry-run]"
    echo ""
    echo "Options:"
    echo "  -g, --resource-group    Azure resource group name (required)"
    echo "  -a, --account          CosmosDB account name (required)"
    echo "  -d, --database         Database name (default: MemberPropertyAlert)"
    echo "  --dry-run              Show what would be created without actually creating"
    echo "  -h, --help             Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0 -g rg-member-property-alert-dev -a cosmos-member-property-alert-dev"
    echo "  $0 -g rg-member-property-alert-dev -a cosmos-member-property-alert-dev --dry-run"
    exit 1
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -g|--resource-group)
            RESOURCE_GROUP="$2"
            shift 2
            ;;
        -a|--account)
            COSMOS_ACCOUNT="$2"
            shift 2
            ;;
        -d|--database)
            DATABASE_NAME="$2"
            shift 2
            ;;
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        -h|--help)
            usage
            ;;
        *)
            echo "Unknown option: $1"
            usage
            ;;
    esac
done

# Validate required parameters
if [[ -z "$RESOURCE_GROUP" || -z "$COSMOS_ACCOUNT" ]]; then
    echo "Error: Resource group and CosmosDB account name are required"
    usage
fi

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
MAGENTA='\033[0;35m'
NC='\033[0m' # No Color

echo -e "${MAGENTA}=== CosmosDB Container Creation Script ===${NC}"
echo -e "Resource Group: ${RESOURCE_GROUP}"
echo -e "Cosmos Account: ${COSMOS_ACCOUNT}"
echo -e "Database: ${DATABASE_NAME}"
echo -e "Dry Run: ${DRY_RUN}"
echo ""

# Check if Azure CLI is installed and user is logged in
if ! command -v az &> /dev/null; then
    echo -e "${RED}Error: Azure CLI is not installed. Please install it first.${NC}"
    exit 1
fi

# Check if user is logged in
if ! az account show &> /dev/null; then
    echo -e "${YELLOW}Please log in to Azure...${NC}"
    az login
fi

SUBSCRIPTION=$(az account show --query name -o tsv)
echo -e "${GREEN}✓ Connected to Azure subscription: ${SUBSCRIPTION}${NC}"

# Check if CosmosDB account exists
echo -e "\n${YELLOW}Checking CosmosDB account...${NC}"
if ! az cosmosdb show --resource-group "$RESOURCE_GROUP" --name "$COSMOS_ACCOUNT" &> /dev/null; then
    echo -e "${RED}Error: CosmosDB account '$COSMOS_ACCOUNT' not found in resource group '$RESOURCE_GROUP'${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Found CosmosDB account: ${COSMOS_ACCOUNT}${NC}"

# Check if database exists
echo -e "\n${YELLOW}Checking database...${NC}"
if ! az cosmosdb sql database show --resource-group "$RESOURCE_GROUP" --account-name "$COSMOS_ACCOUNT" --name "$DATABASE_NAME" &> /dev/null; then
    echo -e "${RED}Error: Database '$DATABASE_NAME' not found in CosmosDB account '$COSMOS_ACCOUNT'${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Found database: ${DATABASE_NAME}${NC}"

# Function to check if container exists
container_exists() {
    local container_name="$1"
    az cosmosdb sql container show --resource-group "$RESOURCE_GROUP" --account-name "$COSMOS_ACCOUNT" --database-name "$DATABASE_NAME" --name "$container_name" &> /dev/null
}

# Function to create container
create_container() {
    local container_name="$1"
    local partition_key="$2"
    
    if [[ "$DRY_RUN" == "true" ]]; then
        echo -e "${CYAN}DRY RUN: Would create container '$container_name' with partition key '$partition_key'${NC}"
        return 0
    fi
    
    echo -e "${YELLOW}Creating container '$container_name' with partition key '$partition_key'...${NC}"
    
    if az cosmosdb sql container create \
        --resource-group "$RESOURCE_GROUP" \
        --account-name "$COSMOS_ACCOUNT" \
        --database-name "$DATABASE_NAME" \
        --name "$container_name" \
        --partition-key-path "$partition_key" \
        --throughput 400 &> /dev/null; then
        echo -e "${GREEN}✓ Successfully created container '$container_name'${NC}"
        return 0
    else
        echo -e "${RED}✗ Failed to create container '$container_name'${NC}"
        return 1
    fi
}

# List existing containers
echo -e "\n${YELLOW}Checking existing containers...${NC}"
existing_containers=$(az cosmosdb sql container list --resource-group "$RESOURCE_GROUP" --account-name "$COSMOS_ACCOUNT" --database-name "$DATABASE_NAME" --query "[].name" -o tsv 2>/dev/null || echo "")

if [[ -n "$existing_containers" ]]; then
    echo -e "Existing containers:"
    while IFS= read -r container; do
        if [[ -n "$container" ]]; then
            # Get partition key for the container
            partition_key=$(az cosmosdb sql container show --resource-group "$RESOURCE_GROUP" --account-name "$COSMOS_ACCOUNT" --database-name "$DATABASE_NAME" --name "$container" --query "resource.partitionKey.paths[0]" -o tsv 2>/dev/null || echo "unknown")
            echo -e "  - ${container} (partition: ${partition_key})"
        fi
    done <<< "$existing_containers"
else
    echo -e "No existing containers found."
fi

echo ""

# Define required containers
declare -A required_containers=(
    ["Institutions"]="/id"
    ["Addresses"]="/institutionId"
    ["Alerts"]="/institutionId"
    ["ScanLogs"]="/institutionId"
)

# Check which containers need to be created
containers_to_create=()
warnings=()

for container_name in "${!required_containers[@]}"; do
    partition_key="${required_containers[$container_name]}"
    
    if container_exists "$container_name"; then
        echo -e "${GREEN}✓ Container exists: ${container_name}${NC}"
    else
        echo -e "${RED}⚠ Missing container: ${container_name}${NC}"
        containers_to_create+=("$container_name:$partition_key")
    fi
done

# Check for legacy container names
if container_exists "MemberAddresses"; then
    warnings+=("Found legacy container 'MemberAddresses'. You may need to rename it to 'Addresses' or update your application configuration.")
fi

if container_exists "PropertyAlerts"; then
    warnings+=("Found legacy container 'PropertyAlerts'. You may need to rename it to 'Alerts' or update your application configuration.")
fi

# Display warnings
if [[ ${#warnings[@]} -gt 0 ]]; then
    echo -e "\n${YELLOW}Warnings:${NC}"
    for warning in "${warnings[@]}"; do
        echo -e "${YELLOW}⚠ ${warning}${NC}"
    done
fi

# Create missing containers
if [[ ${#containers_to_create[@]} -eq 0 ]]; then
    echo -e "\n${GREEN}✓ All required containers already exist!${NC}"
else
    echo -e "\n${YELLOW}Creating missing containers...${NC}"
    
    success_count=0
    for container_info in "${containers_to_create[@]}"; do
        IFS=':' read -r container_name partition_key <<< "$container_info"
        
        if create_container "$container_name" "$partition_key"; then
            ((success_count++))
        fi
    done
    
    if [[ "$DRY_RUN" == "false" ]]; then
        echo -e "\n${GREEN}✓ Successfully created ${success_count}/${#containers_to_create[@]} containers${NC}"
    fi
fi

# Summary
echo -e "\n${MAGENTA}=== Summary ===${NC}"
if [[ "$DRY_RUN" == "true" ]]; then
    echo -e "${CYAN}Dry run completed. ${#containers_to_create[@]} containers would be created.${NC}"
else
    echo -e "${GREEN}Script completed. ${#containers_to_create[@]} containers were processed.${NC}"
fi

if [[ ${#warnings[@]} -gt 0 ]]; then
    echo -e "\n${YELLOW}Please review the warnings above and consider updating your infrastructure or application configuration.${NC}"
fi

echo -e "\nNext steps:"
echo -e "1. Verify containers were created correctly in the Azure portal"
echo -e "2. Test your application to ensure it can connect to all containers"
echo -e "3. Consider updating your Bicep infrastructure to match the current state"
