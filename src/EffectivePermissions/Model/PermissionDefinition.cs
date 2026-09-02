namespace EffectivePermissions.Model;

/// <summary>
/// A permission, and the permissions it cannot function without.
/// </summary>
/// <remarks>
/// <para>
/// <c>Requires</c> is the whole point of this library. Approving an order is not a
/// standalone capability: the approval screen lists orders, opens one, and shows its lines.
/// A subject granted <c>orders.approve</c> and nothing else gets a menu item that leads to a
/// page that 403s, or — worse — a page that renders with every panel empty and no
/// indication why.
/// </para>
/// <para>
/// Declaring the requirement here means the answer to "can they approve?" is computed from
/// the closure rather than from the grant, and the two are not the same set.
/// </para>
/// </remarks>
public sealed record PermissionDefinition
{
    public PermissionDefinition(string name, string module, params string[] requires)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(module);

        Name = name;
        Module = module;
        Requires = requires;
    }

    /// <summary>Stable identifier, e.g. <c>orders.approve</c>.</summary>
    public string Name { get; }

    /// <summary>The module that declares it. A module owns its permissions; nothing else may declare them.</summary>
    public string Module { get; }

    /// <summary>Permissions that must also be held for this one to be usable.</summary>
    public IReadOnlyList<string> Requires { get; }

    public override string ToString() => Name;
}
