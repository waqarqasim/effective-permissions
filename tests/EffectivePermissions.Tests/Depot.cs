namespace EffectivePermissions.Tests;

/// <summary>
/// A small but realistically shaped world, shared by the tests.
///
///   acme                        (business)
///   ├── north                   (region)
///   │   ├── leeds               (warehouse)
///   │   └── york                (warehouse)
///   └── south                   (region)
///       └── bristol             (warehouse)
///
/// Two regions and three warehouses is the minimum that makes the interesting cases
/// distinguishable: one warehouse cannot show that a grant stops at a sibling, and one
/// region cannot show that it stops at a sibling region.
/// </summary>
internal static class Depot
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

    /// <summary>
    /// Requirements read downwards: approving needs reading, and reading needs the module.
    ///
    ///   orders.approve ─┐
    ///   orders.edit  ───┼─▶ orders.read ──▶ orders.access
    ///   orders.export ──┘
    /// </summary>
    public static PermissionCatalog Catalog { get; } = new(
    [
        new PermissionDefinition("orders.access", "Orders"),
        new PermissionDefinition("orders.read", "Orders", "orders.access"),
        new PermissionDefinition("orders.edit", "Orders", "orders.read"),
        new PermissionDefinition("orders.approve", "Orders", "orders.read"),
        new PermissionDefinition("orders.export", "Orders", "orders.read"),

        new PermissionDefinition("stock.access", "Stock"),
        new PermissionDefinition("stock.read", "Stock", "stock.access"),
        new PermissionDefinition("stock.adjust", "Stock", "stock.read"),
    ]);

    public static EffectivePermissionSet SetOf(params Grant[] grants) =>
        EffectivePermissionSet.Build(Catalog, Scopes, grants);
}
