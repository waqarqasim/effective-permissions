namespace EffectivePermissions.Model;

/// <summary>
/// Every permission the application declares.
/// </summary>
/// <remarks>
/// <para>
/// The catalogue is built from what modules <em>declare</em>, never from what the codebase
/// happens to reference. A <c>const string</c> sitting in a static class is a name someone
/// typed; it is not a permission until a module declares it here. Otherwise the set of
/// permissions in the system is whatever a grep returns that day, and a typo becomes a new
/// permission that nobody can ever hold.
/// </para>
/// <para>
/// Construction validates the graph, so an unknown or circular requirement is a startup
/// failure rather than a wrong answer at runtime.
/// </para>
/// </remarks>
public sealed class PermissionCatalog
{
    private readonly Dictionary<string, PermissionDefinition> _byName;

    public PermissionCatalog(IEnumerable<PermissionDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        _byName = new Dictionary<string, PermissionDefinition>(StringComparer.Ordinal);

        foreach (var definition in definitions)
        {
            if (!_byName.TryAdd(definition.Name, definition))
            {
                var existing = _byName[definition.Name];

                throw new ArgumentException(
                    $"Permission '{definition.Name}' is declared by both '{existing.Module}' "
                    + $"and '{definition.Module}'. A permission has exactly one owning module; "
                    + "two owners means neither can safely change it.",
                    nameof(definitions));
            }
        }

        ValidateRequirements();
        DetectCycles();
    }

    public IReadOnlyCollection<PermissionDefinition> All => _byName.Values;

    public bool Contains(string permission) => _byName.ContainsKey(permission);

    public PermissionDefinition this[string permission] =>
        _byName.TryGetValue(permission, out var definition)
            ? definition
            : throw new KeyNotFoundException(
                $"No permission '{permission}' is declared. If it is referenced by a page or "
                + "a policy, that reference is a name nobody can hold.");

    public IEnumerable<PermissionDefinition> InModule(string module) =>
        _byName.Values.Where(d => string.Equals(d.Module, module, StringComparison.Ordinal));

    private void ValidateRequirements()
    {
        foreach (var definition in _byName.Values)
        {
            foreach (var required in definition.Requires)
            {
                if (!_byName.ContainsKey(required))
                {
                    throw new ArgumentException(
                        $"Permission '{definition.Name}' requires '{required}', which is not "
                        + "declared. The closure would silently stop there, and the subject "
                        + "would hold a permission whose prerequisite can never be satisfied.");
                }
            }
        }
    }

    private void DetectCycles()
    {
        var state = new Dictionary<string, int>(StringComparer.Ordinal);   // 0 unvisited, 1 in progress, 2 done

        foreach (var definition in _byName.Values)
        {
            Visit(definition.Name, []);
        }

        void Visit(string name, List<string> path)
        {
            if (state.TryGetValue(name, out var current))
            {
                if (current == 1)
                {
                    throw new ArgumentException(
                        "Permission requirements contain a cycle: "
                        + string.Join(" -> ", path.Append(name))
                        + ". The closure would not terminate.");
                }

                if (current == 2)
                {
                    return;
                }
            }

            state[name] = 1;
            path.Add(name);

            foreach (var required in _byName[name].Requires)
            {
                Visit(required, path);
            }

            path.RemoveAt(path.Count - 1);
            state[name] = 2;
        }
    }
}
