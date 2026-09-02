using EffectivePermissions.Evaluation;

namespace EffectivePermissions.AspNetCore;

/// <summary>
/// The current subject's effective permissions and the scope they are currently working in.
/// </summary>
/// <remarks>
/// <para>
/// Immutable, and constructed complete. The alternative — a holder with settable properties
/// that something populates after the container hands it out — is the shape of a well-known
/// failure: a scoped service captured before it is filled in answers "no" to everything, or
/// worse, keeps answering with a previous request's subject.
/// </para>
/// <para>
/// A revocation implemented as "mutate the holder" fails open for exactly as long as any
/// component holds a reference to the old one. Building a fresh state per request and never
/// mutating it removes the category.
/// </para>
/// </remarks>
public sealed class PermissionState(EffectivePermissionSet permissions, string currentScopeId)
{
    public EffectivePermissionSet Permissions { get; } =
        permissions ?? throw new ArgumentNullException(nameof(permissions));

    /// <summary>The scope the current screen is operating in — the branch, site or team selected.</summary>
    public string CurrentScopeId { get; } = string.IsNullOrWhiteSpace(currentScopeId)
        ? throw new ArgumentException("A scope is required.", nameof(currentScopeId))
        : currentScopeId;

    public bool IsAllowed(string permission) => Permissions.IsAllowed(permission, CurrentScopeId);

    public bool IsAllowed(string permission, string scopeId) =>
        Permissions.IsAllowed(permission, scopeId);

    public PermissionDecision Explain(string permission) =>
        Permissions.Evaluate(permission, CurrentScopeId);

    /// <summary>The same state viewed from a different scope.</summary>
    public PermissionState At(string scopeId) => new(Permissions, scopeId);
}
