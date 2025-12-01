# AWS DDNS API Only

## Vision

Explore a minimal dynamic address registry that allows systems with changing public IP addresses to publish their current address and allows other systems to retrieve it through an API.

This project deliberately removes DNS from the problem.

The purpose is to isolate the underlying capability that dynamic DNS is providing:

> stable identity → changing network address

Instead of projecting that state into Route 53, the system simply maintains it directly and exposes it through a lightweight API.

The goal is not to build a production-grade registry service. It is to create a paper prototype that makes the core behaviour concrete and allows the design to be explored without DNS-specific concerns.

## Problem

Dynamic DNS combines two separate ideas:

- discovering or reporting a changing address;
- and exposing that address through DNS.

Those concerns do not necessarily need to be coupled.

There are scenarios where another system only needs to know:

> what is the current address of system X?

In those cases, querying a small API may be sufficient.

Removing DNS also makes it easier to understand the core state-management problem before introducing:

- DNS TTLs;
- caching;
- Route 53 API behaviour;
- propagation;
- record management;
- and other DNS-specific mechanics.

This project should determine what the simplest useful dynamic address registry looks like.

## Basic Model

Each participating system has a stable identity.

Its network address is allowed to change.

The registry maintains the most recently accepted mapping between the two.

Conceptually:

> system identity → current IP address

A client reports its current address:

> client → registry API → current state

A consumer retrieves it:

> consumer → registry API → current address

That is the essential system.

Everything beyond that should be added only where it helps explore an important behaviour.

## Registration and Updates

Participating systems should periodically report their current address.

The exact reporting mechanism is intentionally open.

A client might:

- explicitly determine its own public IP and submit it;
- allow the API to infer the source address;
- submit an identifier alongside the address;
- or use another simple registration mechanism.

The implementation should explore whatever approach provides the clearest prototype.

Repeated updates should be harmless.

If a client reports the same address many times, the resulting state should remain equivalent to having reported it once.

The system should also handle address changes cleanly:

> old address → new report → current address becomes new value

## Data Model

Keep the initial model small.

A registry entry may contain values such as:

- system identifier;
- current address;
- time the address was last reported;
- time the address last changed;
- and minimal metadata useful for identifying the system.

Do not add a large schema simply because more fields might eventually be useful.

The purpose of the first version is to determine which information is actually required to make the registry useful.

## Query Behaviour

Consumers should be able to retrieve the latest known address for a system.

The simplest useful operation is effectively:

> give me the current address for this identity

Additional queries may be worth exploring if they naturally emerge from the experiment, such as:

- listing registered systems;
- checking when an address was last observed;
- determining whether an entry appears stale;
- or retrieving a small amount of system metadata.

Avoid turning the initial implementation into a general-purpose inventory API.

The main responsibility remains address registration and lookup.

## Freshness and Staleness

A stored address is only as trustworthy as the most recent report.

The experiment should therefore consider whether freshness needs to be represented explicitly.

For example, there is an important difference between:

> this system reported `203.0.113.10` thirty seconds ago

and:

> this system last reported `203.0.113.10` three months ago

The registry may expose timestamps or some indication of stale state so that consumers can decide how much confidence to place in the returned address.

The exact policy should be explored rather than fixed in advance.

The system does not necessarily need to delete old entries simply because they have not recently reported.

## Consistency

The desired behaviour is eventual correctness rather than real-time synchronization.

Clients can report periodically.

The system only needs to maintain the latest accepted state and make it available reliably to consumers.

The prototype should explore simple handling for cases such as:

- repeated identical reports;
- rapid consecutive address changes;
- temporary client failure;
- stale entries;
- concurrent updates;
- and duplicate submissions.

There is no need to solve distributed consensus or other production-scale consistency problems unless the experiment demonstrates that they are relevant.

## Relationship to Dynamic DNS

This project should remain conceptually related to the Route 53 DDNS experiment, but it should not depend on it.

The Route 53 version can be thought of as:

> identity → address → DNS record

This experiment stops at:

> identity → address

That distinction is useful.

It allows the project to explore whether some use cases currently assumed to require DNS actually only require a stable lookup mechanism.

It also provides a cleaner view of which responsibilities belong to dynamic address tracking and which responsibilities exist specifically because DNS is being used as the presentation layer.

## Possible Extension

If the registry proves useful, it could later become the source of truth for other projections.

For example:

> dynamic address registry → Route 53

or:

> dynamic address registry → configuration generation

or:

> dynamic address registry → another discovery mechanism

This should not be treated as a required architecture for the prototype.

The initial implementation should remain focused on proving the registry itself.

The useful architectural question is simply whether separating:

> address state

from:

> how that state is published

creates a cleaner model.

## Questions

The project should help answer questions such as:

- What is the minimum useful representation of a dynamic endpoint?
- Should clients report their address or should the service infer it?
- How should a client identify itself?
- How frequently should clients report?
- What makes an entry stale?
- Should old entries expire automatically?
- What information should a lookup return beyond the address itself?
- How should repeated identical updates behave?
- What happens when several updates arrive in rapid succession?
- Is history useful, or is only the latest value necessary?
- Is a simple API materially easier or more useful than DNS for some scenarios?
- Could this registry sensibly act as a source for other systems later?
- Which parts of a traditional DDNS design disappear entirely once DNS is removed?

These are exploration questions rather than a fixed implementation checklist.

## Security

Keep security deliberately secondary during the initial paper prototype.

The project will eventually need some answer for:

- who may register an identity;
- who may modify an address;
- who may query addresses;
- and how spoofed updates are prevented.

However, do not let authentication or authorization architecture dominate the first experiment.

Use enough protection to make the prototype coherent, then focus on validating the registry model.

More sophisticated security can be explored after the basic behaviour has proven useful.

## Boundaries

Do not introduce DNS into the initial implementation.

Do not turn the service into a general asset inventory platform.

Do not attempt to model every property of the participating systems.

Do not prematurely build:

- complex tenancy;
- high availability;
- multi-region replication;
- sophisticated identity management;
- elaborate administrative interfaces;
- or large-scale event processing.

Those may become relevant later, but they are not required to answer the initial question.

The prototype should remain recognizably small.

## Expected Output

The repository should demonstrate a complete dynamic-address registration and lookup flow.

Useful outputs may include:

- a simple reporting client;
- an API for receiving address updates;
- persistence of the latest known state;
- an API for retrieving current addresses;
- timestamps or freshness information;
- examples involving several independently changing systems;
- and notes describing behaviours discovered during experimentation.

Small experiments comparing alternative approaches are useful where they help clarify the design.

## Success

The work is successful if one system can publish its changing address and another system can reliably retrieve the latest known value without involving DNS.

The implementation should make the underlying dynamic-address problem feel simple and explicit.

The useful final question is:

> If DNS is removed entirely, what is the smallest effective system for tracking and retrieving changing endpoint addresses?

The prototype should provide enough evidence to determine whether such a registry is independently useful and whether it could serve as a foundation for other mechanisms later.
