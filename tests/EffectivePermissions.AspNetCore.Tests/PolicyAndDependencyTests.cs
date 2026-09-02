using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace EffectivePermissions.AspNetCore.Tests;

public sealed class PolicyProviderTests
{
    [Fact]
    public async Task A_policy_for_a_declared_permission_resolves_without_being_registered()
    {
        var provider = Provider();

        var policy = await provider.GetPolicyAsync(PermissionPolicy.For("orders.approve"));

        // Registering every permission by hand is not merely tedious. A page decorated with
        // an unregistered policy does not 403 — ASP.NET Core throws, and the page returns
        // 500 for everyone, including the people who should be able to use it.
        policy.ShouldNotBeNull();
        policy.Requirements.OfType<PermissionRequirement>()
            .ShouldHaveSingleItem().Permission.ShouldBe("orders.approve");
    }

    [Fact]
    public async Task A_policy_naming_an_undeclared_permission_is_refused_loudly()
    {
        var provider = Provider();

        // A typo must not quietly become a policy nobody can satisfy. That is a page which
        // is unreachable forever and looks exactly like a deliberate restriction.
        var error = await Should.ThrowAsync<InvalidOperationException>(
            () => provider.GetPolicyAsync(PermissionPolicy.For("orders.aprove")));

        error.Message.ShouldContain("orders.aprove");
        error.Message.ShouldContain("no module declares");
    }

    [Fact]
    public async Task A_policy_that_is_not_ours_is_left_alone()
    {
        var provider = Provider();

        (await provider.GetPolicyAsync("SomeOtherPolicy")).ShouldBeNull();
    }

    [Fact]
    public async Task An_explicitly_registered_policy_still_wins()
    {
        var services = BaseServices();
        services.AddAuthorization(options =>
            options.AddPolicy(PermissionPolicy.For("orders.read"), builder => builder.RequireAssertion(_ => true)));

        var provider = services.BuildServiceProvider().GetRequiredService<IAuthorizationPolicyProvider>();

        var policy = await provider.GetPolicyAsync(PermissionPolicy.For("orders.read"));

        policy.ShouldNotBeNull();
        policy.Requirements.OfType<PermissionRequirement>().ShouldBeEmpty();
    }

    [Fact]
    public void Policy_names_round_trip()
    {
        PermissionPolicy.PermissionOf(PermissionPolicy.For("orders.approve")).ShouldBe("orders.approve");
        PermissionPolicy.PermissionOf("NotOurs").ShouldBeNull();
    }

    private static IAuthorizationPolicyProvider Provider() =>
        BaseServices().BuildServiceProvider().GetRequiredService<IAuthorizationPolicyProvider>();

    private static ServiceCollection BaseServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEffectivePermissions(Depot.Catalog, Depot.Scopes);
        return services;
    }
}

/// <summary>
/// Service lifetimes, which is where an authorization layer silently stops being per-user.
/// </summary>
public sealed class DependencyInjectionTests
{
    [Fact]
    public void The_authorization_handler_is_per_request_not_per_process()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEffectivePermissions(Depot.Catalog, Depot.Scopes);

        using var root = services.BuildServiceProvider(validateScopes: true);

        // The check this exists for. The handler depends on the per-request state accessor;
        // registered as a singleton it would capture the FIRST request's accessor and
        // authorise every later request against whoever arrived first — a cross-user leak
        // that no functional test notices, because every individual test uses one user.
        //
        // With scope validation on, resolving a scoped service from the root throws. A
        // handler that resolved successfully here would be a singleton, and that is exactly
        // what must not happen.
        Should.Throw<InvalidOperationException>(
            () => root.GetRequiredService<IAuthorizationHandler>());

        using var scope = root.CreateScope();
        scope.ServiceProvider.GetRequiredService<IAuthorizationHandler>()
            .ShouldBeOfType<PermissionAuthorizationHandler>();
    }

    [Fact]
    public void Each_request_scope_gets_its_own_handler_instance()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEffectivePermissions(Depot.Catalog, Depot.Scopes);

        using var root = services.BuildServiceProvider(validateScopes: true);
        using var first = root.CreateScope();
        using var second = root.CreateScope();

        first.ServiceProvider.GetRequiredService<IAuthorizationHandler>()
            .ShouldNotBeSameAs(second.ServiceProvider.GetRequiredService<IAuthorizationHandler>());
    }

    [Fact]
    public void Two_scopes_get_two_independent_states()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEffectivePermissions(Depot.Catalog, Depot.Scopes);

        using var root = services.BuildServiceProvider(validateScopes: true);

        using var first = root.CreateScope();
        using var second = root.CreateScope();

        first.ServiceProvider.GetRequiredService<IPermissionStateSetter>()
            .Set(Depot.StateFor("leeds", Grant.Allow("orders.approve", "leeds")));

        second.ServiceProvider.GetRequiredService<IPermissionStateSetter>()
            .Set(Depot.StateFor("leeds"));

        first.ServiceProvider.GetRequiredService<IPermissionStateAccessor>()
            .Current!.IsAllowed("orders.approve").ShouldBeTrue();

        second.ServiceProvider.GetRequiredService<IPermissionStateAccessor>()
            .Current!.IsAllowed("orders.approve").ShouldBeFalse();
    }

    [Fact]
    public void State_cannot_be_reassigned_within_a_request()
    {
        var accessor = new PermissionStateAccessor();
        accessor.Set(Depot.StateFor("leeds"));

        Should.Throw<InvalidOperationException>(
            () => accessor.Set(Depot.StateFor("york")));
    }

    [Fact]
    public void An_unset_accessor_reports_nothing_rather_than_an_empty_permission_set()
    {
        // Not "an empty set": an empty set is a legitimate answer for a subject with no
        // permissions, and conflating the two means "we never worked out who this is"
        // becomes indistinguishable from "this person may do nothing".
        new PermissionStateAccessor().Current.ShouldBeNull();
    }
}
