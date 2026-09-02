using EffectivePermissions.Closure;
using EffectivePermissions.Model;

namespace EffectivePermissions.Evaluation;

/// <summary>
/// What a subject can actually do, computed once from what they were granted.
/// </summary>
/// <remarks>
/// <para>
/// The distinction this type exists to enforce: <b>granted is not effective.</b> A grant is
/// a row someone wrote. An effective permission is what survives the requirement closure,
/// the scope hierarchy, and any deny that overrides it. Every surface that decides whether
/// to render a button, allow a route, or run a command must consult the second — and the
/// commonest bug in an authorisation layer is a surface that consults the first because it
/// is right there and looks equivalent.
/// </para>
/// <para>
/// It is immutable. A set that can be mutated after construction is a set some component
/// will read before it is finished being built.
/// </para>
/// </remarks>
public sealed class EffectivePermissionSet
{
    private readonly ScopeTree _scopes;
    private readonly Dictionary<string, Dictionary<string, Grant>> _byPermissionThenScope;

    private EffectivePermissionSet(
        ScopeTree scopes,
        Dictionary<string, Dictionary<string, Grant>> byPermissionThenScope,
        IReadOnlyList<Grant> granted,
        IReadOnlyList<Grant> ignored)
    {
        _scopes = scopes;
        _byPermissionThenScope = byPermissionThenScope;
        Granted = granted;
        Ignored = ignored;
    }

    /// <summary>The rows the subject actually has. Not the answer to any authorisation question.</summary>
    public IReadOnlyList<Grant> Granted { get; }

    /// <summary>
    /// Grants naming a permission the catalogue does not declare. They are skipped rather
    /// than thrown on — grant rows outlive the releases that declared them — but they are
    /// reported, because a permanently ignored grant means somebody believes a subject has
    /// access they do not have.
    /// </summary>
    public IReadOnlyList<Grant> Ignored { get; }

    public static EffectivePermissionSet Build(
        PermissionCatalog catalog,
        ScopeTree scopes,
        IEnumerable<Grant> grants)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(grants);

        var granted = grants.ToArray();
        var ignored = new List<Grant>();
        var map = new Dictionary<string, Dictionary<string, Grant>>(StringComparer.Ordinal);

        foreach (var grant in granted)
        {
            if (!catalog.Contains(grant.Permission))
            {
                ignored.Add(grant);
                continue;
            }

            if (!scopes.Contains(grant.ScopeId))
            {
                ignored.Add(grant);
                continue;
            }

            // Allow expands forward: holding a permission means holding what it requires.
            // Deny expands backward: withholding a permission withholds everything that
            // needs it. See DependencyClosure for why the direction differs.
            var expanded = grant.Kind == GrantKind.Allow
                ? DependencyClosure.Forward(catalog, [grant.Permission])
                : DependencyClosure.Backward(catalog, [grant.Permission]);

            foreach (var permission in expanded)
            {
                if (!map.TryGetValue(permission, out var atScope))
                {
                    atScope = new Dictionary<string, Grant>(StringComparer.Ordinal);
                    map[permission] = atScope;
                }

                // Two grants for the same permission at the same node: the deny wins. A
                // subject holding both an allow and a deny at one scope is a contradiction
                // that has to resolve the safe way, not the last-writer way.
                if (atScope.TryGetValue(grant.ScopeId, out var existing)
                    && existing.Kind == GrantKind.Deny)
                {
                    continue;
                }

                atScope[grant.ScopeId] = grant;
            }
        }

        return new EffectivePermissionSet(scopes, map, granted, ignored);
    }

    /// <summary>Whether the subject may do <paramref name="permission"/> at <paramref name="scopeId"/>.</summary>
    public bool IsAllowed(string permission, string scopeId) =>
        Evaluate(permission, scopeId).IsAllowed;

    /// <summary>The decision, with the grant and scope that produced it.</summary>
    public PermissionDecision Evaluate(string permission, string scopeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);

        if (!_scopes.Contains(scopeId))
        {
            // Not "deny quietly": a question asked about a scope that does not exist is a
            // bug in the caller, and answering false would hide it behind a plausible
            // access-denied page.
            throw new KeyNotFoundException(
                $"Cannot evaluate '{permission}': there is no scope '{scopeId}'.");
        }

        if (!_byPermissionThenScope.TryGetValue(permission, out var atScope))
        {
            return new PermissionDecision(DecisionOutcome.NotGranted, permission, scopeId);
        }

        // Nearest first. The first ancestor holding any grant for this permission decides,
        // so a deny on one warehouse carves an exception out of an allow over the region,
        // and an allow on one warehouse carves one out of a deny.
        foreach (var node in _scopes.SelfAndAncestors(scopeId))
        {
            if (!atScope.TryGetValue(node.Id, out var grant))
            {
                continue;
            }

            return grant.Kind == GrantKind.Allow
                ? new PermissionDecision(
                    DecisionOutcome.Allowed, permission, scopeId, grant, node.Id)
                : new PermissionDecision(
                    DecisionOutcome.Denied, permission, scopeId, grant, node.Id,
                    Because: $"denied by a grant at '{node.Id}'");
        }

        return new PermissionDecision(DecisionOutcome.NotGranted, permission, scopeId);
    }

    /// <summary>
    /// Every permission effective at a scope. This is what a UI should be built from — never
    /// from <see cref="Granted"/>, which omits the closure and ignores denials.
    /// </summary>
    public IReadOnlySet<string> EffectiveAt(string scopeId)
    {
        var effective = new HashSet<string>(StringComparer.Ordinal);

        foreach (var permission in _byPermissionThenScope.Keys)
        {
            if (IsAllowed(permission, scopeId))
            {
                effective.Add(permission);
            }
        }

        return effective;
    }

    /// <summary>Scopes at which the subject holds a permission, for a scope picker.</summary>
    public IReadOnlyList<string> ScopesAllowing(string permission) =>
        _scopes.Nodes
            .Where(node => IsAllowed(permission, node.Id))
            .Select(node => node.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
