# Agents League Reasoning Agents

Multi-agent Microsoft certification study assistant built with the Microsoft Agent Framework (.NET), with a Blazor demo UI, evaluation runner, and reminder email function.

## What this solution does

The solution supports a student prep flow for Microsoft certification exams:

1. **Preparation**
   - Curates relevant learning paths/modules from Microsoft Learn.
   - Generates a weekly + daily study plan.
   - Generates reminders/motivation messages and persists/schedules them.
2. **Assessment**
   - Builds a 10-question readiness assessment.
   - Scores responses and determines whether the student is exam-ready.

## Projects

- `AgentsLeagueReasoningAgents/` – core agents, workflows, toolsets, services, models.
- `AgentsLeagueReasoningAgents.Demo/` – Blazor Server app for running preparation + assessment.
- `AgentsLeagueReasoningAgents.Evals/` – dataset-driven evaluation runner.
- `AgentsLeagueReasoningAgents.EmailFunction/` – Azure Functions isolated worker that sends scheduled reminder emails from a Service Bus queue.
- `MSLearnPlatformClient/` – typed client for Microsoft Learn catalog APIs.

## Architecture (high-level)

![Reasoning Agents Architecture](./images/Very_Professional_Arch_Diagram.png)

Core flow is orchestrated via `IPreparationWorkflowService` and `IAssessmentWorkflowService`:

- `PreparationWorkflowService.RunPreparationAsync(...)` executes three agents sequentially:
  1. `learning-path-curator`
  2. `study-plan-generator`
  3. `engagement-agent`
- `AssessmentWorkflowService` starts and manages the readiness assessment session.

Agent creation and tool wiring live in `PreparationAgentFactory`.

## Demo screenshots

### Start page

![Start page](./images/Start.png)

### Tool details

![Tool details](./images/ToolDetails.png)

### Tool modal

![Tool modal](./images/ToolModal.png)

### Output example

![Output example](./images/OutputExample.png)

### Assessment agent

![Assessment agent](./images/AssessmentAgent.png)

## In-scope tool integrations

### 1) `LearnCatalogToolset`

Purpose: query Microsoft Learn catalog content using `ILearnCatalogClient`, then apply LLM-based relevance filtering.

Exposed AI tools:
- `SearchLearningPathsAsync(...)`
- `SearchModulesAsync(...)`
- `SearchCertificationsAndExamsAsync(...)`
- `GetModulesAsync(...)`

Implementation notes:
- Uses catalog APIs (learning paths, modules, exams, certifications).
- Uses an internal filter agent (`CreateFilterAgent<TOutput>`) to rank/select most relevant items.
- Returns JSON (schema-constrained by typed response models).

### 2) `MicrosoftLearnMcpToolset`

Purpose: connect to the Microsoft Learn MCP endpoint and expose all discovered MCP tools as `AITool`s.

Implementation notes:
- Creates a singleton `McpClient` via `HttpClientTransport`.
- Calls `ListToolsAsync()` and returns them to agent toolsets.
- Endpoint/name are configured via `MicrosoftLearnMcpOptions`.

## Optional tool integrations

The project also includes optional enrichment toolsets in `AgentsLeagueReasoningAgents/Tools/Optional`:

- [`ExamTopicsToolset`](./AgentsLeagueReasoningAgents/Tools/Optional/ExamTopicsToolset.cs) – checks community-maintained free certification offers and voucher promotions.
- [`GitHubAnkiDeckToolset`](./AgentsLeagueReasoningAgents/Tools/Optional/GitHubAnkiDeckToolset.cs) – extracts flashcards from open-source Anki decks for exam prep.
- [`GitHubCommunityHubToolset`](./AgentsLeagueReasoningAgents/Tools/Optional/GitHubCommunityHubToolset.cs) – pulls curated community resources (courses, labs, practice tests, videos, study guides).
- [`GitHubExamSyllabiToolset`](./AgentsLeagueReasoningAgents/Tools/Optional/GitHubExamSyllabiToolset.cs) – returns structured exam syllabus/topic weights from a community JSON source (with freshness warning).
- [`GitHubPracticeQuestionsToolset`](./AgentsLeagueReasoningAgents/Tools/Optional/GitHubPracticeQuestionsToolset.cs) – searches community-maintained practice question banks for selected exams.
- [`GitHubStudyNotesToolset`](./AgentsLeagueReasoningAgents/Tools/Optional/GitHubStudyNotesToolset.cs) – retrieves structured study notes from mapped GitHub repositories.
- [`OfficialLabExercisesToolset`](./AgentsLeagueReasoningAgents/Tools/Optional/OfficialLabExercisesToolset.cs) – loads official MIT-licensed MicrosoftLearning lab instructions.
- [`PodcastFeedToolset`](./AgentsLeagueReasoningAgents/Tools/Optional/PodcastFeedToolset.cs) – searches Microsoft-focused podcast episodes relevant to exam topics.
- [`StackExchangeToolset`](./AgentsLeagueReasoningAgents/Tools/Optional/StackExchangeToolset.cs) – searches Stack Overflow Q&A for exam-related technical topics.
- [`YouTubeStudyContentToolset`](./AgentsLeagueReasoningAgents/Tools/Optional/YouTubeStudyContentToolset.cs) – searches curated YouTube study videos and can optionally include transcripts.

## Prerequisites

- .NET SDK 10 preview (`net10.0` targets)
- Azure OpenAI deployment (or OpenAI API key for fallback mode)
- Microsoft Learn MCP endpoint access (default public endpoint is used)
- For reminder scheduling/sending:
  - Azure Service Bus
  - Azure Cosmos DB
  - Azure Communication Services Email

## Configuration

Use user secrets and/or environment variables for secrets. Do **not** commit real credentials.

### Minimum for core preparation/assessment

- `AzureOpenAI:Endpoint`
- `AzureOpenAI:DeploymentName`
- `AzureOpenAI:ApiKey`
- `MicrosoftLearnMcp:Endpoint` (default: `https://learn.microsoft.com/api/mcp`)
- `MicrosoftLearnMcp:Name` (default: `Microsoft Learn MCP`)
- `LearnCatalog:*` (base URI, locale, retry/timeouts)

Optional if `LearnCatalog` is configured for Entra auth:
- `AzureAD:TenantId`
- `AzureAD:ClientId`
- `AzureAD:ClientSecret`

### Additional settings for persistence and reminders

- `ConnectionStrings:ReminderDb`
- `ConnectionStrings:ServiceBus`
- `Reminders:QueueName` (defaults to `reminders` in DI if omitted)
- Email Function app values (`COMMUNICATION_SERVICES_CONNECTION_STRING`, `EMAIL_SENDER`)

## Run the demo app

From repository root:

```powershell
dotnet restore
dotnet run --project .\AgentsLeagueReasoningAgents.Demo\AgentsLeagueReasoningAgents.Demo.csproj
```

Open the local URL shown in console, then:
- enter topics/email/hours/weeks on `/`
- run preparation workflow
- open `/assessment?email=<student-email>` via the UI action

## Run evaluations

From repository root:

```powershell
dotnet run --project .\AgentsLeagueReasoningAgents.Evals\AgentsLeagueReasoningAgents.Evals.csproj
```

Useful args:

```powershell
dotnet run --project .\AgentsLeagueReasoningAgents.Evals\AgentsLeagueReasoningAgents.Evals.csproj -- --max-cases-per-agent 1
dotnet run --project .\AgentsLeagueReasoningAgents.Evals\AgentsLeagueReasoningAgents.Evals.csproj -- --max-concurrency 4
```

Reports are written under `AgentsLeagueReasoningAgents.Evals/Datasets/reports/` unless `--output` is provided.

## Run email function locally (optional)

```powershell
dotnet run --project .\AgentsLeagueReasoningAgents.EmailFunction\AgentsLeagueReasoningAgents.EmailFunction.csproj
```

Requires valid `local.settings.json` values for Service Bus, Cosmos DB, and ACS Email.

## Notes

- Core orchestration and prompts are in `AgentsLeagueReasoningAgents/Workflows` and `AgentsLeagueReasoningAgents/Agents`.
- This repository currently contains placeholder or test configuration values in some appsettings files; replace with secure local secrets before use.