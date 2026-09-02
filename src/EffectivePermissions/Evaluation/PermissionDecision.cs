using EffectivePermissions.Model;

namespace EffectivePermissions.Evaluation;

public enum DecisionOutcome
{
    /// <summary>No grant of any kind applied. The default, and the safe one.</summary>
    NotGranted = 0,

    Allowed = 1,

    /// <summary>An explicit deny applied, or something the subject needs was denied.</summary>
    Denied = 2,
}

/// <summary>
/// The answer, plus why.
/// </summary>
/// <remarks>
/// The reason is not decoration. "Access denied" with no explanation is the single most
/// expensive support ticket an internal application produces, because the only people who
/// can answer it are the ones who can read the grant tables. Carrying the deciding grant
/// and the scope it was made at turns that ticket into a screenshot.
/// </remarks>
public sealed record PermissionDecision(
    DecisionOutcome Outcome,
    string Permission,
    string ScopeId,
    Grant? DecidingGrant = null,
    string? DecidingScopeId = null,
    string? Because = null)
{
    public bool IsAllowed => Outcome == DecisionOutcome.Allowed;

    public string Explain() => Outcome switch
    {
        DecisionOutcome.Allowed when DecidingGrant is not null =>
            $"'{Permission}' allowed at '{ScopeId}' by {DecidingGrant} (inherited from '{DecidingScopeId}').",

        DecisionOutcome.Allowed =>
            $"'{Permission}' allowed at '{ScopeId}'.",

        DecisionOutcome.Denied when Because is not null =>
            $"'{Permission}' denied at '{ScopeId}': {Because}",

        DecisionOutcome.Denied =>
            $"'{Permission}' explicitly denied at '{ScopeId}' by a grant at '{DecidingScopeId}'.",

        _ => $"'{Permission}' is not granted at '{ScopeId}' or any scope above it.",
    };

    public override string ToString() => Explain();
}
