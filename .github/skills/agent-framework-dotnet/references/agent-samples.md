# C# Agent Code Samples

## Common Patterns

These patterns are shared across all providers. Define them once and reuse.

> ⚠️ **Source of truth:** Use the exact folders in [agent-examples-latest-links.md](agent-examples-latest-links.md). If a symbol here differs from a linked sample, use the linked sample.

### Tool Definition
```csharp
using System.ComponentModel;

public static class WeatherTools
{
    [Description("Get the weather for a given location.")]
    public static string GetWeather(
        [Description("The location to get the weather for.")] string location)
    {
        var conditions = new[] { "sunny", "cloudy", "rainy", "stormy" };
        var random = Random.Shared;
        return $"The weather in {location} is {conditions[random.Next(0, 4)]} with a high of {random.Next(10, 31)}°C.";
    }
}
```

### MCP Tools Setup
```csharp
// Use the MCP sample in:
// FoundryAgents_Step09_UsingMcpClientAsTools
// from agent-examples-latest-links.md
// Keep tool construction exactly as in the sample for your current SDK version.
```

### Thread Pattern (Multi-turn Conversation)
```csharp
var session = await agent.CreateSessionAsync();

await foreach (var update in agent.RunStreamingAsync("What's the weather like in Seattle?", session))
{
    if (update is StreamingChatMessageUpdate message)
    {
        Console.Write(message);
    }
}

await foreach (var update in agent.RunStreamingAsync("Pardon?", session))
{
    if (update is StreamingChatMessageUpdate message)
    {
        Console.Write(message);
    }
}
```

---

## OpenAI

Connect to OpenAI using API key and model, then create an agent.

```csharp
// Bootstrap provider and agent from:
// Agents/Agent_Step01_Running (and related steps)
// in agent-examples-latest-links.md
var agent = /* create agent exactly as shown in selected sample */;

var session = await agent.CreateSessionAsync();
await foreach (var update in agent.RunStreamingAsync("hello", session))
{
    if (update is StreamingChatMessageUpdate message)
    {
        Console.Write(message);
    }
}
```

---

## Important Tips

Agent Framework supports various implementation patterns. These tips help avoid common errors:

- Keep package versions aligned across `Microsoft.Agents.*` dependencies to avoid API mismatch.
- Agent instance shape and bootstrap APIs can change across previews; always prefer the selected sample folder over memorized symbol names.
- If connecting to Foundry, use a valid agent name: start/end alphanumeric, hyphens allowed, max 63 characters.
