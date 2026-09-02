namespace EffectivePermissions.Tests;

/// <summary>
/// The catalogue validates at construction, so a malformed permission graph is a startup
/// failure rather than a wrong answer under load.
/// </summary>
public sealed class CatalogTests
{
    [Fact]
    public void A_requirement_naming_an_undeclared_permission_is_refused()
    {
        var act = () => new PermissionCatalog(
        [
            new PermissionDefinition("orders.approve", "Orders", "orders.read"),
        ]);

        // The closure would otherwise stop quietly at the missing name, and the subject
        // would hold a permission whose prerequisite can never be satisfied by anyone.
        act.ShouldThrow<ArgumentException>().Message.ShouldContain("orders.read");
    }

    [Fact]
    public void Two_modules_declaring_the_same_permission_is_refused()
    {
        var act = () => new PermissionCatalog(
        [
            new PermissionDefinition("orders.read", "Orders"),
            new PermissionDefinition("orders.read", "Reporting"),
        ]);

        act.ShouldThrow<ArgumentException>().Message.ShouldContain("exactly one owning module");
    }

    [Fact]
    public void A_cycle_in_requirements_is_refused()
    {
        var act = () => new PermissionCatalog(
        [
            new PermissionDefinition("a", "M", "b"),
            new PermissionDefinition("b", "M", "c"),
            new PermissionDefinition("c", "M", "a"),
        ]);

        act.ShouldThrow<ArgumentException>().Message.ShouldContain("cycle");
    }

    [Fact]
    public void A_permission_requiring_itself_is_refused()
    {
        var act = () => new PermissionCatalog(
        [
            new PermissionDefinition("a", "M", "a"),
        ]);

        act.ShouldThrow<ArgumentException>().Message.ShouldContain("cycle");
    }

    [Fact]
    public void A_diamond_is_fine_and_is_not_a_cycle()
    {
        // approve and export both require read. Visiting read twice is not a cycle, and a
        // naive detector that flags any second visit rejects a perfectly ordinary catalogue.
        var catalog = new PermissionCatalog(
        [
            new PermissionDefinition("access", "M"),
            new PermissionDefinition("read", "M", "access"),
            new PermissionDefinition("approve", "M", "read"),
            new PermissionDefinition("export", "M", "read"),
            new PermissionDefinition("publish", "M", "approve", "export"),
        ]);

        DependencyClosure.Forward(catalog, ["publish"])
            .ShouldBe(["publish", "approve", "export", "read", "access"], ignoreOrder: true);
    }

    [Fact]
    public void Asking_for_an_undeclared_permission_says_what_is_wrong()
    {
        Should.Throw<KeyNotFoundException>(() => Depot.Catalog["orders.teleport"])
            .Message.ShouldContain("nobody can hold");
    }

    [Fact]
    public void Permissions_can_be_listed_by_module()
    {
        Depot.Catalog.InModule("Stock").Select(d => d.Name)
            .ShouldBe(["stock.access", "stock.read", "stock.adjust"], ignoreOrder: true);
    }
}

public sealed class DependencyClosureTests
{
    [Fact]
    public void Forward_closure_includes_the_permission_itself()
    {
        DependencyClosure.Forward(Depot.Catalog, ["orders.access"])
            .ShouldBe(["orders.access"]);
    }

    [Fact]
    public void Backward_closure_includes_everything_that_needs_it()
    {
        DependencyClosure.Backward(Depot.Catalog, ["orders.read"])
            .ShouldBe(
                ["orders.read", "orders.edit", "orders.approve", "orders.export"],
                ignoreOrder: true);
    }

    [Fact]
    public void Backward_closure_reaches_across_two_hops()
    {
        // access is required by read, which is required by approve/edit/export.
        DependencyClosure.Backward(Depot.Catalog, ["orders.access"]).Count.ShouldBe(5);
    }

    [Fact]
    public void An_undeclared_permission_is_skipped_rather_than_throwing()
    {
        // Grant rows outlive the release that declared the permission. Throwing here would
        // stop a subject authenticating because of a stale row.
        DependencyClosure.Forward(Depot.Catalog, ["orders.read", "gone"])
            .ShouldBe(["orders.read", "orders.access"], ignoreOrder: true);
    }

    [Fact]
    public void Closing_an_empty_set_yields_an_empty_set()
    {
        DependencyClosure.Forward(Depot.Catalog, []).ShouldBeEmpty();
        DependencyClosure.Backward(Depot.Catalog, []).ShouldBeEmpty();
    }
}
