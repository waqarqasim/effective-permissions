namespace EffectivePermissions.AspNetCore;

/// <summary>Per-request access to the current subject's permission state.</summary>
public interface IPermissionStateAccessor
{
    /// <summary>The state for this request, or <c>null</c> before it has been established.</summary>
    PermissionState? Current { get; }
}

/// <summary>Write side, used once per request by whatever loads the subject's grants.</summary>
public interface IPermissionStateSetter : IPermissionStateAccessor
{
    void Set(PermissionState state);
}

/// <summary>
/// Scoped. Set once per request, never reassigned.
/// </summary>
/// <remarks>
/// Reassignment is refused for the same reason the state itself is immutable: a component
/// that resolved this accessor earlier in the request would start answering with a
/// different subject's permissions, and nothing would indicate that had happened.
/// </remarks>
public sealed class PermissionStateAccessor : IPermissionStateSetter
{
    private PermissionState? _state;

    public PermissionState? Current => _state;

    public void Set(PermissionState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (_state is not null)
        {
            throw new InvalidOperationException(
                "Permission state has already been established for this request. Use a new "
                + "scope to evaluate as a different subject.");
        }

        _state = state;
    }
}
