using EffectivePermissions.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace EffectivePermissions.AspNetCore.Authorization;

/// <summary>
/// Supplies a policy for any declared permission, without each one having to be registered
/// by hand at startup.
/// </summary>
/// <remarks>
/// <para>
/// Registering policies one by one is not merely tedious. A page decorated with a policy
/// nobody registered does not return 403 — ASP.NET Core throws
/// <c>InvalidOperationException: The AuthorizationPolicy named ... was not found</c>, which
/// surfaces as a <b>500</b>. So a missing registration presents as a server fault rather
/// than as an access decision, and the page is unreachable for everyone including the people
/// who should be able to use it.
/// </para>
/// <para>
/// It is also the wrong way round for security: the failure is loud but uninformative, and
/// the temptation is to "fix" it by removing the attribute.
/// </para>
/// <para>
/// This provider builds the policy on demand from the catalogue instead, and — importantly —
/// still refuses a permission the catalogue does not declare. An unknown permission name is
/// a typo, and a typo must not silently become a policy that nobody can satisfy.
/// </para>
/// </remarks>
public sealed class PermissionPolicyProvider(
    IOptions<AuthorizationOptions> options,
    PermissionCatalog catalog) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // Anything explicitly registered wins, so a hand-written policy can still override.
        var registered = await base.GetPolicyAsync(policyName);

        if (registered is not null)
        {
            return registered;
        }

        if (PermissionPolicy.PermissionOf(policyName) is not { } permission)
        {
            return null;
        }

        if (!catalog.Contains(permission))
        {
            throw new InvalidOperationException(
                $"Policy '{policyName}' names permission '{permission}', which no module "
                + "declares. A page guarded by a permission that cannot be granted is "
                + "unreachable, and a mistyped one looks identical to a real restriction.");
        }

        return new AuthorizationPolicyBuilder()
            .AddRequirements(new PermissionRequirement(permission))
            .Build();
    }
}

/// <summary>Answers a <see cref="PermissionRequirement"/> from the current subject's state.</summary>
public sealed class PermissionAuthorizationHandler(IPermissionStateAccessor state)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var current = state.Current;

        // No state means nothing has established who the subject is. Not succeeding is the
        // only safe reading; the request then fails as unauthorised rather than authorising
        // against an empty set that happens to contain nothing.
        if (current is not null && current.IsAllowed(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
