using EffectivePermissions.AspNetCore;
using EffectivePermissions.AspNetCore.Authorization;
using EffectivePermissions.Evaluation;
using EffectivePermissions.Model;

// A deliberately tiny host that makes the model inspectable from a terminal. Pass a user
// with ?as=, a scope with ?at=, and see what they can do and why.
//
//   dotnet run --project samples/Depot
//   curl "http://localhost:5080/effective?as=riley&at=york"

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEffectivePermissions(Warehouse.Catalog, Warehouse.Scopes);

var app = builder.Build();

app.MapGet("/", () => Results.Json(new
{
    users = Warehouse.Grants.Keys,
    scopes = Warehouse.Scopes.Nodes.Select(n => n.Id),
    permissions = Warehouse.Catalog.All.Select(p => p.Name),
    try_this = "/effective?as=riley&at=york",
}));

// What a subject can actually do at a scope, and — for anything they cannot — why not.
app.MapGet("/effective", (string @as, string at) =>
{
    if (!Warehouse.Grants.TryGetValue(@as, out var grants))
    {
        return Results.NotFound(new { error = $"No user '{@as}'.", users = Warehouse.Grants.Keys });
    }

    if (!Warehouse.Scopes.Contains(at))
    {
        return Results.NotFound(new { error = $"No scope '{at}'." });
    }

    var set = EffectivePermissionSet.Build(Warehouse.Catalog, Warehouse.Scopes, grants);

    return Results.Json(new
    {
        user = @as,
        scope = at,

        // The two lists side by side are the point of the sample: the rows somebody wrote,
        // and what those rows actually amount to here.
        granted = grants.Select(g => g.ToString()),
        effective = set.EffectiveAt(at).Order(StringComparer.Ordinal),

        decisions = Warehouse.Catalog.All
            .Select(p => set.Evaluate(p.Name, at))
            .Select(d => new { permission = d.Permission, outcome = d.Outcome.ToString(), why = d.Explain() }),
    });
});

app.Run();

/// <summary>The world the sample runs in.</summary>
internal static class Warehouse
{
    public static ScopeTree Scopes { get; } = new(
    [
        new ScopeNode("acme", "business", null),
        new ScopeNode("north", "region", "acme"),
        new ScopeNode("south", "region", "acme"),
        new ScopeNode("leeds", "warehouse", "north"),
        new ScopeNode("york", "warehouse", "north"),
        new ScopeNode("bristol", "warehouse", "south"),
    ]);

    public static PermissionCatalog Catalog { get; } = new(
    [
        new PermissionDefinition("orders.access", "Orders"),
        new PermissionDefinition("orders.read", "Orders", "orders.access"),
        new PermissionDefinition("orders.edit", "Orders", "orders.read"),
        new PermissionDefinition("orders.approve", "Orders", "orders.read"),
        new PermissionDefinition("stock.access", "Stock"),
        new PermissionDefinition("stock.read", "Stock", "stock.access"),
        new PermissionDefinition("stock.adjust", "Stock", "stock.read"),
    ]);

    public static Dictionary<string, Grant[]> Grants { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        // One row. Three effective permissions, everywhere in the business.
        ["dana"] = [Grant.Allow("orders.approve", "acme")],

        // A regional manager with one site carved out. Denying the PREREQUISITE at york
        // removes approval there too — without that, riley could approve an order at york
        // that they are not allowed to look at.
        ["riley"] = [Grant.Allow("orders.approve", "north"), Grant.Deny("orders.read", "york")],

        // Withdrawing approval at one site leaves reading intact, because deny travels
        // backward through requirements and not forward.
        ["sam"] = [Grant.Allow("orders.approve", "north"), Grant.Deny("orders.approve", "york")],

        // Two modules, two scopes.
        ["kim"] = [Grant.Allow("stock.adjust", "leeds"), Grant.Allow("orders.read", "south")],

        // Nothing at all, so the "not granted" explanation has a subject.
        ["pat"] = [],
    };
}
