using System.Net.Http.Headers;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient("supabase", client => client.Timeout = TimeSpan.FromSeconds(10));
var app = builder.Build();
var databaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")?.TrimEnd('/');
var publishableKey = Environment.GetEnvironmentVariable("SUPABASE_PUBLISHABLE_KEY");

app.Use(async (context, next) => {
    context.Response.Headers.CacheControl = "no-store";
    if (context.Request.Path == "/health") { await next(); return; }
    if (string.IsNullOrEmpty(databaseUrl) || string.IsNullOrEmpty(publishableKey)) {
        context.Response.StatusCode = 503;
        await context.Response.WriteAsJsonAsync(new { error = "database_not_configured" }); return;
    }
    string authorization = context.Request.Headers.Authorization.ToString();
    if (!authorization.StartsWith("Bearer ", StringComparison.Ordinal) || authorization.Length > 8192 || !AuthenticationHeaderValue.TryParse(authorization, out var bearer) || string.IsNullOrWhiteSpace(bearer.Parameter) || bearer.Parameter.Any(char.IsWhiteSpace)) {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsJsonAsync(new { error = "supabase_session_required" }); return;
    }
    try {
        var client = context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("supabase");
        using var request = new HttpRequestMessage(HttpMethod.Get, databaseUrl + "/auth/v1/user");
        request.Headers.Add("apikey", publishableKey);
        request.Headers.Authorization = bearer;
        using var response = await client.SendAsync(request, context.RequestAborted);
        if (!response.IsSuccessStatusCode) {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "invalid_session" }); return;
        }
        using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.RequestAborted));
        if (user.RootElement.TryGetProperty("is_anonymous", out var anonymous) && anonymous.ValueKind == JsonValueKind.True) {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { error = "registered_account_required" }); return;
        }
        if (!user.RootElement.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || !Guid.TryParse(id.GetString(), out var owner)) {
            context.Response.StatusCode = 401; return;
        }
        context.Items["owner"] = owner;
        context.Items["token"] = authorization;
        await next();
    } catch (OperationCanceledException) { context.Response.StatusCode = 504; }
      catch (HttpRequestException) { context.Response.StatusCode = 502; }
      catch (JsonException) { context.Response.StatusCode = 502; }
});

async Task<IResult> Data(HttpContext context, HttpMethod method, string path, object? payload = null) {
    var client = context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("supabase");
    using var request = new HttpRequestMessage(method, databaseUrl + "/rest/v1/" + path);
    request.Headers.Add("apikey", publishableKey);
    request.Headers.Authorization = AuthenticationHeaderValue.Parse((string)context.Items["token"]!);
    request.Headers.Add("Prefer", "return=representation");
    if (payload is not null) request.Content = JsonContent.Create(payload);
    using var response = await client.SendAsync(request, context.RequestAborted);
    string content = await response.Content.ReadAsStringAsync(context.RequestAborted);
    if (!response.IsSuccessStatusCode) {
        using var error = JsonDocument.Parse(content);
        string? code = error.RootElement.TryGetProperty("code", out var value) ? value.GetString() : null;
        int status = code is "P0001" or "23505" ? 409 : code is "23514" or "22P02" ? 400 : code == "P0002" ? 404 : 502;
        return Results.Json(new { error = status == 409 ? "stock_or_revision_conflict" : "database_request_failed" }, statusCode: status);
    }
    return Results.Content(content, "application/json", statusCode: method == HttpMethod.Post && !path.StartsWith("rpc/") ? 201 : 200);
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", database = "supabase", configured = databaseUrl is not null && publishableKey is not null }));
app.MapGet("/items", (int? limit, int? offset, HttpContext context) => {
    if (limit is < 1 or > 500 || offset is < 0 or > 1000000)
        return Task.FromResult<IResult>(Results.BadRequest(new { error = "invalid_pagination" }));
    return Data(context, HttpMethod.Get, $"bd_pantry_items?order=expires_on.asc,id.asc&limit={limit ?? 100}&offset={offset ?? 0}");
});
app.MapPost("/items", (CreateItem input, HttpContext context) => {
    if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 120 || input.Quantity is < 0 or > 1000000 ||
        string.IsNullOrWhiteSpace(input.Location) || input.Location.Length > 80 || input.ExpiresOn < new DateOnly(1900, 1, 1))
        return Task.FromResult<IResult>(Results.BadRequest(new { error = "invalid_item" }));
    return Data(context, HttpMethod.Post, "bd_pantry_items", new { owner_id = context.Items["owner"], name = input.Name.Trim(),
        location = input.Location.Trim(), quantity = input.Quantity, expires_on = input.ExpiresOn });
});
app.MapPatch("/items/{id:guid}/quantity", (Guid id, QuantityChange input, HttpContext context) => {
    if (input.Delta == 0 || input.Delta is < -1000000 or > 1000000 || input.ExpectedRevision < 1 ||
        string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > 128)
        return Task.FromResult<IResult>(Results.BadRequest(new { error = "invalid_movement" }));
    return Data(context, HttpMethod.Post, "rpc/bd_adjust_pantry", new { p_item = id, p_delta = input.Delta,
        p_revision = input.ExpectedRevision, p_key = input.IdempotencyKey });
});
app.MapGet("/alerts/expiring", (int? days, HttpContext context) => {
    if (days is < 0 or > 90) return Task.FromResult<IResult>(Results.BadRequest(new { error = "days_must_be_0_to_90" }));
    var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(days ?? 7);
    return Data(context, HttpMethod.Get, $"bd_pantry_items?expires_on=lte.{cutoff:yyyy-MM-dd}&quantity=gt.0&order=expires_on.asc&limit=500");
});
app.MapGet("/items/{id:guid}/movements", (Guid id, HttpContext context) =>
    Data(context, HttpMethod.Get, $"bd_pantry_movements?item_id=eq.{id}&order=created_at.desc&limit=100"));
app.Run();

record CreateItem(string Name, int Quantity, DateOnly ExpiresOn, string Location = "pantry");
record QuantityChange(int Delta, long ExpectedRevision, string IdempotencyKey);
