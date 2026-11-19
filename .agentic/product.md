# Product brief

> Harbourline Lending is a fictional company. All names, documents and numbers in this repo are invented.

## Problem
Harbourline's fund investors receive statements as email attachments, and nobody can say who opened what or when.

## Users
Investor relations (weekly uploads); investors (quarterly, mostly mobile).

## Outcomes
Zero statements sent by email; access log available on request in seconds.

## Constraints
POPIA; data hosted in South Africa or the EU; MFA for investors.

## Out of scope
Anything not needed for the outcomes above in the first three slices.

## Traits (for the architecture advisor)
- MultiTenant: "each investor entity must see only its own statements"
- RegulatoryAudit: "the fund must prove which investor accessed which document"
- PersonalData: "investor contacts and tax details"
- ReadHeavy: "many downloads at quarter end, few uploads"
- SeveralBusinessAreas: "documents, capital accounts, notices"
- SmallTeam: "two engineers"
