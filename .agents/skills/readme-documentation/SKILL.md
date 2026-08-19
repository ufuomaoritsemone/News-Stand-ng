---
name: readme-documentation
description: Enforces mandatory documentation in README.md under the '## Updates' section after every code update, feature implementation, bug fix, or architectural change.
---

# README Update & Change Documentation Skill

## Goal
Ensure that all AI agents, after completing any code changes, feature additions, bug fixes, refactoring, or architectural improvements, automatically document the updates in the project's root [`README.md`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/README.md) under the `## Updates` section.

---

## 1. Mandatory Core Rule
> [!IMPORTANT]
> **Every task that modifies code, configuration, database schemas, APIs, or UI MUST conclude with updating [`README.md`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/README.md).**
> Never consider a task finished or submit a final completion response without documenting the changes in `README.md`.

---

## 2. Location & Structure in `README.md`

All updates must be recorded under the `## Updates` heading in [`README.md`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/README.md).

### Chronological Ordering
- New entries must always be placed **at the top** of the `## Updates` section (immediately below the `## Updates` header line), ahead of all previous entries.
- Never overwrite, truncate, or delete historical update entries.

### Required Entry Template
```markdown
### [Month Day, Year] — [Concise Headline of Update]
[1-2 sentence executive summary of the changes, architectural decisions, and functional impact]
- **[Component / Feature / Area]**: [Detailed description of changes, specific classes, files, methods, regex patterns, or configurations modified].
- **[Component / Feature / Area]**: [Detailed description of another aspect of the change].
- **[Testing & Verification]**: [Summary of tests added/updated, test pass status, or verification steps executed].
```

---

## 3. Documentation Standards & Best Practices

1. **Technical Specificity**:
   - Reference exact class names, interfaces, methods, endpoints (e.g., `POST /api/v1/articles/ingest`), configuration keys, and database entities.
   - Mention key algorithms, mathematical models, regular expressions, or architectural patterns used (e.g., L-BFGS multiclass classification, SemaphoreSlim throttling, stream-based response reading).

2. **Metrics & Impact**:
   - When applicable, document performance improvements, accuracy benchmarks (e.g., *Micro-accuracy improved from 36% to 85%*), latency reductions, or test coverage metrics (e.g., *42/42 unit tests passing*).

3. **Subsystem Clarity**:
   - Clearly delineate which sub-project or tier was affected (e.g., `NewsApi`, `NewsScraperService`, `NigerianNewsGrid` MAUI client, `AdminDashboard`, `NewsCategorizer.Trainer`, `TtsWorker`).

4. **Preserve Existing Documentation**:
   - Maintain the integrity of project overview, run instructions, port endpoints, and all prior changelog records.

---

## 4. Agent Execution Workflow

When completing any coding task, follow this exact sequence:

1. **Implement Changes**: Make all necessary code, asset, or config changes.
2. **Build & Verify**: Compile the solution and run automated tests to ensure correctness.
3. **Inspect README.md**: Locate the `## Updates` section in [`README.md`](file:///c:/Users/ufuom/source/repos/NigerianNewGrid/README.md).
4. **Draft & Insert Entry**: Format the update with the current date (e.g., `August 16, 2026`) and insert it directly beneath `## Updates`.
5. **Final Review**: Verify that markdown formatting, code identifiers, and bullet points match existing project conventions.
