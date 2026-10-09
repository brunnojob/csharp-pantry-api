# Pantry API

API de estoque doméstico com validade, localização, movimentos auditados, idempotência e controle otimista de revisão.

## Executar

Requisitos: ASP.NET Core 8 e Supabase.

```sh
dotnet restore
dotnet build
dotnet run
```

## Funcionamento

Configure `SUPABASE_URL` e `SUPABASE_PUBLISHABLE_KEY`. Envie `Authorization: Bearer <sessão>`. Rotas: `/health`, `/items`, `/items/{id}/quantity`, `/alerts/expiring` e `/movements`. Ajustes passam pela função transacional `bd_adjust_pantry`; cada usuário acessa seu estoque.

## Persistência de resultados

O arquivo de operações está em [vercel-home-telemetry-api.vercel.app](https://vercel-home-telemetry-api.vercel.app/laboratory.html?project=csharp-pantry-api). As migrações Supabase estão no [repositório da API](https://github.com/brunnojob/vercel-home-telemetry-api/tree/main/supabase/migrations).

```sh
python cloud/sync.py enqueue resultado.json --project csharp-pantry-api
python cloud/sync.py sync
```

Defina `BRUNNODEV_ACCESS_TOKEN` com sua sessão. A fila SQLite conserva os relatórios até confirmação do servidor; o mesmo conteúdo não gera registros duplicados. Tokens não são gravados no código.
