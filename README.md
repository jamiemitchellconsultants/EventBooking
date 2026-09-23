# EventBooking

Booking system for events with multiple session types.

The REST discovery document is served at /api, the OpenAPI schema at
/openapi/v1.json and the interactive Swagger UI at /swagger. Staff endpoints
require an OIDC bearer token. The MCP host exposes /mcp with the same bearer
authentication and application authorization as REST.
