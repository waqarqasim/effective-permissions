using EffectivePermissions.AspNetCore.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EffectivePermissions.AspNetCore.Tests;

/// <summary>
/// Render tests for <see cref="AuthorizedControl"/>.
/// </summary>
/// <remarks>
/// These are <b>permission-differential</b>: each renders the same markup under two
/// different permission states and asserts the outputs differ in the expected direction.
/// Asserting only that a permitted user sees the button would pass just as happily against
/// a component that renders unconditionally, which is the failure that matters.
/// </remarks>
public sealed class AuthorizedControlTests
{
    private const string ApproveButton = "<button>Approve</button>";

    [Fact]
    public async Task Content_renders_for_a_subject_holding_the_permission()
    {
        var html = await Render(Depot.StateFor("leeds", Grant.Allow("orders.approve", "leeds")));

        html.ShouldContain("Approve");
    }

    [Fact]
    public async Task Content_is_absent_for_a_subject_without_it()
    {
        var html = await Render(Depot.StateFor("leeds"));

        html.ShouldNotContain("Approve");
    }

    [Fact]
    public async Task The_two_differ_which_is_the_actual_assertion()
    {
        var allowed = await Render(Depot.StateFor("leeds", Grant.Allow("orders.approve", "leeds")));
        var denied = await Render(Depot.StateFor("leeds"));

        // A component that ignored its permission entirely would pass "renders for the
        // permitted user". Only the difference proves the permission is consulted at all.
        allowed.ShouldNotBe(denied);
    }

    [Fact]
    public async Task The_closure_is_honoured_at_render_time_too()
    {
        // Granted approve only; read is effective through the closure, so a control guarding
        // 'orders.read' must render. This is the UI half of "granted is not effective" — a
        // menu built from the grant rows would hide a page the subject can actually open.
        var html = await Render(
            Depot.StateFor("leeds", Grant.Allow("orders.approve", "leeds")),
            permission: "orders.read");

        html.ShouldContain("Approve");
    }

    [Fact]
    public async Task A_deny_lower_down_hides_the_control_there_and_only_there()
    {
        var state = Depot.StateFor(
            "leeds",
            Grant.Allow("orders.approve", "north"),
            Grant.Deny("orders.read", "york"));

        (await Render(state, scope: "leeds")).ShouldContain("Approve");

        // Denying the prerequisite at york removes approval there too.
        (await Render(state, scope: "york")).ShouldNotContain("Approve");
    }

    [Fact]
    public async Task Scope_can_be_overridden_per_control()
    {
        var state = Depot.StateFor("leeds", Grant.Allow("orders.approve", "leeds"));

        (await Render(state, scope: "leeds")).ShouldContain("Approve");
        (await Render(state, scope: "york")).ShouldNotContain("Approve");
    }

    [Fact]
    public async Task A_missing_cascade_throws_instead_of_rendering_nothing()
    {
        // The whole reason this component validates its cascade. Rendering nothing would
        // remove every guarded control in the application, for every user including
        // administrators, and report no error anywhere — so the bug arrives as "the approve
        // button disappeared" weeks later.
        var error = await Should.ThrowAsync<InvalidOperationException>(
            () => RenderWithoutCascade());

        error.Message.ShouldContain("found no cascading");
        error.Message.ShouldContain("CascadingValue");
    }

    [Fact]
    public async Task A_control_with_no_permission_named_throws()
    {
        var error = await Should.ThrowAsync<InvalidOperationException>(
            () => Render(Depot.StateFor("leeds"), permission: ""));

        // Neither defaulting to "render" nor to "hide" is safe: one is a hole, the other a
        // gap, and both are silent.
        error.Message.ShouldContain("requires a Permission");
    }

    [Fact]
    public async Task The_denied_fragment_renders_when_the_permission_is_missing()
    {
        var html = await Render(
            Depot.StateFor("leeds"),
            denied: builder => builder.AddMarkupContent(0, "<span>Ask a manager</span>"));

        html.ShouldContain("Ask a manager");
        html.ShouldNotContain("Approve");
    }

    private static Task<string> Render(
        PermissionState state,
        string permission = "orders.approve",
        string? scope = null,
        RenderFragment? denied = null) =>
        RenderTree(builder =>
        {
            builder.OpenComponent<CascadingValue<PermissionState>>(0);
            builder.AddComponentParameter(1, nameof(CascadingValue<PermissionState>.Value), state);
            builder.AddComponentParameter(2, nameof(CascadingValue<PermissionState>.ChildContent),
                (RenderFragment)(inner => Control(inner, permission, scope, denied)));
            builder.CloseComponent();
        });

    private static Task<string> RenderWithoutCascade() =>
        RenderTree(builder => Control(builder, "orders.approve", scope: null, denied: null));

    private static void Control(
        Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder,
        string permission,
        string? scope,
        RenderFragment? denied)
    {
        builder.OpenComponent<AuthorizedControl>(0);
        builder.AddComponentParameter(1, nameof(AuthorizedControl.Permission), permission);
        builder.AddComponentParameter(2, nameof(AuthorizedControl.Scope), scope);
        builder.AddComponentParameter(3, nameof(AuthorizedControl.ChildContent),
            (RenderFragment)(b => b.AddMarkupContent(0, ApproveButton)));

        if (denied is not null)
        {
            builder.AddComponentParameter(4, nameof(AuthorizedControl.Denied), denied);
        }

        builder.CloseComponent();
    }

    private static async Task<string> RenderTree(RenderFragment body)
    {
        await using var renderer = new HtmlRenderer(
            new ServiceCollection().BuildServiceProvider(),
            NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(Harness.Body)] = body });

            var output = await renderer.RenderComponentAsync<Harness>(parameters);
            return output.ToHtmlString();
        });
    }
}
