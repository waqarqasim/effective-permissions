using EffectivePermissions.Model;

namespace EffectivePermissions.Closure;

/// <summary>
/// Expands a set of permissions through the requirement graph.
/// </summary>
/// <remarks>
/// <para>
/// Allow and deny travel in <b>opposite directions</b>, and this asymmetry is the part that
/// is almost always got wrong.
/// </para>
/// <para>
/// <b>Allow goes forward.</b> Granting <c>orders.approve</c> also grants everything approval
/// requires — <c>orders.read</c>, and whatever that requires in turn. Without this, the
/// grant is nominally held and practically unusable, and the bug reads as "the approve page
/// is broken" rather than as a permissions problem.
/// </para>
/// <para>
/// <b>Deny goes backward.</b> Denying <c>orders.read</c> must also deny <c>orders.approve</c>,
/// because approval requires reading. Expanding a deny forwards instead — denying read
/// because you denied approve — would revoke far more than intended; and not expanding it
/// at all leaves a subject who can approve an order they are not allowed to see, which is
/// the more dangerous of the two and the one that looks fine in a permissions grid.
/// </para>
/// </remarks>
public static class DependencyClosure
{
    /// <summary>
    /// <paramref name="permissions"/> plus everything they require, transitively.
    /// </summary>
    public static IReadOnlySet<string> Forward(
        PermissionCatalog catalog,
        IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(permissions);

        var closed = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();

        foreach (var permission in permissions)
        {
            pending.Push(permission);
        }

        while (pending.Count > 0)
        {
            var permission = pending.Pop();

            // An undeclared permission is skipped rather than throwing: grants outlive the
            // code that declared them, and a permission removed in a release should not stop
            // every subject who still has the old row from authenticating. Reporting it is
            // the catalogue's job at startup.
            if (!catalog.Contains(permission) || !closed.Add(permission))
            {
                continue;
            }

            foreach (var required in catalog[permission].Requires)
            {
                pending.Push(required);
            }
        }

        return closed;
    }

    /// <summary>
    /// <paramref name="permissions"/> plus everything that requires them, transitively —
    /// the set that becomes unusable when these are withheld.
    /// </summary>
    public static IReadOnlySet<string> Backward(
        PermissionCatalog catalog,
        IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(permissions);

        var dependents = BuildReverseIndex(catalog);

        var closed = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();

        foreach (var permission in permissions)
        {
            pending.Push(permission);
        }

        while (pending.Count > 0)
        {
            var permission = pending.Pop();

            if (!closed.Add(permission))
            {
                continue;
            }

            if (!dependents.TryGetValue(permission, out var requiredBy))
            {
                continue;
            }

            foreach (var dependent in requiredBy)
            {
                pending.Push(dependent);
            }
        }

        return closed;
    }

    private static Dictionary<string, List<string>> BuildReverseIndex(PermissionCatalog catalog)
    {
        var index = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var definition in catalog.All)
        {
            foreach (var required in definition.Requires)
            {
                if (!index.TryGetValue(required, out var dependents))
                {
                    dependents = [];
                    index[required] = dependents;
                }

                dependents.Add(definition.Name);
            }
        }

        return index;
    }
}
