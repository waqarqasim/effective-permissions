namespace EffectivePermissions.AspNetCore.Tests;

internal static class Depot
{
    public static ScopeTree Scopes { get; } = new(
    [
        new ScopeNode("acme", "business", null),
        new ScopeNode("north", "region", "acme"),
        new ScopeNode("leeds", "warehouse", "north"),
        new ScopeNode("york", "warehouse", "north"),
    ]);

    public static PermissionCatalog Catalog { get; } = new(
    [
        new PermissionDefinition("orders.access", "Orders"),
        new PermissionDefinition("orders.read", "Orders", "orders.access"),
        new PermissionDefinition("orders.approve", "Orders", "orders.read"),
    ]);

    public static PermissionState StateFor(string scopeId, params Grant[] grants) =>
        new(EffectivePermissionSet.Build(Catalog, Scopes, grants), scopeId);
}
