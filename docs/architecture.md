# Architecture

## Purpose

AutoContext curates the context an AI coding agent works with. It provides a
corpus of instruction files describing how code should be written, and a set
of analysis tools that check code against those same descriptions. Both are
filtered to the workspace, so a repository sees only what applies to it.

Implementation detail — option names, defaults, file layouts, the tool
catalogue — changes without changing anything here.

## The central decision

**One process owns all AutoContext state.** That process is the engine.

Configuration, the instruction corpus and its projection, workspace
detection, the tool registry, worker processes, and logs are engine state.
Hosts do not read that state from disk and do not maintain their own copies.
They connect to the engine and ask.

```
     editor extension        agent hooks        MCP clients        command line
             │                    │                  │                  │
             └────────────────────┴─────────┬────────┴──────────────────┘
                                            │
                                     client connections
                                            │
                                 ┌──────────▼──────────┐
                                 │       engine        │
                                 │                     │
                                 │  configuration      │
                                 │  instructions       │
                                 │  workspace detection│
                                 │  tools              │
                                 │  worker supervision │
                                 │  logs               │
                                 └──────────┬──────────┘
                                            │
                          ┌─────────────────┼─────────────────┐
                          │                 │                 │
                       worker            worker            worker
```

Three consequences follow, and they shape everything below.

**There is one reader.** State is read once, in one place. A host that
cached its own copy would become a second source of truth, free to diverge —
and divergence in a system whose whole job is *correct context* is not a
cosmetic bug.

**The connection is the only seam.** The engine serves; clients consume.
Nothing is shared between them except the message contract. There is no
common library both sides program against, because such a library becomes a
third place where behaviour lives and a channel through which the two halves
can couple.

**Hosts are interchangeable.** No host has privileged access. An editor
extension, a hook script, a command-line tool, and a third-party integration
all see the same surface. Adding a host is not an architectural change.

## Boundaries

### The client/server asymmetry

The engine binds endpoints and answers; clients dial endpoints and ask. This
asymmetry is deliberate and load-bearing. It is what makes the engine's
authority enforceable rather than merely conventional: a client *cannot*
serve state it does not own, because it has nothing to serve it on.

It must not be softened. An abstraction that lets either side play either
role would dissolve the distinction the architecture depends on.

### Endpoint roles

Client connections are separated by role rather than multiplexed onto one
channel, because the roles have genuinely different semantics:

| Role | Nature |
|---|---|
| Request/response | Ask a question, get an answer; also carries long-lived subscriptions |
| Event stream | Engine-initiated notifications the client did not ask for individually |
| Health probe | Cheap liveness check requiring no session |
| Log stream | Observability output |

The split matters most for **lifetime accounting**. Only the roles that
represent a client actually *using* the engine keep it alive. Observability
roles are passive: attaching a log viewer or a health poller must never be
the reason a process continues to exist. Collapsing these onto one channel
would make "is anyone using this?" unanswerable.

### Versioning the contract

Every session begins with a handshake that exchanges a protocol version, and
the match is exact — there is no negotiation and no partial compatibility.

A mismatched client is refused rather than served degraded answers.
Negotiation would mean the engine must implement every historical shape
forever, and a client could silently receive something other than what it
asked for. Refusal fails loudly at the boundary, which is where a version
problem is cheapest to diagnose.

### Identity and isolation

Endpoints are addressed by composing a **role**, a **workspace identity**
derived from the workspace path, and an **instance identity** minted by
whoever started the engine.

This yields isolation on two axes without any coordination protocol.
Different workspaces cannot collide because their identities differ.
Multiple windows on the same workspace cannot collide because their instance
identities differ. Isolation falls out of naming rather than being enforced
by a broker.

## Lifecycle

The engine is **on demand, not resident**. It exists while something needs
it and stops when nothing does.

**Attach or start.** A client never assumes an engine is running, and never
assumes one is not. It attempts to connect; if nothing answers and the
client is permitted to start one, it does so and retries. Concurrent first
callers converge on a single engine rather than racing to create several.
Readiness is proven by a successful connection — the engine accepts
connections only once it is fully able to serve, so connecting *is* the
readiness check. Nothing polls for a signal that could be reported before it
is true.

**Idle-bounded lifetime.** When the last using client disconnects, the
engine waits, and if nothing reconnects, it stops itself. This is what keeps
"on demand" from degrading into "resident forever": the common case of
closing an editor window leaves nothing behind, while the common case of
reopening one attaches to a warm process instead of paying a cold start.

**Supervision.** An engine started on behalf of a host can be bound to that
host's lifetime, so it cannot outlive the thing it was started for. Orphaned
processes are prevented structurally rather than cleaned up afterwards.

**Announced shutdown.** Shutdown is published to subscribers before it
happens, and a bounded drain period lets them observe it. Clients learn that
an engine is going away by being told, not by discovering a broken
connection and guessing why.

**Liveness directory.** Live engines are discoverable through a shared
registry, so any process can enumerate what is running without probing.
Because a process can die without removing its entry, entries are validated
against process identity rather than trusted — a directory of running
processes must assume its own staleness.

## State

State divides by **who owns it and where it belongs**.

**Workspace state** is the user's decisions about their project: which
instruction files apply, which individual rules within them are disabled,
which tools are active. It lives in a file in the workspace, because it is
per-project, belongs in source control, and should be reviewable and
shareable like any other project configuration.

**Machine-local state** is everything derived or incidental: logs, caches,
the liveness registry. It lives outside the workspace, because it is neither
shareable nor meaningful to anyone else.

The engine writes workspace state as well as reading it, which creates the
classic hazard: a file watcher observing the writer's own change. The
architecture resolves this by having the writer recognise its own
modifications, so an echo is distinguishable from a genuine external edit.
Without that, every write would trigger a reload of what was just written.

### Disable is granular

A user can disable a whole instruction file, or individual rules within a
file that otherwise remains active. These are different operations at
different granularities, and both must survive to the point of use — any
component that serves instruction content has to honour rule-level
decisions, not just file-level ones. A path that respects only the coarse
setting silently returns guidance the user switched off.

### Curation and derived facts are separate

Instruction metadata comes from two sources, kept in separate artefacts:

- **Curated** — human editorial judgement: how files are categorised, what
  they are called, when they apply.
- **Derived** — mechanically extracted from the corpus: structure, applicable
  file types, versions, content hashes.

They are separate because they have different authors and different
lifecycles. Derived facts are regenerated whenever the corpus changes;
editorial judgement is not, and must never be destroyed by regeneration.
Merging them into one artefact would put a generator in a position to
overwrite human decisions.

### Stored form is not served form

Instruction files are stored in one form and served in another. What a
client receives is a **projection**: the stored content filtered by the
user's decisions and narrowed to what was asked for.

Projection happens in the engine, not in clients, for the same reason state
does: if each consumer projected for itself, each could get it subtly wrong,
and there would be no single answer to "what does this workspace actually
see?"

Anything derived from instruction content must derive it from the
*projected* form. Content search that indexed stored text would surface
material the user disabled — technically present, but not part of their
context. Indexing the projection makes that impossible by construction
rather than by remembering to filter afterwards.

### Overrides

A workspace can supply its own version of an instruction file, shadowing the
built-in one. Precedence is definite and one-directional: workspace beats
built-in. The system does not merge the two, because merged guidance has no
owner and no reviewable source.

### Activation is derived, not configured

The engine inspects the workspace and derives a set of facts about it —
which languages, frameworks, and tooling are present. Those facts gate what
is offered: instruction files and tools declare what must hold for them to
apply.

This is the mechanism behind the product's core promise. Relevance is
computed from the workspace rather than curated per user, so it stays
correct as a project changes, and a repository never sees guidance for
technology it does not use.

## Capabilities

The engine serves a small number of capability families: workspace
configuration, instruction retrieval and search, workspace facts, tool
listing and invocation, contextual routing, agent-lifecycle events, and log
access. Some are request/response; some are subscriptions that deliver a
snapshot and then updates.

Two properties of this surface are architectural rather than incidental.

**Expected outcomes are values, not failures.** "That is disabled", "that
name does not exist", and "those arguments do not fit" are ordinary answers
a caller must handle, and they are modelled as distinct results rather than
raised as errors. Collapsing them into an error channel would merge domain
answers with transport faults, discarding the distinction the caller needs
to react correctly.

**Slow consumers are dropped, not tolerated.** A subscriber that cannot keep
up is disconnected with a terminal message telling it so. The alternative —
unbounded buffering — lets one stalled client consume the engine's memory
and eventually degrade service for everyone. Bounded buffers make
backpressure explicit and its consequence honest.

## Tools

Tool definitions are split by concern into two artefacts:

- **Execution** — what a tool is to a model, what arguments it takes, and
  which worker performs it.
- **Presentation and activation** — where a tool appears in the interface,
  and which workspace facts must hold for it to be offered.

Categories form a tree, and activation requirements accumulate down it: a
tool is offered only when everything its category and that category's
ancestors require is satisfied. Gating is therefore declared once at the
level where it is true, rather than repeated on every tool.

The split exists because the two concerns change independently and are
edited by different people. The model-facing contract can evolve without
disturbing the interface, and gating can be revised without touching a
single tool schema.

Invocation validates arguments and applies the user's decisions *before* any
worker is contacted. Rejecting a call for a disabled or unknown tool, or one
with arguments that do not fit, is the engine's responsibility — a worker
should never be started to discover that the request was never valid.

## Workers

Analysis runs in **separate processes** from the engine, one per capability
area.

Isolation is the point. Analysis means parsing untrusted source, loading
language toolchains, and running third-party libraries; a crash, a memory
leak, or a pathological input in that work must not take down the process
that owns all state. The engine supervises workers; it does not host their
code.

**Workers declare themselves.** A component is a worker because it carries a
descriptor saying so — not because of where it sits or what it is named.
Identity is declared rather than inferred, so the roster is explicit and a
component cannot be silently included or omitted by a naming accident.

**Workers start on demand.** A worker process exists only after something it
provides is actually requested. A workspace that never triggers a given
analysis never pays for it.

Because a cold start is genuinely slow — process creation, runtime warm-up,
connection setup — the engine's willingness to wait must exceed any client's
patience. A client that gives up before the engine does abandons a request
that would have been answered, which is worse than waiting.

**The contract is the wire, not a library.** A worker is defined by its
descriptor and by the dispatch protocol it speaks over its pipe — nothing
else. It need not share a runtime with the engine, and need not link any
particular helper library. Shared worker-hosting scaffold exists as a
convenience for workers that want it, but a worker that implements the
protocol directly is equally valid. Keeping the boundary at the wire is what
lets an analysis capability be written in whatever language suits it.

Adding an analysis capability therefore means adding a worker or a task
behind that contract, never extending the protocol between engine and
workers.

## Observability

All log output converges on the engine. It records its own activity, and
workers send theirs to the engine rather than writing independently. One
stream carries everything, with records tagged by origin.

The engine is the only process that spans the whole system; a viewer that
had to assemble a picture from several independent sources would have to
re-derive ordering and correlation that the engine already has. Workers
having their own separate output channel would also make them
independently observable but collectively incoherent.

Logs are available both as bounded history and as a live stream, and
consuming either is a passive act that does not keep the engine alive.

## Module structure

| Module | Responsibility |
|---|---|
| Transport substrate | Framing and connection primitives |
| Protocol | The wire contract: message shapes and address composition. Inert |
| Engine core | The engine as a library: state ownership, capability handlers, endpoint binding |
| Engine host | The engine as a runnable process |
| Client core | Connecting, consuming, and starting an engine |
| Worker runtime | The task contract and worker-side hosting |
| Instruction parser | Reading instruction files into a structured form |
| Build-time generators | Producing derived artefacts from curated sources |
| Workers | The analysis capabilities themselves |
| Hosts | Editor extension, hooks, command line |

The dependency rule is **one-way**. The transport substrate and the protocol
are leaves: they depend on nothing else and are depended upon by everything.
Engine core, client core, and worker runtime each build on those leaves and
**never on each other**.

This is the asymmetry from *Boundaries* expressed structurally. The engine
serves, the client consumes, and workers provide — three roles that share a
contract and nothing else. If any two of them referenced each other, the
seam would stop being the wire and start being code, and the guarantee that
hosts are interchangeable would quietly disappear.

The instruction parser is deliberately dependency-free so that one
implementation serves both the build-time generators and the engine at
runtime. Two parsers would mean two interpretations of the same file, with
generated metadata describing something other than what is served.

## Distribution

The engine ships **self-contained per platform**, carrying its own runtime,
workers, instruction corpus, and generated artefacts.

A host that bundles the engine can rely on it working without the user
installing a runtime or the host discovering one. The engine resolves its
resources relative to itself rather than to whoever launched it, so it
behaves identically however it was started.

Because platform-specific artefacts are built separately, packaging verifies
what it produced: that binaries match their intended platform, and that
content which should be identical everywhere actually is. A build that
cannot check its own output is a build that ships whatever it happened to
produce.

## Invariants

The rules above reduce to a handful of statements that must remain true:

1. The engine is the only reader and writer of AutoContext state.
2. Clients hold no authoritative copy of that state.
3. The wire contract is the only thing shared between engine and clients.
4. Engine, client, and worker modules never reference one another.
5. Observability never extends a process's lifetime.
6. Expected outcomes are values; only faults are errors.
7. Content is served, searched, and indexed in projected form only.
8. Human curation is never overwritten by generation.
9. Analysis runs outside the process that owns state.
10. No host is privileged.
