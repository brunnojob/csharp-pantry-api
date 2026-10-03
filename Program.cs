using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var items = new ConcurrentDictionary<Guid, PantryItem>();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/items", () => Results.Ok(items.Values.OrderBy(x => x.ExpiresOn)));
app.MapPost("/items", (CreateItem input) => {
    if (string.IsNullOrWhiteSpace(input.Name) || input.Quantity < 0 || input.ExpiresOn < DateOnly.FromDateTime(DateTime.UtcNow))
        return Results.BadRequest(new { error = "name, quantity or expiry date is invalid" });
    var item = new PantryItem(Guid.NewGuid(), input.Name.Trim(), input.Quantity, input.ExpiresOn);
    items[item.Id] = item;
    return Results.Created($"/items/{item.Id}", item);
});
app.MapPatch("/items/{id:guid}/quantity", (Guid id, QuantityChange change) => {
    if (!items.TryGetValue(id, out var item)) return Results.NotFound();
    var quantity = item.Quantity + change.Delta;
    if (quantity < 0) return Results.Conflict(new { error = "stock cannot be negative" });
    var updated = item with { Quantity = quantity };
    items[id] = updated;
    return Results.Ok(updated);
});
app.MapGet("/alerts/expiring", (int? days) => {
    var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(Math.Clamp(days ?? 7, 0, 90));
    return Results.Ok(items.Values.Where(x => x.ExpiresOn <= cutoff || x.Quantity <= 1));
});
app.Run();

record PantryItem(Guid Id, string Name, int Quantity, DateOnly ExpiresOn);
record CreateItem(string Name, int Quantity, DateOnly ExpiresOn);
record QuantityChange(int Delta);