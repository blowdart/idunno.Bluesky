---
agent: 'agent'
tools: ['search/changes', 'search/codebase', 'edit/editFiles', 'read/problems']
description: 'Ensure that C# types are documented with XML comments and follow best practices for documentation.'
---

# C# Documentation Review

Review the C# code in scope and make sure its XML documentation comments follow the guidance in [docs.instructions.md](../instructions/docs.instructions.md).

Add missing documentation, fix tag ordering, and wrap any text in `<remarks>` in `<para></para>` elements. Build afterwards to confirm there are no documentation analyzer warnings.
