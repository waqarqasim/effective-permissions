namespace EffectivePermissions.Model;

/// <summary>One node in the scope hierarchy: a business, a branch, a warehouse, a team.</summary>
public sealed record ScopeNode(string Id, string Kind, string? ParentId);

/// <summary>
/// The hierarchy grants are made against.
/// </summary>
/// <remarks>
/// A grant made at a node applies to that node and everything beneath it, which is what
/// makes "regional manager for the north" expressible as one row rather than as a grant per
/// warehouse that someone has to remember to extend when a warehouse opens.
/// </remarks>
public sealed class ScopeTree
{
    private readonly Dictionary<string, ScopeNode> _nodes;
    private readonly Dictionary<string, List<string>> _children;

    public ScopeTree(IEnumerable<ScopeNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        _nodes = new Dictionary<string, ScopeNode>(StringComparer.Ordinal);
        _children = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var node in nodes)
        {
            if (!_nodes.TryAdd(node.Id, node))
            {
                throw new ArgumentException($"Scope '{node.Id}' is declared more than once.", nameof(nodes));
            }
        }

        foreach (var node in _nodes.Values)
        {
            if (node.ParentId is null)
            {
                continue;
            }

            if (!_nodes.ContainsKey(node.ParentId))
            {
                throw new ArgumentException(
                    $"Scope '{node.Id}' names parent '{node.ParentId}', which does not exist. "
                    + "A grant made at a missing parent silently applies to nothing.",
                    nameof(nodes));
            }

            if (!_children.TryGetValue(node.ParentId, out var siblings))
            {
                siblings = [];
                _children[node.ParentId] = siblings;
            }

            siblings.Add(node.Id);
        }

        DetectCycles();
    }

    public IReadOnlyCollection<ScopeNode> Nodes => _nodes.Values;

    public bool Contains(string scopeId) => _nodes.ContainsKey(scopeId);

    public ScopeNode this[string scopeId] => _nodes.TryGetValue(scopeId, out var node)
        ? node
        : throw new KeyNotFoundException($"No scope '{scopeId}'.");

    /// <summary>
    /// The node itself, then its parent, then its parent's parent — nearest first.
    /// </summary>
    /// <remarks>
    /// Order matters to evaluation: the nearest grant wins, so that a deny placed on one
    /// warehouse can carve an exception out of an allow held over the whole region.
    /// </remarks>
    public IReadOnlyList<ScopeNode> SelfAndAncestors(string scopeId)
    {
        var chain = new List<ScopeNode>();

        for (var current = this[scopeId]; ; )
        {
            chain.Add(current);

            if (current.ParentId is null)
            {
                break;
            }

            current = this[current.ParentId];
        }

        return chain;
    }

    /// <summary>The node and everything beneath it.</summary>
    public IReadOnlyList<ScopeNode> SelfAndDescendants(string scopeId)
    {
        var results = new List<ScopeNode>();
        var pending = new Queue<string>();
        pending.Enqueue(this[scopeId].Id);

        while (pending.Count > 0)
        {
            var id = pending.Dequeue();
            results.Add(_nodes[id]);

            if (_children.TryGetValue(id, out var children))
            {
                foreach (var child in children)
                {
                    pending.Enqueue(child);
                }
            }
        }

        return results;
    }

    /// <summary>How far a node sits from the root. Used to decide which grant is nearer.</summary>
    public int DepthOf(string scopeId) => SelfAndAncestors(scopeId).Count - 1;

    private void DetectCycles()
    {
        foreach (var start in _nodes.Values)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (var current = start; current.ParentId is not null; current = _nodes[current.ParentId])
            {
                if (!seen.Add(current.Id))
                {
                    throw new ArgumentException(
                        $"Scope hierarchy contains a cycle through '{current.Id}'. "
                        + "Walking ancestors would not terminate.");
                }
            }
        }
    }
}
