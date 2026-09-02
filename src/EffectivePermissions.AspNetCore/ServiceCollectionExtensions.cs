using EffectivePermissions.AspNetCore.Authorization;
using EffectivePermissions.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EffectivePermissions.AspNetCore;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the permission catalogue, the scope hierarchy, and the authorization
    /// policy machinery.
    /// </summary>
    /// <remarks>
    /// The catalogue and the scope tree are singletons because they are immutable and
    /// describe the application rather than the request. The <em>state</em> — who the
    /// subject is and what they hold — is deliberately not registered here: it is per
    /// request, and supplying it is the host's job, because only the host knows how a
    /// subject's grants are loaded.
    /// </remarks>
    public static IServiceCollection AddEffectivePermissions(
        this IServiceCollection services,
        PermissionCatalog catalog,
        ScopeTree scopes)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(scopes);

        services.AddSingleton(catalog);
        services.AddSingleton(scopes);

        services.AddAuthorization();

        // Replaces the default provider, so that any perm: policy resolves without having
        // been registered by hand. See PermissionPolicyProvider for what happens otherwise.
        services.Replace(ServiceDescriptor.Singleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>());
        // SCOPED, not singleton. The handler depends on the per-request state accessor, and
        // a singleton capturing a scoped dependency keeps the FIRST request's accessor for
        // the lifetime of the process -- so every later request is authorised against
        // whoever happened to arrive first. It is the same class of bug as a mutable state
        // holder, arriving through service lifetimes instead.
        //
        // ASP.NET Core resolves authorization handlers from the request scope, so scoped is
        // both correct and free. DependencyInjectionTests pins it.
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.TryAddScoped<PermissionStateAccessor>();
        services.TryAddScoped<IPermissionStateSetter>(sp => sp.GetRequiredService<PermissionStateAccessor>());
        services.TryAddScoped<IPermissionStateAccessor>(sp => sp.GetRequiredService<PermissionStateAccessor>());

        return services;
    }
}
