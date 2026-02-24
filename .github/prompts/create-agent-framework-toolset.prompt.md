---
agent: 'Microsoft Agent Framework .NET'
description: 'Generate proper creation of tool set classes for the Microsoft Agent Framework, following best practices for tool design, metadata, and registration.'
argument-hint: 'Describe the toolset you want to create, including the domain it covers, the services it should integrate with, and any specific tools or actions it should provide.'
model: 'GPT-5.3-Codex'
---

## You are writing
C# tool set classes ("toolsets") for **Microsoft Agent Framework** agents.

## Goal
Given a domain (for example: "Catalog", "Weather", "Orders") and a set of services already available via DI, generate a clean, production-ready toolset that can be registered and passed to an agent as tools.

## Canonical APIs to use (do not invent alternatives)
- Tools are created from .NET methods using `Microsoft.Extensions.AI.AIFunctionFactory.Create(...)`.
- Tool and parameter descriptions come from `System.ComponentModel.DescriptionAttribute` on the method and its parameters. 
- Pass tools to the agent via `AsAIAgent(..., tools: [ ... ])` (list of `AITool` / `AIFunction`). 
- For tools that should require human approval before execution, wrap the created function in `ApprovalRequiredAIFunction`.   
- If a tool needs access to ambient context or DI at invocation time, support `AIFunctionArguments` / `IServiceProvider` parameters (these are specially bound by the library). 

## Output requirements (what you MUST generate)
When asked to create tools for a domain, output ALL of the following:

1. **Toolset class**: `public sealed class {Domain}ToolSet`
2. **Tool materialization**: method that returns `AITool[]` or `List<AITool>` for that toolset


Keep the code minimal but complete enough to paste into a .NET app.

## Toolset class design rules (strict)
- Use **constructor injection** for dependencies (HttpClient, repositories, SDK clients, loggers, etc.).
- Tool methods must be **public** instance methods (prefer instance over static to keep DI clean).
- Tool methods must be **single-purpose** and narrow:
  - One action per method.
  - No "do everything" method with multiple unrelated parameters.
- Tool methods must be **safe to call by an LLM**:
  - Validate inputs early (null, empty, bounds, regex, enum ranges).
  - Prefer returning a structured result DTO for user-facing errors rather than throwing for expected validation failures.
  - Never leak secrets in return values, logs, or exception messages.
- Tool methods must be **JSON-serializable**:
  - Parameters and return types should be primitives, enums, `record` DTOs, arrays/lists, dictionaries of simple types.
  - Avoid `Stream`, `HttpResponseMessage`, complex framework types, or circular graphs.
- Support **cancellation**:
  - If async, accept an optional `CancellationToken cancellationToken = default`.
  - Pass it through to downstream async calls. :contentReference[oaicite:6]{index=6}
- Prefer `Task<T>` async methods for I/O; do not block.
- Do not do console I/O; use `ILogger<T>` if logging is needed.

## Metadata rules (Descriptions matter)
- Every tool method MUST have `[Description("...")]` written for an LLM, not for developers.
  - Start with an imperative verb.
  - Say what it does and what it returns.
  - Mention constraints or side effects.
- Every parameter MUST have `[Description("...")]` and include examples where helpful.

## Tool creation rules (how to build the AITool list)
- Create tools using `AIFunctionFactory.Create(toolset.SomeMethod)` for each method.
- If a method is side-effectful (writes, deletes, sends messages, charges money), wrap it with:
  - `new ApprovalRequiredAIFunction(AIFunctionFactory.Create(toolset.SomeDangerousMethod))` :contentReference[oaicite:7]{index=7}
- If you need custom names/descriptions beyond attributes, use `AIFunctionFactoryOptions` to override metadata.
- If the tool needs DI at invocation time and you cannot easily scope the toolset instance, prefer patterns that allow `AIFunctionArguments.Services` binding rather than building a service locator.

## Naming and structure conventions
- Namespace: `{CompanyOrProduct}.Agents.Tools.{Domain}`
- Class name: `{Domain}ToolSet`
- File names:
  - `{Domain}ToolSet.cs`
  - `{Domain}ToolSetServiceCollectionExtensions.cs`
- Public tool method naming:
  - VerbNoun: `FindExam`, `GetWeather`, `SearchCatalog`, `CreateOrderDraft`
  - Async methods end with `Async`

## Return type patterns (recommended)
- For query tools: return DTOs like `SearchResult<T>` or `IReadOnlyList<TDto>`.
- For action tools: return `ActionResultDto { bool Succeeded; string? Message; ... }`.
- For validation: prefer returning a structured "failed" result with a human message.

## Security and safety defaults
- Any method that modifies external state defaults to approval required.
- Disallow wildcard operations by default (for example: delete all).
- Require explicit identifiers and confirm existence before destructive actions.
- Never accept raw SQL; never accept raw filesystem paths; never accept arbitrary URLs unless you validate allow-lists.

---

# TEMPLATE OUTPUT

## 1) Toolset class

```csharp
using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace YourApp.Agents.Tools.{{Domain}};

public sealed class {{Domain}}ToolSet
{
    private readonly ILogger<{{Domain}}ToolSet> _logger;
    private readonly I{{Domain}}Client _client;

    public {{Domain}}ToolSet(I{{Domain}}Client client, ILogger<{{Domain}}ToolSet> logger)
    {
        _client = client;
        _logger = logger;
    }
    // All toolsets should have a method like this to return their tools for agent registration
    public List<AITool> GetTools()
    {
        return new List<AITool>
        {
            AIFunctionFactory.Create(SearchAsync),
            AIFunctionFactory.Create(CreateDraftAsync)
        };
    }

    [Description("Search the {{domain}} for items that match the given query and return a short list of results.")]
    public async Task<IReadOnlyList<{{Domain}}ItemDto>> SearchAsync(
        [Description("Search query text. Example: \"oauth invalid_client\"")] string query,
        [Description("Maximum number of results to return. Range: 1 to 25.")] int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<{{Domain}}ItemDto>();

        if (maxResults < 1) maxResults = 1;
        if (maxResults > 25) maxResults = 25;

        var results = await _client.SearchAsync(query, maxResults, cancellationToken).ConfigureAwait(false);
        return results ?? Array.Empty<{{Domain}}ItemDto>();
    }

    [Description("Create a draft {{domain}} item based on the provided fields. Returns the draft identifier and status message.")]
    public async Task<ActionResultDto> CreateDraftAsync(
        [Description("Title for the draft item. Example: \"Quarterly report\"")] string title,
        [Description("Optional notes or description for the draft.")] string? notes = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return ActionResultDto.Failed("Title is required.");

        var draftId = await _client.CreateDraftAsync(title.Trim(), notes, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(draftId)
            ? ActionResultDto.Failed("Draft could not be created.")
            : ActionResultDto.Succeeded($"Draft created: {draftId}", draftId);
    }

    // NOTE: Any side-effectful or risky method should be wrapped with ApprovalRequiredAIFunction at registration time.
}
```

## 2) DTOs (keep JSON-friendly)

```csharp
namespace YourApp.Agents.Tools.{{Domain}};

public sealed record {{Domain}}ItemDto(
    string Id,
    string Title,
    string? Summary);

public sealed record ActionResultDto(
    bool Succeeded,
    string Message,
    string? Id = null)
{
    public static ActionResultDto Failed(string message) => new(false, message);
    public static ActionResultDto Succeeded(string message, string? id = null) => new(true, message, id);
}
```

## 3) Usage snippet (agent creation)

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var tools = new {{Domain}}ToolSet().GetTools();

AIAgent agent = chatClient.AsAIAgent(
    name: "{{Domain}}Agent",
    description: "Agent that can use {{domain}} tools to answer questions and take approved actions.",
    instructions: "Use tools when helpful. Ask for approval before risky actions.",
    tools: tools);
```

---

## Quality bar checklist (Copilot must satisfy)

* Every tool method has `[Description]` and every parameter has `[Description]`.
* Methods validate inputs and never expose secrets.
* Return types are JSON-friendly DTOs.
* Async I/O uses `Task<T>` and passes `CancellationToken`.
* Tool materialization uses `AIFunctionFactory.Create(...)`.
* Side-effectful tools are wrapped for approval.
* Includes DI registration and an example showing `AsAIAgent(..., tools: ...)`.



