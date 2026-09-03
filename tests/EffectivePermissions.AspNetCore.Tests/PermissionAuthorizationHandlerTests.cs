using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace EffectivePermissions.AspNetCore.Tests;

/// <summary>
/// Tests for the handler that actually produces the 403.
/// </summary>
/// <remarks>
/// <para>
/// This file exists because it did not, and the gap was invisible. Every other test in the
/// suite exercised the evaluation model or the Blazor control; nothing touched the code path
/// that turns a decision into an HTTP status. Replacing the whole handler body with an
/// unconditional <c>context.Succeed(requirement)</c> left all 54 tests green.
/// </para>
/// <para>
/// That is the worst shape a hole can take: the UI still hides the button, because
/// <c>AuthorizedControl</c> is a separate path — so the application looks correct while every
/// guarded route is open to anyone authenticated. It surfaces in an audit, not in a bug report.
/// </para>
/// </remarks>
public sealed class PermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task Succeeds_for_a_subject_that_holds_the_permission()
    {
        var context = await Evaluate(
            "orders.approve",
            Depot.StateFor("leeds", Grant.Allow("orders.approve", "leeds")));

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Does_not_succeed_for_a_subject_without_it()
    {
        var context = await Evaluate("orders.approve", Depot.StateFor("leeds"));

        // The assertion that the fail-open mutation kills.
        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Does_not_succeed_when_no_state_was_established()
    {
        // Nothing resolved the subject. Authorising here would mean authorising against an
        // empty set that happens to contain nothing, which is indistinguishable from a
        // subject who legitimately holds nothing.
        var context = await Evaluate("orders.approve", state: null);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Honours_the_requirement_closure()
    {
        // Granted approve only; read is effective through the requirement graph, so a route
        // guarded on read must be reachable.
        var context = await Evaluate(
            "orders.read",
            Depot.StateFor("leeds", Grant.Allow("orders.approve", "leeds")));

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Honours_a_deny_on_a_prerequisite()
    {
        // Denying read at york must close the approve route there too, or a subject can
        // approve a record they are not allowed to see.
        var state = Depot.StateFor(
            "york",
            Grant.Allow("orders.approve", "north"),
            Grant.Deny("orders.read", "york"));

        var context = await Evaluate("orders.approve", state);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Succeeds_only_for_the_requirement_it_was_given()
    {
        // Two requirements on one policy: holding one must not satisfy the other.
        var state = Depot.StateFor("leeds", Grant.Allow("orders.read", "leeds"));

        var held = new PermissionRequirement("orders.read");
        var notHeld = new PermissionRequirement("orders.approve");

        var context = new AuthorizationHandlerContext(
            [held, notHeld], new ClaimsPrincipal(new ClaimsIdentity("test")), resource: null);

        await new PermissionAuthorizationHandler(new StubAccessor(state)).HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse("one unmet requirement leaves the policy unmet");
        context.PendingRequirements.ShouldContain(notHeld);
        context.PendingRequirements.ShouldNotContain(held);
    }

    /// <summary>Builds a context and runs the real handler over it.</summary>
    private static async Task<AuthorizationHandlerContext> Evaluate(
        string permission,
        PermissionState? state)
    {
        var context = new AuthorizationHandlerContext(
            [new PermissionRequirement(permission)],
            new ClaimsPrincipal(new ClaimsIdentity("test")),
            resource: null);

        await new PermissionAuthorizationHandler(new StubAccessor(state)).HandleAsync(context);

        return context;
    }

    private sealed class StubAccessor(PermissionState? current) : IPermissionStateAccessor
    {
        public PermissionState? Current { get; } = current;
    }
}
