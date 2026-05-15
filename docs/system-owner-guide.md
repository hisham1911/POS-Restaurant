# System Owner Guide

## Overview
- The System Owner is the root account for the whole system.
- It is not tied to any tenant or branch (TenantId and BranchId are null).
- It is created only when no System Owner exists.

## Where it is created
1) SystemSeedService.EnsureSystemOwnerAsync
   - Called from RunFullSeedPipelineAsync.
2) MultiTenantSeeder.EnsureSystemOwnerAsync
   - Called from MultiTenantSeeder.SeedAsync for demo tenants.

## Identity
- Name: System Owner
- Email: owner@kasserpro.com
- Role: SystemOwner
- Active: true

## Password rules (current behavior)
Priority order:
1) If env var KASSERPRO_SEED_SYSTEM_OWNER_PASSWORD is set and non-empty, it is used.
2) Otherwise, a fixed default is used: Owner@123 (all environments).

This behavior is defined in SeedSystemOwnerPasswordResolver.

## How to change the password rules
- Edit SeedSystemOwnerPasswordResolver:
  - EnvironmentVariableName
  - FixedDefaultPassword
  - ResolveWithSource logic
- Update log formatting in SystemSeedService if you rename sources.

## Operational notes
- After initial seed, the System Owner is not recreated if it already exists.
- For production, consider setting the env var and rotating the password after first login.
- Verify by checking the Users table for Role = SystemOwner.

## Related files
- backend/KasserPro.Infrastructure/Data/SeedSystemOwnerPasswordResolver.cs
- backend/KasserPro.Infrastructure/Services/SystemSeedService.cs
- backend/KasserPro.Infrastructure/Data/MultiTenantSeeder.cs
