---
name: agent-framework-dotnet
description: |
  Create AI agents and workflows using Microsoft Agent Framework SDK. Supports single-agent and multi-agent workflow patterns.
  USE FOR: create agent, build agent, scaffold agent, new agent, agent framework, workflow pattern, multi-agent, MCP tools, create workflow.
  DO NOT USE FOR: deploying agents (use agent/deploy), evaluating agents (use agent/evaluate), Azure AI Foundry agents without Agent Framework SDK.
---

# Create Agent with Microsoft Agent Framework

Build AI agents, agentic apps, and multi-agent workflows using Microsoft Agent Framework SDK.

## Quick Reference

| Property | Value |
|----------|-------|
| **SDK** | Microsoft Agent Framework (.NET/C#) |
| **Patterns** | Single Agent, Multi-Agent Workflow |
| **Server** | ASP.NET Core HTTP hosting |
| **Debug** | AI Toolkit Agent Inspector + VSCode |
| **Best For** | Enterprise agents with type safety, checkpointing, orchestration |

## When to Use This Skill

Use when the user wants to:

- **Create** a new AI agent or agentic application
- **Scaffold** an agent with tools (MCP, function calling)
- **Build** multi-agent workflows with orchestration patterns
- **Add** HTTP server mode to an existing agent
- **Configure** F5/debug support for VSCode

## Defaults

- **Language**: C#
- **SDK**: Microsoft Agent Framework (.NET) (use latest stable/preview)
- **Server**: HTTP via ASP.NET Core hosting
- **Environment**: .NET SDK project (create or detect existing)

## References

| Topic | File | Description |
|-------|------|-------------|
| Server Pattern | [references/agent-as-server.md](references/agent-as-server.md) | HTTP server wrapping (production) |
| Debug Setup | [references/debug-setup.md](references/debug-setup.md) | VS Code configs for Agent Inspector |
| Agent Samples | [references/agent-samples.md](references/agent-samples.md) | Single agent, tools, MCP, threads |
| Workflow Basics | [references/workflow-basics.md](references/workflow-basics.md) | Executor types, handler signatures, edges, WorkflowBuilder — start here for any workflow |
| Workflow Agents | [references/workflow-agents.md](references/workflow-agents.md) | Agents as executor nodes, linear pipeline, run_stream event consumption |
| Workflow Foundry | [references/workflow-foundry.md](references/workflow-foundry.md) | Foundry agents with bidirectional edges, loop control, register_executor factories |
| Latest Sample Links | [references/agent-examples-latest-links.md](references/agent-examples-latest-links.md) | Authoritative latest .NET sample folders (Agents, FoundryAgents, Workflows) |

> 💡 **Tip:** For advanced patterns (Reflection, Switch-Case, Fan-out/Fan-in, Loop, Human-in-Loop), search `microsoft/agent-framework` on GitHub.

## MCP Tools

This skill delegates to `microsoft-foundry` MCP tools for model and project operations:

| Tool | Purpose |
|------|---------|
| `foundry_models_list` | Browse model catalog for selection |
| `foundry_models_deployments_list` | List deployed models for selection |
| `foundry_resource_get` | Get project endpoint |

## Creation Workflow

1. Gather context (read agent-as-server.md + debug-setup.md + code samples)
2. Select model & configure environment
3. Implement agent/workflow code + HTTP server mode + `.vscode/` configs
4. Install dependencies (`dotnet add package` + restore)
5. Verify startup (Run-Fix loop)
6. Documentation

### Step 1: Gather Context

Read reference files based on user's request:

**Always read these references:**
- Server pattern: **agent-as-server.md** (required — HTTP server is the default)
- Debug setup: **debug-setup.md** (required — always generate `.vscode/` configs)

**Read the relevant code sample:**
- Code samples: agent-samples.md, workflow-basics.md, workflow-agents.md, or workflow-foundry.md
- Latest links index: **agent-examples-latest-links.md** (required source of truth when API names differ)

**Model Selection**: Use `microsoft-foundry` skill's model catalog to help user select and deploy a model.

**Recommended**: Search `microsoft/agent-framework` on GitHub for advanced patterns.

### Step 2: Select Model & Configure Environment

*Decide on the model BEFORE coding.*

If user hasn't specified a model, use `microsoft-foundry` skill to list deployed models or help deploy one.

**ALWAYS create/update `.env` file**:
```bash
FOUNDRY_PROJECT_ENDPOINT=<project-endpoint>
FOUNDRY_MODEL_DEPLOYMENT_NAME=<model-deployment-name>
OPENAI_API_KEY=<openai-api-key>
OPENAI_MODEL=<openai-model>
```

- **Standard flow**: Populate with real values from user's provider/project
- **Deferred Config**: Use placeholders, remind user to update before running

### Step 3: Implement Code

**All three are required by default:**

1. **Agent/Workflow code**: Use gathered context to structure the agent or workflow
2. **HTTP Server mode**: Wrap with Agent-as-Server pattern from `agent-as-server.md` — this is the default entry point
3. **Debug configs**: Generate `.vscode/launch.json` and `.vscode/tasks.json` using templates from `debug-setup.md`

> ⚠️ **Warning:** Only skip server mode or debug configs if the user explicitly requests a "minimal" or "no server" setup.

### Step 4: Install Dependencies

1. Add/update package references in the project file (or use `dotnet add package`)
  ```bash
  # prefer package versions from the chosen latest sample's .csproj
  # (do not invent versions; keep package family versions aligned)
  dotnet add package <package-from-sample>
  ```

2. Restore dependencies:
  ```bash
  dotnet restore
  ```

> ⚠️ **Warning:** Avoid hard-pinning preview build numbers unless user explicitly asks for reproducible CI lockstep.
> ⚠️ **Warning:** If API names in local snippets differ from repo samples, follow the sample code from `references/agent-examples-latest-links.md`.

### Step 5: Verify Startup (Run-Fix Loop)

Enter a run-fix loop until no startup errors:

1. Run the main entrypoint (e.g., `dotnet run`)
2. **If startup fails**: Fix error → Rerun
3. **If startup succeeds**: Stop server immediately

**Guardrails**:
- ✅ Perform real run to catch startup errors
- ✅ Cleanup after verification (stop HTTP server)
- ✅ Ignore environment/auth/connection/timeout errors
- ❌ Don't wait for user input
- ❌ Don't create separate test scripts
- ❌ Don't mock configuration

### Step 6: Documentation

Create/update `README.md` with setup instructions and usage examples.

## Error Handling

| Error | Cause | Resolution |
|-------|-------|------------|
| `CS0246` type/namespace not found | Missing SDK package | Add required Agent Framework package and run `dotnet restore` |
| API type/member not found | SDK drift across previews | Align sample code with latest Learn docs and package set |
| Agent name validation error | Invalid characters | Use alphanumeric + hyphens, start/end with alphanumeric, max 63 chars |
| Credential/auth failure | Missing provider credentials | Configure `.env`/User Secrets for selected provider and rerun |

## Latest Official Documentation

- Learn hub: https://learn.microsoft.com/en-us/agent-framework/
- Overview (C#): https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp
- Get started: https://learn.microsoft.com/en-us/agent-framework/get-started/your-first-agent
- Agents (C#): https://learn.microsoft.com/en-us/agent-framework/agents/?pivots=programming-language-csharp
- Integrations (C#): https://learn.microsoft.com/en-us/agent-framework/integrations/?pivots=programming-language-csharp
- Official .NET samples: https://github.com/microsoft/agent-framework/tree/main/dotnet/samples
- Latest sample index in this skill: [references/agent-examples-latest-links.md](references/agent-examples-latest-links.md)
