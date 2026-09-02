namespace EffectivePermissions.Tests;

/// <summary>
/// The distinction the library exists for. Every test here would pass trivially in an
/// implementation that answered from the grant rows, and every one of them describes a real
/// screen that would then be broken.
/// </summary>
public sealed class GrantedIsNotEffectiveTests
{
    [Fact]
    public void A_granted_permission_carries_everything_it_requires()
    {
        var subject = Depot.SetOf(Grant.Allow("orders.approve", "leeds"));

        // One row was written.
        subject.Granted.Count.ShouldBe(1);

        // Three permissions are effective. Without the closure, the approval page renders
        // its header and then fails to list a single order — a bug that reads as "the page
        // is broken", not as "the grant was incomplete".
        subject.IsAllowed("orders.approve", "leeds").ShouldBeTrue();
        subject.IsAllowed("orders.read", "leeds").ShouldBeTrue();
        subject.IsAllowed("orders.access", "leeds").ShouldBeTrue();

        subject.EffectiveAt("leeds").ShouldBe(
            ["orders.approve", "orders.read", "orders.access"],
            ignoreOrder: true);
    }

    [Fact]
    public void The_closure_does_not_run_the_other_way()
    {
        var subject = Depot.SetOf(Grant.Allow("orders.approve", "leeds"));

        // Approving implies reading. Reading does not imply editing or exporting, and an
        // implementation that expanded in both directions would hand every reader the
        // ability to approve.
        subject.IsAllowed("orders.edit", "leeds").ShouldBeFalse();
        subject.IsAllowed("orders.export", "leeds").ShouldBeFalse();
    }

    [Fact]
    public void Requirements_are_followed_all_the_way_down()
    {
        var subject = Depot.SetOf(Grant.Allow("orders.edit", "acme"));

        // edit -> read -> access. A closure that only went one hop is the version that
        // passes a two-level test fixture and fails on the real catalogue.
        subject.IsAllowed("orders.access", "bristol").ShouldBeTrue();
    }

    [Fact]
    public void Nothing_is_effective_without_a_grant()
    {
        var subject = Depot.SetOf();

        subject.IsAllowed("orders.read", "leeds").ShouldBeFalse();
        subject.EffectiveAt("leeds").ShouldBeEmpty();
        subject.Evaluate("orders.read", "leeds").Outcome.ShouldBe(DecisionOutcome.NotGranted);
    }

    [Fact]
    public void Permissions_from_one_module_do_not_leak_into_another()
    {
        var subject = Depot.SetOf(Grant.Allow("orders.approve", "acme"));

        subject.IsAllowed("stock.read", "acme").ShouldBeFalse();
    }

    [Fact]
    public void A_grant_for_a_permission_that_no_longer_exists_is_reported_not_silently_dropped()
    {
        var subject = Depot.SetOf(
            Grant.Allow("orders.read", "leeds"),
            Grant.Allow("orders.unpublish", "leeds"));   // removed in some release

        subject.IsAllowed("orders.unpublish", "leeds").ShouldBeFalse();

        // Silently ignoring it would leave an administrator looking at a grant screen that
        // shows access the subject does not have, with nothing anywhere saying so.
        subject.Ignored.ShouldHaveSingleItem().Permission.ShouldBe("orders.unpublish");
    }

    [Fact]
    public void A_grant_at_a_scope_that_no_longer_exists_is_reported_too()
    {
        var subject = Depot.SetOf(Grant.Allow("orders.read", "warehouse-that-closed"));

        subject.Ignored.ShouldHaveSingleItem().ScopeId.ShouldBe("warehouse-that-closed");
    }
}
