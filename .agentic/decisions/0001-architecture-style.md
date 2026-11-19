# 0001. Architecture style

- Status: Accepted
- Date: 2026-11-19

## Context

Harbourline's fund investors receive statements as email attachments, and nobody can say who opened what or when. Traits: MultiTenant, RegulatoryAudit, PersonalData, ReadHeavy, SeveralBusinessAreas, SmallTeam.

## Decision

Build a modular monolith (documents, capital accounts, notices) with tenant isolation enforced centrally and tested.

## Alternatives considered

- Single layered app: boundaries between the three areas would blur fast.
- Microservices: quarter-end peaks fit one scaled app and a CDN for files.

## Consequences

- Clear boundaries that tests can enforce.
- Room to extract a part later if its load demands it.
