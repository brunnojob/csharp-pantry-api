# Pantry API

A household inventory API with expiry dates, locations, audited movements, idempotency, and optimistic revision control.

## Run

Requirements: ASP.NET Core 8 and Supabase.

```sh
dotnet restore
dotnet build
dotnet run
```

## Behavior

Set `SUPABASE_URL` and `SUPABASE_PUBLISHABLE_KEY`. Send `Authorization: Bearer <session>`. Routes: `/health`, `/items`, `/items/{id}/quantity`, `/alerts/expiring`, and `/movements`. Adjustments use the transactional `bd_adjust_pantry` function; each user accesses their own stock.

## Result synchronization

The [operations archive](https://vercel-home-telemetry-api.vercel.app/laboratory.html?project=csharp-pantry-api) stores execution results. Supabase migrations are in the [API repository](https://github.com/brunnojob/vercel-home-telemetry-api/tree/main/supabase/migrations).

```sh
python cloud/sync.py enqueue result.json --project csharp-pantry-api
python cloud/sync.py sync
```

Set `BRUNNODEV_ACCESS_TOKEN` to your session token. The SQLite outbox retains reports until the server confirms persistence; identical content does not create duplicate records. Tokens are not stored in source code. To run the synchronization tests:

```sh
python -m unittest discover -s cloud
```
