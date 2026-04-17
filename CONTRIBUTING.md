# Contributing Guide

## Branch strategy
- `main`: protected branch.
- `feature/*`: new functionality.
- `fix/*`: bug fixes.
- `chore/*`: tooling and maintenance.

## Commit style
Use Conventional Commits:
- `feat:`
- `fix:`
- `refactor:`
- `test:`
- `docs:`
- `chore:`

## Pull request checklist
- Scope aligned with MVP docs in `docs/`.
- API changes reflected in `src/backend/contracts/openapi-v1.yaml`.
- RBAC and transition rules verified.
- Tests or smoke validations included.
- No secrets or credentials committed.
