# Home-lab identity configuration

EventBooking.SeedData supports idempotent Keycloak demo convergence. Supply Keycloak__BaseUrl, Keycloak__AdminUsername, Keycloak__AdminPassword and Keycloak__DemoPassword through the deployment environment. The realm owns roles; EventBooking owns appointment-type scope. Secrets must not be committed.

The complete container installation is introduced by Phase 6.

The API serves its discovery schema at /openapi/v1.json and its interactive
Swagger UI at /swagger. Both describe the deployed REST host.
