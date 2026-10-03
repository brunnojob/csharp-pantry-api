using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Pantry") ?? "Data Source=pantry.db";
builder.Services.AddDbContext<PantryDb>(options => options.UseSqlite(connection));
var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<PantryDb>().Database.EnsureCreatedAsync();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/items", async (PantryDb db) =>
    Results.Ok(await db.Items.AsNoTracking().OrderBy(x => x.ExpiresOn).ToListAsync()));
app.MapPost("/items", async (CreateItem input, PantryDb db) => {
    if (string.IsNullOrWhiteSpace(input.Name) || input.Quantity < 0 ||
        input.ExpiresOn < DateOnly.FromDateTime(DateTime.UtcNow))
        return Results.BadRequest(new { error = "name, quantity or expiry date is invalid" });
    var item = new PantryItem { Name = input.Name.Trim(), Quantity = input.Quantity, ExpiresOn = input.ExpiresOn };
    db.Items.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/items/{item.Id}", item);
});
app.MapPatch("/items/{id:guid}/quantity", async (Guid id, QuantityChange change, PantryDb db) => {
    var item = await db.Items.FindAsync(id);
    if (item is null) return Results.NotFound();
    if (item.Quantity + change.Delta < 0) return Results.Conflict(new { error = "stock cannot be negative" });
    item.Quantity += change.Delta;
    await db.SaveChangesAsync();
    return Results.Ok(item);
});
app.MapGet("/alerts/expiring", async (int? days, PantryDb db) => {
    var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(Math.Clamp(days ?? 7, 0, 90));
    return Results.Ok(await db.Items.AsNoTracking().Where(x => x.ExpiresOn <= cutoff || x.Quantity <= 1).ToListAsync());
});
app.Run();

sealed class PantryDb(DbContextOptions<PantryDb> options) : DbContext(options) {
    public DbSet<PantryItem> Items => Set<PantryItem>();
}
sealed class PantryItem {
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public int Quantity { get; set; }
    public DateOnly ExpiresOn { get; set; }
}
record CreateItem(string Name, int Quantity, DateOnly ExpiresOn);
record QuantityChange(int Delta);