# Liaison.Mediator – Project Direction

This document describes the long-term design direction of the Liaison.Mediator project.
It is **not a delivery schedule** and **not a list of promises**.

All items described here are subject to change based on real-world usage,
feedback, and alignment with the project’s core principles.

---

## Guiding Principles

The following principles are considered non-negotiable and guide all future decisions:

- Explicit over implicit
- Deterministic behavior by default
- Minimal and intentional surface area
- Trimming / AOT friendliness
- No reflection-heavy or scanning-based behavior in the default path
- Optional features must be opt-in and non-magical

If a potential feature conflicts with these principles, it is likely not a good fit
for this project.

---

## Areas Under Consideration

### 1. Abstractions Package

We are exploring the introduction of a separate
`Liaison.Mediator.Abstractions` package.

The goal of this package would be to provide a **stable, minimal set of interfaces**
(e.g. `IMediator`, request/notification contracts, handler interfaces)
without pulling in the full runtime implementation.

Potential motivations include:
- Allowing libraries and shared code to depend on mediator abstractions only
- Improving testability and mocking scenarios
- Enabling future evolution of the runtime implementation with clearer boundaries

The abstractions package is expected to remain intentionally small and evolve slowly.

---

### 2. Optional Behaviors Package

We are exploring an **opt-in behaviors model** provided via a separate package
(e.g. `Liaison.Mediator.Behaviors`).

Key constraints for this package:
- The core `Liaison.Mediator` package must not depend on it
- Behaviors must be registered explicitly and executed in a deterministic order
- No implicit discovery, assembly scanning, or DI-driven magic
- The default execution model remains behavior-free

The initial design focus is on **predictable before/after hooks**
(e.g. logging, metrics, validation, auditing),
rather than a fully implicit or reflection-driven pipeline model.

This package is considered advanced infrastructure and is not required for
typical usage.

---

### 3. Explicit Extension Points

Future evolution may include carefully scoped extension points such as:
- Invocation-level hooks for diagnostics or tracing
- Explicit publish strategies with well-defined semantics
- Registration-time validation to catch configuration errors early

Any extension point must preserve determinism and be clearly visible
at configuration time.

---

## Explicit Non-Goals

The following are considered out of scope by design:

- Full feature parity with MediatR
- Implicit or reflection-based pipelines by default
- Automatic retries, transactions, or ambient context
- Behavior ordering based on DI registration order
- Feature growth at the expense of predictability

Liaison.Mediator is intentionally opinionated and does not aim to be a
drop-in replacement for more feature-rich mediator frameworks.

---

## Compatibility & Versioning Philosophy

- Additive, opt-in features are preferred over breaking changes
- Behavioral changes result in major version increments
- Stable abstractions are expected to change rarely
- Experimental ideas may live in separate packages or namespaces

---

## Feedback

Feedback is welcome, especially when grounded in concrete use cases
and trade-off discussions.

If you want to influence the direction of the project:
- Open a GitHub Discussion
- Describe the problem you are solving
- Focus on constraints and trade-offs rather than feature parity
