# Member Property Market Alert API Documentation

## Overview

The Member Property Market Alert service provides APIs for financial institutions to manage member addresses and receive notifications when those properties are listed for sale.

## Base URL

```
https://your-function-app.azurewebsites.net/api
```

## Authentication

All API endpoints require function-level authentication. Include the function key in the request:

- **Header**: `x-functions-key: YOUR_FUNCTION_KEY`
- **Query Parameter**: `?code=YOUR_FUNCTION_KEY`

## Endpoints

### Member Address Management

#### 1. Create Member Addresses (Bulk)

**POST** `/members/addresses/bulk`

Creates multiple member addresses in a single request.

**Request Body:**
```json
{
  "institutionId": "string",
  "addresses": [
    {
      "anonymousReferenceId": "string",
      "address": "string",
      "city": "string",
      "state": "string",
      "zipCode": "string"
    }
  ]
}
```

**Response:**
```json
{
  "totalProcessed": 0,
  "successCount": 0,
  "errorCount": 0,
  "errors": ["string"],
  "createdIds": ["string"]
}
```

**Status Codes:**
- `200 OK` - Success
- `400 Bad Request` - Invalid request data
- `500 Internal Server Error` - Server error

#### 2. Get Member Addresses

**GET** `/members/addresses/{institutionId}`

Retrieves all member addresses for a specific institution.

**Parameters:**
- `institutionId` (path) - The institution identifier

**Response:**
```json
[
  {
    "id": "string",
    "anonymousReferenceId": "string",
    "address": "string",
    "city": "string",
    "state": "string",
    "zipCode": "string",
    "createdDate": "2025-01-01T00:00:00Z",
    "isActive": true
  }
]
```

**Status Codes:**
- `200 OK` - Success
- `400 Bad Request` - Invalid institution ID
- `500 Internal Server Error` - Server error

#### 3. Create Single Member Address

**POST** `/members/addresses`

Creates a single member address.

**Request Body:**
```json
{
  "institutionId": "string",
  "anonymousReferenceId": "string",
  "address": "string",
  "city": "string",
  "state": "string",
  "zipCode": "string"
}
```

**Response:**
```json
{
  "id": "string",
  "anonymousReferenceId": "string",
  "address": "string",
  "city": "string",
  "state": "string",
  "zipCode": "string",
  "createdDate": "2025-01-01T00:00:00Z",
  "isActive": true
}
```

**Status Codes:**
- `201 Created` - Success
- `400 Bad Request` - Invalid request data
- `500 Internal Server Error` - Server error

#### 4. Update Member Address

**PUT** `/members/addresses/{id}`

Updates an existing member address.

**Parameters:**
- `id` (path) - The member address ID

**Request Body:**
```json
{
  "institutionId": "string",
  "address": "string",
  "city": "string",
  "state": "string",
  "zipCode": "string",
  "isActive": true
}
```

**Response:**
```json
{
  "id": "string",
  "anonymousReferenceId": "string",
  "address": "string",
  "city": "string",
  "state": "string",
  "zipCode": "string",
  "createdDate": "2025-01-01T00:00:00Z",
  "isActive": true
}
```

**Status Codes:**
- `200 OK` - Success
- `400 Bad Request` - Invalid request data
- `404 Not Found` - Address not found
- `500 Internal Server Error` - Server error

#### 5. Delete Member Address

**DELETE** `/members/addresses/{id}?institutionId={institutionId}`

Deletes a member address.

**Parameters:**
- `id` (path) - The member address ID
- `institutionId` (query) - The institution ID

**Response:**
- No content

**Status Codes:**
- `204 No Content` - Success
- `400 Bad Request` - Missing institution ID
- `500 Internal Server Error` - Server error

## Data Models

### MemberAddress

```json
{
  "id": "string",
  "institutionId": "string",
  "anonymousReferenceId": "string",
  "address": "string",
  "city": "string",
  "state": "string",
  "zipCode": "string",
  "normalizedAddress": "string",
  "latitude": 0.0,
  "longitude": 0.0,
  "createdDate": "2025-01-01T00:00:00Z",
  "updatedDate": "2025-01-01T00:00:00Z",
  "isActive": true
}
```

### PropertyListing

```json
{
  "id": "string",
  "mlsNumber": "string",
  "address": "string",
  "city": "string",
  "state": "string",
  "zipCode": "string",
  "normalizedAddress": "string",
  "latitude": 0.0,
  "longitude": 0.0,
  "price": 0.0,
  "listingDate": "2025-01-01T00:00:00Z",
  "status": "Active",
  "propertyType": "SingleFamily",
  "bedrooms": 0,
  "bathrooms": 0.0,
  "squareFeet": 0,
  "lotSize": 0.0,
  "yearBuilt": 0,
  "description": "string",
  "listingAgent": "string",
  "listingOffice": "string",
  "dataSource": "string",
  "sourceUrl": "string",
  "imageUrls": ["string"],
  "createdDate": "2025-01-01T00:00:00Z",
  "updatedDate": "2025-01-01T00:00:00Z"
}
```

### PropertyMatch

```json
{
  "id": "string",
  "memberAddressId": "string",
  "propertyListingId": "string",
  "institutionId": "string",
  "anonymousReferenceId": "string",
  "matchConfidence": "High",
  "matchScore": 95.0,
  "matchMethod": "ExactAddress",
  "memberAddress": "string",
  "listingAddress": "string",
  "listingPrice": 0.0,
  "listingDate": "2025-01-01T00:00:00Z",
  "propertyStatus": "Active",
  "notificationsSent": [],
  "isProcessed": false,
  "createdDate": "2025-01-01T00:00:00Z",
  "updatedDate": "2025-01-01T00:00:00Z"
}
```

## Enums

### PropertyStatus
- `Active`
- `Pending`
- `Sold`
- `Withdrawn`
- `Expired`
- `Cancelled`

### PropertyType
- `SingleFamily`
- `Townhouse`
- `Condominium`
- `Duplex`
- `Triplex`
- `Fourplex`
- `Manufactured`
- `Land`
- `Commercial`
- `Other`

### MatchConfidence
- `Low` (1)
- `Medium` (2)
- `High` (3)
- `Exact` (4)

### MatchMethod
- `ExactAddress`
- `NormalizedAddress`
- `FuzzyMatch`
- `GeographicProximity`

### NotificationType
- `Email`
- `Webhook`
- `SMS`
- `Dashboard`

## Error Handling

All endpoints return consistent error responses:

```json
{
  "error": "Error message description",
  "details": "Additional error details if available"
}
```

Common HTTP status codes:
- `400 Bad Request` - Invalid request data
- `401 Unauthorized` - Missing or invalid authentication
- `404 Not Found` - Resource not found
- `500 Internal Server Error` - Server error

## Rate Limiting

API requests are subject to rate limiting:
- **Bulk operations**: 10 requests per minute
- **Individual operations**: 100 requests per minute

## Examples

### Bulk Create Member Addresses

```bash
curl -X POST "https://your-function-app.azurewebsites.net/api/members/addresses/bulk" \
  -H "Content-Type: application/json" \
  -H "x-functions-key: YOUR_FUNCTION_KEY" \
  -d '{
    "institutionId": "credit-union-123",
    "addresses": [
      {
        "anonymousReferenceId": "member-001",
        "address": "123 Main St",
        "city": "Anytown",
        "state": "CA",
        "zipCode": "12345"
      },
      {
        "anonymousReferenceId": "member-002",
        "address": "456 Oak Ave",
        "city": "Somewhere",
        "state": "CA",
        "zipCode": "67890"
      }
    ]
  }'
```

### Get Member Addresses

```bash
curl -X GET "https://your-function-app.azurewebsites.net/api/members/addresses/credit-union-123" \
  -H "x-functions-key: YOUR_FUNCTION_KEY"
```

## Webhooks (Future Implementation)

The service will support webhook notifications for property matches:

**POST** `/webhooks/register`

Register a webhook endpoint to receive notifications when matches are found.

**Webhook Payload:**
```json
{
  "eventType": "PropertyMatch",
  "institutionId": "string",
  "matches": [
    {
      "anonymousReferenceId": "string",
      "memberAddress": "string",
      "listingAddress": "string",
      "listingPrice": 0.0,
      "listingDate": "2025-01-01T00:00:00Z",
      "matchConfidence": "High",
      "matchScore": 95.0
    }
  ],
  "timestamp": "2025-01-01T00:00:00Z"
}
