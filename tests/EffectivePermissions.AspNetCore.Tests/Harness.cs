using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace EffectivePermissions.AspNetCore.Tests;

/// <summary>
/// Renders an arbitrary fragment, so tests can compose the real component tree — including
/// the cascade — rather than setting a cascading parameter directly, which the framework
/// (correctly) refuses.
/// </summary>
internal sealed class Harness : ComponentBase
{
    [Parameter]
    public RenderFragment? Body { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        builder.AddContent(0, Body);
}
