# EffectivePermissions

[![ci](https://github.com/waqarqasim/effective-permissions/actions/workflows/ci.yml/badge.svg)](https://github.com/waqarqasim/effective-permissions/actions/workflows/ci.yml)

Hierarchical, dependency-aware authorization for .NET: **what a subject can actually do —
not what was granted to them.**

```bash
git clone https://github.com/waqarqasim/effective-permissions
cd effective-permissions
dotnet test effective-permissions.slnx        # 60 tests
dotnet run --project samples/Depot            # then: /effective?as=riley&at=york
```

---

## Granted is not effective

A grant is a row someone wrote. An effective permission is what survives three things the
row does not mention:

1. **The requirement closure.** Approving an order is not standalone — the approval screen
   lists orders and opens one. A subject granted `orders.approve` alone gets a page that
   renders its header and lists nothing, and the bug arrives as *"the approve page is
   broken"*.
2. **The scope hierarchy.** A grant at a region applies to every warehouse under it, and to
   nothing beside it.
3. **Denials**, which override allows held further up.

Almost every authorization bug I have had to unpick came from a surface answering from the
first list because it was right there and looked equivalent.

```csharp
var subject = EffectivePermissionSet.Build(catalog, scopes,
    [Grant.Allow("orders.approve", "leeds")]);

subject.Granted.Count;                              // 1
subject.EffectiveAt("leeds");                       // orders.approve, orders.read, orders.access
subject.IsAllowed("orders.edit", "leeds");          // false — closure runs one way only
```

## The asymmetry that makes denials correct

This is the part that is usually wrong, and it is wrong in a way no permissions grid shows.

> **Allow travels forward. Deny travels backward.**

Granting `orders.approve` also grants what approval *requires* — `orders.read`. Denying
`orders.read` must also deny `orders.approve`, because approval requires reading.

Expand a deny forwards instead and withdrawing one capability silently withdraws everything
beneath it. Do not expand it at all and you get a subject who **can approve an order they
are not allowed to see** — which reads as perfectly reasonable in an admin screen.

The sample makes the contrast concrete. Two people, the same shape of grants, opposite
outcomes:

```
riley  Allow orders.approve @ north  +  Deny orders.read    @ york
sam    Allow orders.approve @ north  +  Deny orders.approve @ york
```

```
riley @ york   effective: orders.access
               'orders.approve' denied at 'york': denied by a grant at 'york'

sam   @ york   effective: orders.access, orders.read
               'orders.read'    allowed at 'york' by Allow orders.approve @ north
               'orders.approve' denied at 'york': denied by a grant at 'york'
```

Riley loses approval because their ability to read was withdrawn. Sam keeps reading because
only approval was withdrawn. Nearest scope wins in both directions, so a deny on one
warehouse carves an exception out of a region-wide allow — and an allow on one warehouse
carves one out of a region-wide deny.

## Every decision explains itself

```
'orders.approve' allowed at 'leeds' by Allow orders.approve @ north (inherited from 'north').
'orders.read'    denied  at 'york': denied by a grant at 'york'
'orders.read'    is not granted at 'leeds' or any scope above it.
```

"Access denied" with no reason is the most expensive ticket an internal application
produces, because the only people who can answer it are the ones who can read the grant
tables. Carrying the deciding grant and the scope it came from turns that into a screenshot.

## Four traps this is built around

### A missing cascade renders nothing, for everybody

`AuthorizedControl` reads its state from a cascading value. If that cascade is absent, the
obvious implementation renders **nothing** — every guarded button in the application
disappears, for every user including administrators, and nothing anywhere reports an error.
The reports that follow describe missing features, not missing configuration.

So it throws instead, naming the fix. A component that cannot answer its question must not
answer *no*.

### An unregistered policy is a 500, not a 403

A page decorated with `[Authorize(Policy = "perm:orders.approve")]` where nothing registered
that policy does not return 403. ASP.NET Core throws, and the page returns **500** — for
everyone, including the people who should be able to use it. Registering every permission by
hand is not tedium avoidance; it is a foot-gun.

`PermissionPolicyProvider` builds policies on demand from the catalogue, and still refuses a
permission the catalogue does not declare — because a typo must not quietly become a policy
nobody can ever satisfy.

### A singleton handler freezes the first user

```csharp
services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();   // not Singleton
```

The handler depends on the per-request state. Registered as a singleton it captures the
*first* request's accessor and authorises every later request against whoever arrived first.
No functional test notices, because each test uses one user.
[The test for it](tests/EffectivePermissions.AspNetCore.Tests/PolicyAndDependencyTests.cs)
asserts the handler cannot be resolved from the root container.

### A constant is not a declaration

A `const string` in a static class is a name someone typed. It is a permission only when a
module declares it in the catalogue. Otherwise the set of permissions in the system is
whatever a grep returns that day, and a typo becomes a permission nobody can hold.

The catalogue validates at construction: unknown requirements, requirement cycles, and two
modules claiming one permission are all startup failures.

## Verified, not asserted

60 tests, and the ones that matter were mutation-checked rather than trusted:

| mutation | tests killed |
| --- | --- |
| deny expands forward like allow | 3 |
| deny always wins regardless of scope depth | 1 |
| authorization handler registered as singleton | 2 |
| missing cascade renders nothing instead of throwing | 1 |
| **authorization handler always succeeds (fail-open)** | **4** |

That last row was added after the fact, and it is the most useful one here. The handler — the
code path that actually produces the 403 — originally had **no tests at all**, and replacing
its body with an unconditional `context.Succeed()` left the whole suite green. Every other test
covered the evaluation model or the Blazor control; nothing covered the step that turns a
decision into an HTTP status. The UI would still have hidden the button, so the application
would have looked correct while every guarded route stood open.

The render tests are **permission-differential** — they render the same markup under two
different permission states and assert the outputs differ. Asserting only that a permitted
user sees the button passes just as happily against a component that renders
unconditionally, which is the failure that matters.

## Layout

```
src/EffectivePermissions             catalogue, scope tree, closure, evaluation — no ASP.NET
src/EffectivePermissions.AspNetCore  policy provider, handler, AuthorizedControl
samples/Depot                        a tiny host: /effective?as=riley&at=york
tests/                               60 tests, including the render tests
```

The core has no framework dependency, so the model is usable from a worker, a console tool,
or a test without dragging in a web host.

## Requirements

.NET 10 SDK.

---

Generalised from the authorization layer of a multi-tenant ERP I architect solo. Nothing
client-specific is reproduced here. See also
[dotnet-multitenant-reference](https://github.com/waqarqasim/dotnet-multitenant-reference)
for the tenancy layer, and [ModuLint](https://github.com/waqarqasim/ModuLint) for an
architecture rule that checks pages are behind a gate rather than merely naming a permission.
