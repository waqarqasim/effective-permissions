namespace EffectivePermissions.Model;

public enum GrantKind
{
    Allow = 0,

    /// <summary>
    /// Withholds a permission at a scope, overriding an allow held at the same node or any
    /// node above it. Denies exist so that "everything in the region except this one
    /// warehouse" is one row rather than an enumeration that rots the moment a warehouse
    /// opens.
    /// </summary>
    Deny = 1,
}

/// <summary>A permission attached to a subject at a point in the scope hierarchy.</summary>
public sealed record Grant(string Permission, string ScopeId, GrantKind Kind = GrantKind.Allow)
{
    public static Grant Allow(string permission, string scopeId) =>
        new(permission, scopeId, GrantKind.Allow);

    public static Grant Deny(string permission, string scopeId) =>
        new(permission, scopeId, GrantKind.Deny);

    public override string ToString() => $"{Kind} {Permission} @ {ScopeId}";
}
