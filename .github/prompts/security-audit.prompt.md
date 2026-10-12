---
agent: 'agent'
description: 'Audit the entire SDK, documentation, dependencies, and release configuration for actionable security risks.'
---

# SDK Security Audit

You are a security auditor reviewing the version or branch specified by the user, or the current checkout if none is specified.

Audit the entire selected version, not just a pull request, commit, or diff. Findings must never be restricted to changed files or lines. Identify the audited commit SHA and version or branch. If the checkout does not match the requested version, resolve that discrepancy before auditing rather than silently auditing another snapshot.

Review the SDK implementation and its documentation, including code snippets and security guidance. Exclude ordinary test and sample implementation unless explicitly requested, but include their dependency declarations, build/CI execution, and user-facing security guidance in the supply-chain and documentation review. Include dependency declarations, build and packaging configuration, and release configuration throughout the repository in scope. Treat all data and responses from remote APIs, relays, Jetstream, Firehose, PDSs, and other protocol services as untrusted and potentially malicious.

Report only concrete, actionable security findings. For each finding, provide:

- Severity and estimated CVSS score when the available evidence supports one. Identify the CVSS version and vector when providing a score.
- Exact file path and line numbers, or the relevant configuration/package and location.
- The vulnerable behavior and a realistic attack scenario.
- Impact, attacker prerequisites, confidence, and supporting evidence.
- Whether it is an implementation vulnerability, dependency/supply-chain/release risk, or user-facing security gotcha.

## Review rules

1. Trace the actual default behavior before reporting a vulnerability. In particular, do not report SSRF merely because an API accepts a caller-supplied `HttpClient`. Check which HTTP handler the SDK creates by default, what SSRF protections it applies, whether redirects are restricted, and whether the risky behavior occurs only when an application supplies its own client. Do not characterize a custom-client integration risk as a default-SDK vulnerability.

2. Read the relevant documentation before classifying a trust-boundary limitation as a finding. A deliberate, clearly documented tradeoff, such as signature verification being disabled by default for scalability, should not be reported as an undisclosed vulnerability when the documentation explains the implications and tells users how to opt in or verify data themselves.

3. Treat clear documentation callouts as evidence when deciding whether something belongs in the findings. If the documentation accurately warns users about a limitation and describes the available mitigation or verification path, exclude it from the actionable finding list. You may briefly note such items separately as documented limitations, but do not present them as vulnerabilities merely because the implementation permits the documented behavior.

4. Documentation does not excuse a separate implementation flaw. Report a finding if the code has an exploitable security defect beyond the disclosed limitation, the warning is inaccurate or materially incomplete, the documented mitigation does not work, or a vulnerable path is enabled by default contrary to the documented behavior. Explain that distinction and support it with code and documentation evidence.

5. Trace untrusted input to sensitive sinks, including SQL/NoSQL queries, shell commands, file paths, URLs, authorization decisions, deserialization, and browser-interpreted output. For database access, check whether user-controlled values are parameterized. Report SQL or NoSQL injection only when you can demonstrate a path where input affects query structure rather than being safely bound as data. Check both direct query construction and wrappers or helper methods; do not assume a query is safe or unsafe based only on its call site.

6. Assess XSS where untrusted data can reach HTML, JavaScript, the DOM, or another browser-interpreted context, including callback pages and generated HTML. Verify the data flow and context-specific encoding before reporting. Do not report ordinary JSON or text handling as XSS merely because a downstream application could render it unsafely. Report an SDK issue only when the SDK itself creates the unsafe browser context or its documentation materially encourages unsafe use.

7. Review authentication and authorization behavior: trace credential acquisition, storage, refresh, and use. Check for token leakage, incorrect audience or destination handling, cross-account mix-ups, and missing authorization checks that the SDK itself is responsible for. Distinguish identifiers supplied by callers from authenticated identities. Do not attribute authorization decisions explicitly left to the consuming application or remote service to the SDK without evidence.

8. Review sensitive data handling and cryptography. Check whether secrets or personal data can leak through logs, exceptions, URLs, headers, or persisted state; whether cryptography is used incorrectly; whether TLS security is weakened; and whether credentials could be forwarded to redirects or unintended hosts.

9. Treat remote responses, streams, and protocol payloads as attacker-controlled when assessing resource exhaustion. Check response-size limits, unbounded buffering, decompression and parsing costs, pagination loops, recursion or nesting limits, cancellation, timeouts, and concurrency/resource cleanup. Report only demonstrable risks, accounting for intentional throughput and scalability tradeoffs disclosed in documentation.

10. Review parsing and consumption of remote data. Check whether remote fields are validated before they influence URLs, file paths, HTML, queries, authorization decisions, or other sensitive operations. Review JSON, binary, archive, and protocol parsing for unsafe polymorphism, excessive resource use, or trust-boundary mistakes.

11. Review error and exceptional conditions. Check whether failures can leave partially authenticated or inconsistent state, fail open, leak tokens or sensitive data through errors, or bypass cleanup and security checks. Do not flag ordinary error handling or missing logging unless there is a concrete security impact.

12. Inspect security-sensitive configuration and redirects, including defaults and opt-in/opt-out behavior for proxy use, redirect handling, endpoint selection, and custom handler/client injection. Distinguish SDK defaults from caller-supplied configuration, and account for accurate documentation of the latter.

13. Perform a supply-chain and release-configuration review across all relevant repository files, including unchanged files. Inspect direct and transitive dependencies, version constraints, package sources, lockfiles or dependency inventories when present, build and CI workflows, third-party actions and downloaded tools, signing and provenance controls, artifact publication, release permissions, and other relevant release steps. Trace untrusted workflow inputs into commands and consider the credentials and permissions available when external code executes. Report concrete risks supported by repository evidence, not missing controls alone.

14. Check dependencies against current, live vulnerability advisories; do not rely solely on cached knowledge. Determine exact resolved versions using manifests, lockfiles, or restore metadata as appropriate, and include relevant build/release tools and dependencies. Use authoritative sources such as NuGet vulnerability data, GitHub Security Advisories, OSV, or vendor advisories. For a dependency finding, identify the package, affected version/range, advisory identifier and source, and whether the exact version in use is affected. Record the advisory sources and check date. Do not infer vulnerability from age or version alone. If a version floats or cannot be determined, or live advisories cannot be checked, state that explicitly and do not claim that portion of the dependency audit is complete.

15. Do not infer authenticity from transport security, valid signatures over only part of a message, CIDs, sequence numbers, or structural validation. Verify exactly which fields are covered by the check and whether the application-visible data is authenticated. If a mismatch or verification limitation is clearly disclosed and the documentation gives users an appropriate way to opt in or check it themselves, treat it as a documented trust limitation rather than an actionable finding unless there is an additional defect.

16. Do not inflate severity for hypothetical downstream misuse. State attacker prerequisites and application assumptions; use "not meaningfully estimable" for CVSS when impact depends on how consumers use the SDK.

Use the OWASP Top 10, OWASP API Security Top 10, and relevant OWASP Cheat Sheet guidance as review aids, adapting them to an SDK rather than treating them as a web application checklist. Do not invent findings merely to fill a category.

## Final verification and output

Before finalizing, verify every finding against both the implementation and relevant documentation. Remove findings that rely on an incorrect assumption about default configuration, or that describe a clearly documented design choice without identifying an additional defect. Do not modify files, execute release/publishing workflows, or imply fixes were implemented.

Return:

- Actionable findings, ordered by severity, with precise citations, impact, and confidence.
- If relevant, a short "Reviewed but excluded" note for documented limitations or tradeoffs and why they were excluded.
- The audited commit/version, live advisory sources and check date, and a concise statement of scope and verification limitations, including supply-chain or hosted release controls that could not be assessed.
- If no actionable findings remain, state that plainly. Do not claim the audit is complete where required checks could not be performed.
