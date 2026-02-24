# Agent / Workflow Debugging

Support debugging for agent-framework-based agents or workflows locally in VSCode.

For agent as HTTP server, use ASP.NET Core hosting and AI Toolkit Agent Inspector for interactive debugging and testing, supporting:
- agent and workflow execution
- visualize interactions and message flows
- monitor and trace multi-agent orchestration patterns
- troubleshoot complex workflow logic

(This doc applies to .NET/C# SDK only)

> ⚠️ **Source of truth:** Use startup/debug shape from the selected sample in [agent-examples-latest-links.md](agent-examples-latest-links.md).

## Prerequisites

- (REQUIRED) Agent or workflow created using agent-framework SDK
- (REQUIRED) Running in HTTP server mode, i.e., using ASP.NET Core hosting packages

## SDK Installations

Install dependencies using NuGet:

```bash
# keep versions aligned with the selected latest sample .csproj
dotnet add package <package-from-sample>
dotnet restore
```

## Launch Command

The agent/workflow can run in HTTP server mode or CLI mode, depending on implementation.

(Important) By default use HTTP server mode for full local-debug and inspector features. If code supports CLI mode, use it for simpler debugging.

```bash
# HTTP server mode sample launch command
dotnet run

# Optional CLI mode sample launch command
dotnet run -- --cli
```

## Example

Example configuration files for VSCode to enable debugging support.

### tasks.json

Run agent with debugging enabled.

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "Validate prerequisites",
      "type": "aitk",
      "command": "debug-check-prerequisites",
      "args": {
        "portOccupancy": [8087]
      }
    },
    {
      "label": "Run Agent/Workflow HTTP Server",
      "type": "shell",
      "command": "dotnet run",
      "isBackground": true,
      "options": { "cwd": "${workspaceFolder}" },
      "dependsOn": ["Validate prerequisites"],
      "problemMatcher": {
        "pattern": [{ "regexp": "^.*$", "file": 0, "location": 1, "message": 2 }],
        "background": {
          "activeOnStart": true,
          "beginsPattern": ".*",
          "endsPattern": "Now listening on|Application started"
        }
      }
    },
    {
      "label": "Open Agent Inspector",
      "type": "shell",
      "command": "echo '${input:openAgentInspector}'",
      "presentation": { "reveal": "never" },
      "dependsOn": ["Run Agent/Workflow HTTP Server"]
    },
    {
      "label": "Run Agent/Workflow in Terminal",
      "type": "shell",
      "command": "dotnet run -- --cli",
      "isBackground": true,
      "options": { "cwd": "${workspaceFolder}" },
      "problemMatcher": {
        "pattern": [{ "regexp": "^.*$", "file": 0, "location": 1, "message": 2 }],
        "background": {
          "activeOnStart": true,
          "beginsPattern": ".*",
          "endsPattern": "Now listening on|Application started"
        }
      }
    },
    {
      "label": "Terminate All Tasks",
      "command": "echo ${input:terminate}",
      "type": "shell",
      "problemMatcher": []
    }
  ],
  "inputs": [
    {
      "id": "openAgentInspector",
      "type": "command",
      "command": "ai-mlstudio.openTestTool",
      "args": { "triggeredFrom": "tasks", "port": 8087 }
    },
    {
      "id": "terminate",
      "type": "command",
      "command": "workbench.action.tasks.terminate",
      "args": "terminateAll"
    }
  ]
}
```

### launch.json

Launch debugger for the running agent/workflow.

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Debug Local Agent/Workflow HTTP Server",
      "type": "coreclr",
      "request": "launch",
      "program": "${workspaceFolder}/bin/Debug/net8.0/<your-app>.dll",
      "cwd": "${workspaceFolder}",
      "preLaunchTask": "Open Agent Inspector",
      "postDebugTask": "Terminate All Tasks"
    },
    {
      "name": "Debug Local Agent/Workflow in Terminal",
      "type": "coreclr",
      "request": "launch",
      "program": "${workspaceFolder}/bin/Debug/net8.0/<your-app>.dll",
      "args": ["--cli"],
      "cwd": "${workspaceFolder}",
      "preLaunchTask": "Run Agent/Workflow in Terminal",
      "postDebugTask": "Terminate All Tasks"
    }
  ]
}
```
