using Microsoft.AspNetCore.Authorization;

namespace EffectivePermissions.AspNetCore.Authorization;

/// <summary>Naming convention for permission-backed authorization policies.</summary>
public static class PermissionPolicy
{
    public const string Prefix = "perm:";

    /// <summary>The policy name for a permission, e.g. <c>perm:orders.approve</c>.</summary>
    public static string For(string permission) => Prefix + permission;

    /// <summary>The permission a policy name refers to, or <c>null</c> if it is not one of ours.</summary>
    public static string? PermissionOf(string policyName) =>
        policyName.StartsWith(Prefix, StringComparison.Ordinal)
            ? policyName[Prefix.Length..]
            : null;
}

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;

    public override string ToString() => $"Requires '{Permission}'";
}
