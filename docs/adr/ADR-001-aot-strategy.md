# ADR-001: AOT and Trimming Strategy

**Status:** Accepted (v1.1.0)

## Context

The package advertises trimming/AOT friendliness, but the two dispatch modes
differ sharply. Builder mode (`MediatorBuilder`) wires handlers through generic
type parameters and uses no reflection. DI mode (`ServiceProviderMediator`)
closes wrapper types over the runtime request type via `MakeGenericType` +
`Activator.CreateInstance`, and the optional scanning overload reflects over
assembly types. None of this was annotated, so trim/AOT analyzers saw six IL
warnings, Native AOT consumers got no guidance, and the README claim was
stronger than the code.

## Options considered

1. **Typed registration API** — replace runtime-closed generics with
   generic registration methods. Rejected: breaks the existing
   `AddScoped<IRequestHandler<,>>` pattern without removing the reflective
   dispatch that remains for polymorphic sends.
2. **Annotate honestly now** — mark the reflective paths, keep behavior
   identical, verify the clean surface in CI. Chosen.
3. **Source generator** — generate the wrapper registrations at compile time so
   DI mode becomes AOT-clean. Right end state, too large for this release;
   targeted for 1.2.

## Decision

Annotate now (option 2). Builder mode is the supported AOT surface: the library
sets `IsAotCompatible` on net8.0+ targets, builds warning-free, and CI publishes
and runs a Native AOT smoke consumer against it. DI mode is marked
`[RequiresDynamicCode]` at both `AddMediator` overloads and the mediator
constructor and is documented as JIT-only. Scanning is additionally
`[RequiresUnreferencedCode]`. The source generator (option 3) is the planned
path to lift DI mode into the AOT-supported tier.

## Consequences

- Consumers publishing with Native AOT (or any project with the AOT analyzer
  enabled) see IL3050 at their `AddMediator()` call sites. This is intended:
  the warning names `MediatorBuilder` as the supported alternative and the
  source-generated registration as the successor.
- Trim-only consumers (`PublishTrimmed` without AOT analysis) see IL2026 only
  on the scanning overload; the plain `AddMediator()` overload carries
  `RequiresDynamicCode`, which trim analysis does not surface. Its dispatch is
  nonetheless expected to work under trimming: handler types are rooted by
  explicit DI registration, and dispatch inspects only `IRequest<>` metadata of
  live instances (see the IL2111 suppression justification). Guaranteed-clean
  trimming remains builder-mode territory.
- Builder-mode AOT support is enforced by CI, not just claimed.
- The `aot`/`trimming` package tags stay, backed by the builder-mode guarantee.
- DI-mode behavior is unchanged; nothing breaks for JIT deployments.
