namespace EffectivePermissions.Tests;

/// <summary>
/// Denies, and the asymmetry that makes them correct.
/// </summary>
public sealed class DenyTests
{
    [Fact]
    public void A_deny_beneath_an_allow_carves_out_an_exception()
    {
        var subject = Depot.SetOf(
            Grant.Allow("orders.read", "north"),
            Grant.Deny("orders.read", "york"));

        subject.IsAllowed("orders.read", "leeds").ShouldBeTrue();
        subject.IsAllowed("orders.read", "york").ShouldBeFalse();
    }

    [Fact]
    public void An_allow_beneath_a_deny_carves_out_the_opposite_exception()
    {
        var subject = Depot.SetOf(
            Grant.Deny("orders.read", "north"),
            Grant.Allow("orders.read", "leeds"));

        // Nearest wins in both directions, or "deny always wins" makes it impossible to
        // grant an exception without restructuring the hierarchy.
        subject.IsAllowed("orders.read", "leeds").ShouldBeTrue();
        subject.IsAllowed("orders.read", "york").ShouldBeFalse();
    }

    [Fact]
    public void At_the_same_scope_a_deny_beats_an_allow()
    {
        var subject = Depot.SetOf(
            Grant.Allow("orders.read", "leeds"),
            Grant.Deny("orders.read", "leeds"));

        // A contradiction has to resolve the safe way rather than the last-written way,
        // which would make the answer depend on row order.
        subject.IsAllowed("orders.read", "leeds").ShouldBeFalse();
    }

    [Fact]
    public void The_order_grants_are_supplied_in_does_not_change_the_answer()
    {
        var one = Depot.SetOf(
            Grant.Allow("orders.read", "leeds"),
            Grant.Deny("orders.read", "leeds"));

        var other = Depot.SetOf(
            Grant.Deny("orders.read", "leeds"),
            Grant.Allow("orders.read", "leeds"));

        one.IsAllowed("orders.read", "leeds").ShouldBe(other.IsAllowed("orders.read", "leeds"));
    }

    [Fact]
    public void Denying_a_prerequisite_denies_everything_that_needs_it()
    {
        var subject = Depot.SetOf(
            Grant.Allow("orders.approve", "acme"),
            Grant.Deny("orders.read", "york"));

        // This is the asymmetry. Approving requires reading, so a subject who cannot read
        // orders at york must not be able to approve them there either. Without the
        // backward closure they can approve an order they are not allowed to see — and a
        // permissions grid would show that as perfectly reasonable.
        subject.IsAllowed("orders.read", "york").ShouldBeFalse();
        subject.IsAllowed("orders.approve", "york").ShouldBeFalse();

        // ...and it stops at york.
        subject.IsAllowed("orders.approve", "leeds").ShouldBeTrue();
    }

    [Fact]
    public void Denying_a_dependent_does_not_deny_its_prerequisite()
    {
        var subject = Depot.SetOf(
            Grant.Allow("orders.approve", "acme"),
            Grant.Deny("orders.approve", "york"));

        // Withdrawing approval at one site must not also withdraw the ability to look at
        // orders there. If deny expanded forwards, taking away one capability would take
        // away every capability underneath it.
        subject.IsAllowed("orders.approve", "york").ShouldBeFalse();
        subject.IsAllowed("orders.read", "york").ShouldBeTrue();
        subject.IsAllowed("orders.access", "york").ShouldBeTrue();
    }

    [Fact]
    public void A_denied_permission_is_absent_from_the_effective_set()
    {
        var subject = Depot.SetOf(
            Grant.Allow("orders.approve", "acme"),
            Grant.Deny("orders.read", "york"));

        var effective = subject.EffectiveAt("york");

        // The set a UI is built from has to already account for denials, or every consumer
        // has to remember to check separately, and one of them will not.
        effective.ShouldNotContain("orders.read");
        effective.ShouldNotContain("orders.approve");
        effective.ShouldContain("orders.access");
    }

    [Fact]
    public void A_decision_explains_which_grant_and_scope_produced_it()
    {
        var subject = Depot.SetOf(
            Grant.Allow("orders.read", "north"),
            Grant.Deny("orders.read", "york"));

        subject.Evaluate("orders.read", "leeds").Explain()
            .ShouldContain("inherited from 'north'");

        subject.Evaluate("orders.read", "york").Explain()
            .ShouldContain("denied by a grant at 'york'");

        subject.Evaluate("orders.read", "bristol").Explain()
            .ShouldContain("not granted");
    }
}
