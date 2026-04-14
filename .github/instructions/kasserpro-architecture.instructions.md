---
description: "Always enforce KasserPro architecture and safety rules from .kiro/steering/architecture.md for every task in this repository."
applyTo: "**"
---

# KasserPro Always-On Architecture Guardrails

Use `.kiro/steering/architecture.md` as the authoritative architecture and data safety policy for all edits.

Required defaults:

1. Clean architecture boundaries must be preserved.
2. Multi-tenant and branch isolation must be preserved.
3. Financial flows must be transactional and auditable.
4. Backend responses should follow `ApiResponse<T>` conventions.
5. SQLite migration safety patterns must be respected.

If a request conflicts with these guardrails, provide a compliant implementation and explain the tradeoff.
