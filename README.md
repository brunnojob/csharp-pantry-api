# csharp-pantry-api

ASP.NET Core minimal API for pantry stock and expiry alerts. Endpoints: `GET /health`, `GET /items`, `POST /items`, `PATCH /items/{id}/quantity`, and `GET /alerts/expiring?days=7`.

Requires .NET 8 SDK. Run `dotnet run`; data is held in memory for this starter build. Use a database provider before relying on it across restarts or multiple instances.

Project by [Brunno Dev](https://brunnodev.store).