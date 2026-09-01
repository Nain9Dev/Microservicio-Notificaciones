# Project constitution — Microservicio Notificaciones

Microservicio de notificaciones por correo (SMTP, Mailtrap) guiado por eventos, utilizando .NET 10, MassTransit y RabbitMQ. Arquitectura Hexagonal / Domain-centric.

This file is the operational contract for any AI agent working on this repository.
Read `docs/README.md` before proposing anything.

## Scope of this file

**Where this file speaks, it governs this repository. Where it is silent, the operator's global rules apply.**

## Authority

Highest first. When two sources disagree, the higher one wins **and the discrepancy is recorded** in `docs/11-open-questions.md`. Never resolve a disagreement silently by picking a side.

1. Approved requirements and human decisions — `docs/10-requirements.md`, `docs/30-decisions/`
2. Approved constitution and charter — this file, `docs/00-charter.md`
3. Approved feature specification — `specs/<id>-<feature>/spec.md`
4. Approved ADR — `docs/30-decisions/ADR-####-*.md`
5. Code and tests — evidence of *implemented* behaviour
6. Dated runbooks with evidence — `docs/60-runbook.md`
7. `README.md` — index and quick start (portfolio optimized)
8. Historical reports — `docs/archive/`
9. Proposals and drafts — anything whose status is `Proposed`

## Documentation

`docs/` is numbered by lifecycle phase, one document per phase. **The map lives in `docs/README.md`**.
Do not renumber it, and do not invent a second convention.

Rules:
- Any change starts in the specification, never in the code.
- A feature gets its own directory under `specs/`. Never a phase file at the top of `docs/`.
- A significant decision is recorded as an ADR with status `Proposed`. Only the owner promotes it.

## Feature specifications (Spec-Driven Development)

**The Spec-Driven Development (SDD) Workflow is non-negotiable:**
1. **Specify**: Draft `spec.md` using EARS.
2. **Clarify**: Detect ambiguities in `clarification.md`.
3. **Plan**: Architecture, data model, ADRs in `plan.md`.
4. **Tasks**: Break down `tasks.md` into 30m chunks.
5. **Analyze**: Pre-flight cross-check in `checklist.md`.
6. **Implement**: Test-first (TDD) execution.
7. **Converge**: Audit codebase against spec; append missing tasks.
8. **Validate**: Traceability in `validation.md`.
9. **Change**: Update `spec.md` before touching code.

These seven file names are mandatory in any `specs/<NNN>-<name>/` folder.

## Architecture invariants

The API gateway only handles HTTP requests and routing. Logic lives in Application (Consumers/Template Engines) and Domain. Infrastructure handles SMTP and RabbitMQ bindings.

1. **MassTransit + RabbitMQ**: Messaging is asynchronous.
2. **Domain-centric**: Domain has no dependencies. Application depends on Domain. Infrastructure implements Application interfaces.
3. **No direct database or HTTP calls from Domain.**
4. **Privacy**: Personal Identifiable Information (PII) must be masked in logs via `PrivacyMasker`.

Layers, strictest first:

| Layer | Path | May not |
| :--- | :--- | :--- |
| Domain | `Notificaciones.Domain/` | Depend on any other project or infrastructure package |
| Application | `Notificaciones.Application/` | Do I/O directly (HTTP, SMTP, DB). It uses interfaces |
| Infrastructure| `Notificaciones.Infrastructure/` | Contain business rules. It only maps and transports |
| Worker | `Notificaciones.Worker/` | Be an HTTP server. It only runs background tasks |
| API | `Notificaciones.Api/` | Do heavy lifting. It validates and pushes to the bus |

## Commands

```bash
# Compile
dotnet build

# Test
dotnet test

# Run In-Memory Demo
dotnet run --project src/Notificaciones.Api
```

A task is done when its "Done when" criterion is verifiable **and verified**. **Test-Driven Development (TDD) is mandatory**.
