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

## Optional report archive

Export a JSON report from the command above, then run `python cloud/sync.py enqueue result.json --project csharp-pantry-api` and `python cloud/sync.py sync`. Synchronization requires `BRUNNODEV_ACCESS_TOKEN` and the external operations API; the local outbox retains unacknowledged reports.

## License

Original source and documentation are MIT licensed; see [LICENSE](LICENSE). Third-party dependencies and media retain their respective terms. Maintained by [Brunno Dev](https://brunnodev.store).
