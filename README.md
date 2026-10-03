# csharp-pantry-api

ASP.NET Core 8 minimal API with SQLite persistence for pantry stock and expiry alerts. Endpoints: `GET /health`, `GET /items`, `POST /items`, `PATCH /items/{id}/quantity`, and `GET /alerts/expiring?days=7`.

Requires .NET 8 SDK. Run `dotnet run`; SQLite creates `pantry.db` on first start. Set `ConnectionStrings__Pantry` to move the database. Keep the database out of source control.

Project by [Brunno Dev](https://brunnodev.store).