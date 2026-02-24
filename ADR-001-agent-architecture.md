# ADR-001: Agent Architecture for Microsoft Certification Study Assistant

**Status:** Proposed  
**Date:** 2026-02-18  
**Decision Makers:** Adam (sole developer)  
**Context:** Agents League – Reasoning Agents Battle #2 (Microsoft Reactor Hackathon)

---

## Context

We are building a multi-step AI system to help students prepare for Microsoft certification exams. The system must curate learning paths from Microsoft Learn, generate study plans, send engagement reminders, administer readiness assessments, and route students toward exam planning or back into preparation. The competition requires use of the Microsoft Agent Framework and/or Microsoft Foundry, integration with external tools (notably the Microsoft Learn MCP server), and demonstrable multi-step reasoning.

The suggested reference architecture (see `reasoning-agents-architecture.png`) proposes a **Dispatcher → Learning Path Curator → Study Plan Generator → Engagement Agent** sequential subworkflow, followed by a human-in-the-loop gate, a Readiness Assessment Agent, and an Exam Planning Agent.

A recent large-scale empirical study from Google Research ("Towards a Science of Scaling Agent Systems", Kim et al., Dec 2025) provides quantitative scaling principles derived from 180 controlled configurations across three LLM families and four agentic benchmarks. Its findings directly inform the trade-offs we face.

---

## Decision Drivers

1. **Competition scoring** — Accuracy & Relevance (25%), Reasoning & Multi-step Thinking (25%), Creativity & Originality (15%), UX & Presentation (15%), Reliability & Safety (20%).
2. **Time constraint** — Live hackathon with limited build window; complexity must be scoped to what is demoable.
3. **Empirical evidence** — The Google Research paper provides strong, task-contingent guidance on when multi-agent coordination helps vs. hurts.
4. **Tool integration requirements** — Microsoft Learn MCP server, potential email/calendar APIs, assessment generation. Moderate tool count (~5–8 tools). MS Learn Platform API tools.
5. **Task structure** — The workflow is primarily *sequential* (each agent's output feeds the next), with limited parallelizability. The assessment → pass/fail → loop-back path adds adaptive branching but not parallel decomposition.

---

## Considered Options

### Option A: Multi-Agent Sequential Workflow (Reference Architecture)

Implement the suggested architecture faithfully: a Dispatcher agent, three sequential specialist agents (Learning Path Curator, Study Plan Generator, Engagement Agent), a Readiness Assessment Agent, and an Exam Planning Agent. Each agent is a distinct `ChatCompletionAgent` in the Microsoft Agent Framework with its own system prompt and tool bindings. Orchestration via `AgentGroupChat` with a sequential `SelectionStrategy`.

**Strengths:**

- Directly aligns with the competition's suggested architecture and "multi-agent system" submission requirement.
- Role-based specialization gives each agent a focused prompt, reducing prompt complexity per agent.
- Matches the Planner–Executor and Role-based Specialization reasoning patterns called out in the competition rubric.
- Centralized orchestration (Dispatcher as orchestrator) is the topology that the Google paper found most effective on structured, decomposable tasks (+80.8% on Finance Agent).

**Weaknesses:**

- The Google paper's most critical finding for our scenario: **every multi-agent variant degraded performance 39–70% on sequential reasoning tasks** (PlanCraft benchmark). Our workflow is fundamentally sequential — each agent depends on the prior agent's output.
- Coordination overhead is substantial. Centralized MAS requires 3.8× more reasoning turns than SAS (27.7 vs. 7.2 turns), consuming token budget on coordination rather than reasoning. With a fixed token budget during a hackathon demo, this matters.
- Error amplification: even the best MAS topology (Centralized) amplifies errors 4.4× vs. single-agent baseline. In a sequential pipeline, an error in the Learning Path Curator cascades through Study Plan and Engagement.
- The paper identifies a **capability saturation threshold at ~45% single-agent baseline accuracy** — beyond which coordination yields diminishing or negative returns (β̂ = −0.404, p < 0.001). With GPT-5.3 or Claude Opus 4.6, the single-agent baseline for this structured task is likely well above 45%.
- Engineering complexity: 5–6 agents require careful prompt engineering, state management, and debugging — significant overhead in a time-boxed hackathon.
- Efficiency collapse: MAS efficiency (success per token) drops 2–6× compared to SAS (Table 5 in the paper). Token spend matters for demo reliability and cost.

**Risk:** High probability of worse end-to-end performance than a well-prompted single agent, based on the paper's findings for sequential tasks. The overhead of coordination may consume the hackathon's limited build time without proportional quality gains.

---

### Option B: Condensed MAS — Unified Student Readiness Agent

The same overall multi-agent workflow, but the three-agent sequential subworkflow (Learning Path Curator → Study Plan Generator → Engagement Agent) is **collapsed into a single Student Readiness Agent**. This agent handles curation, planning, and engagement in one reasoning loop with access to all relevant tools. The rest of the system remains multi-agent: Dispatcher → **Student Readiness Agent** → Human-in-the-Loop → Readiness Assessment Agent → Exam Planning Agent.

This reduces the agent count from ~5 to ~3 while preserving the multi-agent orchestration at the workflow boundaries where it arguably matters (dispatching, assessment, exam planning).

**Strengths:**

- Eliminates the most problematic coordination: the three-agent sequential handoff is where the Google paper's PlanCraft findings hit hardest. Each handoff (Curator → Planner → Engagement) is a strict sequential dependency with no parallelizability — exactly the pattern that degraded 39–70% under MAS.
- The unified Student Readiness Agent maintains full context across curation, planning, and engagement. No lossy compression of the curated learning paths into an inter-agent message before the planner sees them; no re-summarization of the study plan before the engagement agent schedules reminders.
- Still a legitimate multi-agent system — the Dispatcher, Student Readiness Agent, Assessment Agent, and Exam Planning Agent are distinct agents with different roles, tools, and system prompts, orchestrated via `AgentGroupChat`.
- Reduces coordination overhead in the subworkflow from ~285% (Centralized) to 0%, while retaining coordination at the outer workflow level where the human-in-the-loop gate and pass/fail branching genuinely benefit from agent separation.
- The Google paper's efficiency data supports this directly: SAS achieves 67.7 successes/1K tokens vs. 21.5 for Centralized MAS. Consolidating the inner loop reclaims most of that efficiency.
- Error amplification drops within the subworkflow (1.0× vs. 4.4× Centralized), reducing the risk of a bad learning path recommendation cascading into an irrelevant study plan and misaligned reminders.
- Frontier models (GPT-5.3, Claude Opus 4.6) with Intelligence Index 71–75+ handle the combined curation+planning+engagement task comfortably — the capability saturation threshold (~45%) is easily exceeded, meaning coordination would yield diminishing returns anyway.

**Weaknesses:**

- The Student Readiness Agent carries a heavier system prompt and more tools than any single agent in Option A, increasing per-call token cost and prompt engineering complexity.
- Judges may view the condensed subworkflow as "less multi-agent" than the reference architecture, potentially docking Accuracy & Relevance points if they expect close alignment.
- If the unified agent's context window fills up (long learning path results + detailed study plan + engagement scheduling), later phases may suffer from attention dilution — a problem that agent separation in Option A naturally avoids by resetting context per agent.

**Risk:** Moderate. This is still clearly a multi-agent system, but the reduced agent count in the subworkflow could be perceived as cutting corners rather than making a principled design choice — unless the rationale is well-presented.

---

### Option C: Both — With Comparative Evaluation Framework (Recommended)

Build **both** Option A (full MAS) and Option B (condensed MAS), then implement a lightweight evaluation harness that runs identical student scenarios through each architecture and compares results. Present the comparison as a mini case-study that tests whether consolidating the sequential subworkflow into a single agent improves or degrades performance — grounded in the Google paper's scaling principles.

**Implementation plan:**

1. **Full MAS (Option A):** Build first. This is the primary submission and fully satisfies the competition's multi-agent requirement. Dispatcher orchestrator, three specialist sub-agents in the Student Readiness subworkflow, Assessment Agent, and Exam Planning Agent. Centralized topology via `AgentGroupChat` with sequential `SelectionStrategy`. Use a frontier model (GPT-5.3 or Claude Opus 4.6) across all agents.
2. **Condensed MAS (Option B):** Build second, reusing the same tool definitions and MCP integrations. Replace the three-agent subworkflow with a single Student Readiness Agent. Keep the outer multi-agent structure (Dispatcher, Assessment, Exam Planning) intact. Same frontier model for fair comparison.
3. **Evaluation harness:** Define 3–5 test scenarios (e.g., "Prepare for AZ-900", "Prepare for AI-102", "Prepare for SC-300"). Run each through both architectures. Measure:
   - **Task completion accuracy** — Did the system produce a valid learning path, study plan, assessment, and routing decision?
   - **Reasoning quality** — Manual rubric scoring of study plan relevance, assessment question quality, and feedback specificity.
   - **Token efficiency** — Total tokens consumed per scenario.
   - **Turn count** — Number of LLM calls per scenario.
   - **Error rate** — Factual errors in learning path recommendations or assessment questions.
   - **Latency** — Wall-clock time per scenario.
4. **Telemetry:** Use OpenTelemetry or Foundry's built-in monitoring to visualize agent interactions, tool calls, and reasoning traces for both architectures. The contrast in telemetry traces (6-agent vs. 4-agent) makes the architectural difference immediately visible.
5. **Presentation:** Demo the full MAS workflow first (primary submission). Then present the condensed MAS comparison as a "what we learned" segment, showing where the three-agent subworkflow added value vs. where it introduced overhead — grounded in the Google paper's framework.

**Strengths:**

- The full MAS build-first strategy guarantees a competition-compliant submission regardless of time pressure.
- The condensed MAS is a minimal diff from the full MAS (swap three agents for one, reuse everything else), making the second build fast once the first is working.
- Creativity & Originality: the controlled comparison isolates exactly one variable (subworkflow agent count) while holding everything else constant. No other team is likely to present an empirically grounded architectural ablation.
- Reasoning & Multi-step Thinking: the evaluation framework itself demonstrates sophisticated reasoning about when agent separation helps vs. hurts.
- Reliability & Safety: directly addresses the "avoid common pitfalls" criterion by testing the Google paper's prediction that sequential subworkflows degrade under multi-agent coordination.
- The comparison is genuinely interesting — the full MAS might win if agent specialization in the subworkflow produces higher-quality outputs despite the overhead (e.g., a curator with a tightly focused prompt might find better learning paths than a generalist agent juggling curation + planning + engagement).
- Both architectures are legitimate multi-agent systems, so even if time only permits the full MAS, the submission is strong.

**Weaknesses:**

- The condensed MAS is a smaller architectural delta than a true SAS comparison — the effect size may be modest enough to be inconclusive with only 3–5 test scenarios.
- Still requires building and debugging two orchestration configurations, though the shared tool layer reduces duplication.
- Telemetry and monitoring setup adds additional implementation time.

**Mitigation:** Build the full MAS to completion first. The condensed MAS is a refactor (delete two agents, expand one agent's prompt), not a rebuild — it can be done in a fraction of the time. If time is extremely tight, present the full MAS with a *designed but not fully executed* comparison, showing the evaluation framework and expected outcomes based on the paper's predictions.

---

## Decision

**Option C: Both, with comparative evaluation framework.**

This option maximizes competition scoring across all five criteria while producing a technically honest and empirically grounded submission. The build order (full MAS first → condensed MAS refactor → evaluation) ensures competition compliance is locked in early, with the comparison as upside.

---

## Consequences

**Positive:**

- The full MAS is built first, guaranteeing a competition-compliant demo even under worst-case time pressure.
- The condensed MAS is a surgical refactor (collapse three agents into one), not a ground-up rebuild — shared tools, MCP integrations, and outer workflow remain identical.
- The comparative framing positions the submission as research-informed rather than just "we followed the suggested architecture."
- Demonstrates mastery of the Microsoft Agent Framework by showing fluency across different orchestration granularities within the same system.
- Telemetry/evaluation components directly address the optional-but-highly-valued criteria (evaluations, monitoring, reasoning patterns).

**Negative:**

- The condensed MAS comparison is a narrower ablation (6 agents vs. 4 agents) than a full SAS-vs-MAS comparison, which may produce a smaller and harder-to-interpret effect size.
- Less time for polish on the primary full MAS implementation if the comparison work expands.

**Neutral:**

- The Google paper predicts the three-agent sequential subworkflow will underperform a single unified agent on the curation → planning → engagement pipeline (closest analog: PlanCraft, where all MAS variants degraded 39–70%). However, the subworkflow agents have genuinely distinct tool sets and domain focus — the Curator queries Microsoft Learn MCP, the Planner does scheduling logic, and the Engagement Agent handles email/reminder APIs. This specialization *might* justify the separation in a way that PlanCraft's uniform crafting agents could not. The result is genuinely uncertain, which makes the comparison worth running.

---

## Technical Notes

**Key paper findings driving this decision:**

| Finding | Relevance to Our Task |
|---|---|
| Sequential tasks degrade 39–70% under MAS | The Student Readiness subworkflow (curator → planner → engagement) is strictly sequential — prime candidate for consolidation |
| Capability saturation at ~45% SAS baseline | Frontier models (GPT-5.3, Claude Opus 4.6) likely exceed this threshold for the subworkflow tasks |
| Centralized MAS: +80.8% on parallelizable tasks | The *outer* workflow (dispatch → readiness → assessment → exam planning) has genuine branching and role differentiation — keep this multi-agent |
| Tool-coordination trade-off (β̂ = −0.267) | Moderate tool count (~5–8) puts us in the zone where subworkflow overhead matters but outer-workflow overhead is manageable |
| Error amplification: 4.4× (Centralized) vs. 1.0× (SAS) | The sequential subworkflow is where error cascade risk is highest — consolidation eliminates the two handoff points (Curator→Planner, Planner→Engagement) |
| Optimal overhead band: 200–300% | Full MAS likely exceeds this; condensed MAS should fall within it by eliminating two inter-agent message exchanges |

**Predicted outcome:** The condensed MAS outperforms the full MAS on subworkflow quality (study plan coherence, reminder relevance) and token efficiency. The full MAS may show advantages if the Curator agent's focused prompt produces better Microsoft Learn search results than a generalist Student Readiness Agent juggling all three concerns.

---

## References

- Kim, Y. et al. "Towards a Science of Scaling Agent Systems." arXiv:2512.08296v2, Dec 2025.
- Microsoft Agent Framework: https://github.com/microsoft/agent-framework
- Microsoft Learn MCP Server: https://github.com/microsoftdocs/mcp
- Competition brief: `copilot-instructions.md`
