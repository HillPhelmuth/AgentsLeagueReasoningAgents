---
agent: 'Microsoft Agent Framework .NET'
description: 'Generate specific non-MSLearn tool sets for the Microsoft Agent Framework, following best practices for tool design, metadata, and registration.'
model: 'GPT-5.3-Codex'
---

# Instructions: Build AI Toolsets for Microsoft Certification Study Resources

## Purpose

You are a coding agent tasked with building a **C# class library of AI toolsets** that expose free, programmatically accessible Microsoft Certification study resources as agent tools. These toolsets will be consumed by agents built with the **Microsoft Agent Framework (C# SDK)** to help users prepare for Microsoft Certification exams.

Each toolset is a class that implements the following interface:

```csharp
public interface IAIToolset
{
    Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default);
}
```

**Excluded from scope**: Microsoft's own Catalog REST API and Microsoft Learn MCP Server — the consumer of this library already uses those directly.

When a toolset class is completed, add it to the appropriate agent in `AgentsLeagueReasoningAgents\Agents\PreparationAgentFactory.cs`
---


### Toolset Implementation Pattern

Each toolset class follows this structure. Use `AIFunctionFactory.Create` to wrap methods as `AITool` instances, with `AIFunctionFactoryCreateOptions` to provide name and description metadata:

```csharp
using Microsoft.Extensions.AI;

public class ExampleToolset : IAIToolset
{
    private readonly ExampleService _service;

    public ExampleToolset(ExampleService service)
    {
        _service = service;
    }

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                DoSomethingAsync,
                new AIFunctionFactoryCreateOptions
                {
                    Name = "tool_name",
                    Description = "What this tool does."
                })
        };

        return Task.FromResult<IReadOnlyList<AITool>>(tools);
    }

    private async Task<string> DoSomethingAsync(
        [Description("Description of the parameter")] string param,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.DoSomethingAsync(param, cancellationToken);
        return JsonSerializer.Serialize(result);
    }
}
```

### DI Registration (Host Application)

In the consuming host application's `Program.cs`:

```csharp

var builder = Host.CreateApplicationBuilder(args);

// Bind configuration sections
builder.Services.Configure<GitHubOptions>(builder.Configuration.GetSection("GitHub"));
builder.Services.Configure<YouTubeOptions>(builder.Configuration.GetSection("YouTube"));
builder.Services.Configure<StackExchangeOptions>(builder.Configuration.GetSection("StackExchange"));

// Register HTTP clients with Polly policies
builder.Services.AddHttpClient("GitHub", client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("CertStudyAgent/1.0");
    // Add GitHub PAT if configured: client.DefaultRequestHeaders.Authorization = ...
});

builder.Services.AddHttpClient("YouTube", client =>
{
    client.BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/");
});

builder.Services.AddHttpClient("StackExchange", client =>
{
    client.BaseAddress = new Uri("https://api.stackexchange.com/2.3/");
});

// Register services
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<GitHubContentService>();
builder.Services.AddSingleton<YouTubeService>();
builder.Services.AddSingleton<StackExchangeService>();
builder.Services.AddSingleton<PodcastFeedService>();
builder.Services.AddSingleton<MarkdownParserService>();
builder.Services.AddSingleton<AnkiExtractorService>();

var host = builder.Build();

// Collect all tools from all registered toolsets
var toolsets = host.Services.GetRequiredService<IEnumerable<IAIToolset>>();
var allTools = new List<AITool>();
foreach (var toolset in toolsets)
{
    allTools.AddRange(await toolset.GetToolsAsync());
}

// Pass allTools to your agent
```

### appsettings.json

```json
{
  "GitHub": {
    "PersonalAccessToken": "",
    "RateLimitPerHour": 5000,
    "CacheDurationMinutes": 60
  },
  "YouTube": {
    "ApiKey": "",
    "DailyQuotaUnits": 10000,
    "CacheDurationMinutes": 60
  },
  "StackExchange": {
    "AppKey": "",
    "CacheDurationMinutes": 15
  },
  "Podcasts": {
    "CacheDurationMinutes": 30
  }
}
```

---

## Toolset Specifications

Each section below defines one `IAIToolset` implementation. Each class exposes one or more `AITool` instances via `GetToolsAsync`. Use `AIFunctionFactory.Create` to register individual methods as tools, and `[Description]` attributes on parameters to provide metadata to the agent runtime.

---

### Toolset 1: `GitHubPracticeQuestionsToolset`

**Exposes tool**: `search_practice_questions`

**Source**: Ditectrev GitHub repositories (Markdown Q&A in README files)

**Description**: Searches community-maintained practice question banks for Microsoft certification exams. Returns multiple-choice questions with answer explanations sourced from Ditectrev's open-source repos (57 repos, 8+ Microsoft exam coverage).

**Repository Map** (configure in appsettings.json):

```json
{
  "Ditectrev": {
    "Repos": {
      "AZ-900": "Ditectrev/Microsoft-Azure-AZ-900-Microsoft-Azure-Fundamentals-Practice-Tests-Exams-Questions-Answers",
      "AZ-104": "Ditectrev/Microsoft-Azure-AZ-104-Microsoft-Azure-Administrator-Practice-Tests-Exams-Questions-Answers",
      "AZ-204": "Ditectrev/Microsoft-Azure-AZ-204-Developing-Solutions-for-Microsoft-Azure-Practice-Tests-Exams-QA",
      "AZ-305": "Ditectrev/Microsoft-Azure-AZ-305-Designing-Microsoft-Azure-Infrastructure-Solutions-Practice-Tests-Exams-QA",
      "AZ-400": "Ditectrev/Microsoft-Azure-AZ-400-Designing-and-Implementing-Microsoft-DevOps-Solutions-Practice-Tests-Exams-QA",
      "AZ-500": "Ditectrev/Microsoft-Azure-AZ-500-Azure-Security-Engineer-Practice-Tests-Exams-Questions-Answers",
      "AZ-800": "Ditectrev/Microsoft-Azure-AZ-800-Windows-Server-Hybrid-Administrator-Practice-Tests-Exams-Questions-Answers",
      "SC-900": "Ditectrev/Microsoft-SC-900-Microsoft-Security-Compliance-and-Identity-Fundamentals-Practice-Tests-Exams-QA"
    }
  }
}
```

**Tool Parameters**:

| Parameter | Type | Required | Description |
|---|---|---|---|
| `examCode` | string | Yes | Exam code, e.g. `"AZ-900"`, `"AZ-104"`, `"SC-900"` |
| `keyword` | string | No | Filter questions containing this keyword/phrase |
| `count` | int | No | Number of questions to return (default: 5, max: 20) |
| `random` | bool | No | If true, return random selection; if false, return sequentially (default: true) |

**Implementation Skeleton**:

```csharp
public class GitHubPracticeQuestionsToolset : IAIToolset
{
    private readonly GitHubContentService _github;
    private readonly DitectrevMarkdownParser _parser;

    public GitHubPracticeQuestionsToolset(GitHubContentService github, DitectrevMarkdownParser parser)
    {
        _github = github;
        _parser = parser;
    }

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                SearchPracticeQuestionsAsync,
                new AIFunctionFactoryCreateOptions
                {
                    Name = "search_practice_questions",
                    Description = "Searches community-maintained practice question banks for Microsoft certification exams. Returns multiple-choice questions with answer explanations."
                })
        };
        return Task.FromResult<IReadOnlyList<AITool>>(tools);
    }

    private async Task<string> SearchPracticeQuestionsAsync(
        [Description("Exam code, e.g. 'AZ-900', 'AZ-104', 'SC-900'")] string examCode,
        [Description("Filter questions containing this keyword or phrase")] string? keyword = null,
        [Description("Number of questions to return (default: 5, max: 20)")] int count = 5,
        [Description("If true, return a random selection; if false, return sequentially")] bool random = true,
        CancellationToken cancellationToken = default)
    {
        // Resolve repo from config map, fetch README, parse questions, filter and return
        throw new NotImplementedException();
    }
}
```

**Implementation Notes**:

1. Use the GitHub Contents API: `GET /repos/{owner}/{repo}/readme` (accepts `application/vnd.github.raw+json` for raw content)
2. **Parse the Markdown** — Ditectrev uses a consistent format. Each question block follows this pattern:

```markdown
### A company is planning to migrate to Azure... (question text)

- [ ] Option A text.
- [ ] Option B text.
- [x] Option C text (correct answer).
- [ ] Option D text.

**[⬆ Back to Top](#table-of-contents)**
```

3. Build `DitectrevMarkdownParser` to extract structured `PracticeQuestion` objects:

```csharp
public record PracticeQuestion(
    string QuestionText,
    List<AnswerOption> Options,
    string CorrectAnswer,
    string? Explanation,
    string ExamCode,
    string SourceUrl
);

public record AnswerOption(string Label, string Text, bool IsCorrect);
```

4. Parse rules:
   - Split on `### ` to isolate question blocks
   - Within each block, extract the question text (everything before the first `- [`)
   - Parse `- [x]` as correct answer, `- [ ]` as incorrect
   - Some questions have multiple correct answers (`- [x]` appears more than once)
   - Ignore lines matching `**[⬆ Back to Top]**` pattern
   - Strip image references `![...](...)`— note these in the question as `[Image removed — see source]`
5. **Cache the full parsed question bank per exam** for 1 hour. README files are large (some 500KB+), so avoid re-fetching on every call.
6. When `keyword` is provided, do case-insensitive substring matching against question text and option text.
7. When `random` is true, use `Random.Shared` to select from the filtered set.

**Output**: Return JSON-serialized array of `PracticeQuestion` objects. Include `sourceUrl` pointing to the GitHub repo for attribution.

**Rate Limit Awareness**: GitHub API allows 60 requests/hour unauthenticated, 5,000/hour with PAT. Since we cache aggressively, this is unlikely to be an issue. Implement conditional requests using `If-None-Match` / ETag headers for cache revalidation.

---

### Toolset 2: `GitHubStudyNotesToolset`

**Exposes tool**: `get_study_notes`

**Source**: Multiple GitHub repos with structured Markdown study notes

**Description**: Retrieves structured study notes and summaries for Microsoft certification exam topics. Sources include Azure-in-bullet-points, Azure-Zero-To-Hero, and individual exam study repos.

**Repository Map**:

```json
{
  "StudyNotes": {
    "Repos": [
      {
        "id": "bullet-points",
        "repo": "undergroundwires/Azure-in-bullet-points",
        "exams": ["AZ-900", "AZ-104", "AZ-303", "AZ-304", "AZ-400"],
        "description": "Concise bullet-point summaries with emoji taxonomy",
        "license": "CC-BY-4.0",
        "pathPattern": "AZ-{code}/"
      },
      {
        "id": "zero-to-hero",
        "repo": "abhishekpandaOfficial/AZURE-Zero-To-Hero",
        "exams": ["AZ-900", "AZ-104", "AZ-204", "AZ-305", "AZ-400"],
        "description": "Comprehensive notes and guidance",
        "license": "Unknown"
      },
      {
        "id": "az104-mischa",
        "repo": "mischavandenburg/az-104-azure-administrator",
        "exams": ["AZ-104"],
        "description": "Obsidian notes with Anki-exportable definitions",
        "license": "Unknown"
      },
      {
        "id": "az204-arvigeus",
        "repo": "arvigeus/AZ-204",
        "exams": ["AZ-204"],
        "description": "Case studies, exercises, knowledge checks, practice questions, and topic notes",
        "license": "Unknown"
      }
    ]
  }
}
```

**Tool Parameters**:

| Parameter | Type | Required | Description |
|---|---|---|---|
| `examCode` | string | Yes | Exam code, e.g. `"AZ-104"` |
| `topic` | string | No | Topic keyword to filter notes (e.g. `"networking"`, `"identity"`, `"storage"`) |
| `source` | string | No | Specific repo ID to query (e.g. `"bullet-points"`). Defaults to all available for the exam. |

**Implementation Skeleton**:

```csharp
public class GitHubStudyNotesToolset : IAIToolset
{
    private readonly GitHubContentService _github;
    private readonly MarkdownParserService _markdownParser;

    public GitHubStudyNotesToolset(GitHubContentService github, MarkdownParserService markdownParser)
    {
        _github = github;
        _markdownParser = markdownParser;
    }

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                GetStudyNotesAsync,
                new AIFunctionFactoryCreateOptions
                {
                    Name = "get_study_notes",
                    Description = "Retrieves structured Markdown study notes for a Microsoft certification exam topic from community GitHub repositories."
                })
        };
        return Task.FromResult<IReadOnlyList<AITool>>(tools);
    }

    private async Task<string> GetStudyNotesAsync(
        [Description("Exam code, e.g. 'AZ-104'")] string examCode,
        [Description("Topic keyword to filter notes, e.g. 'networking', 'identity', 'storage'")] string? topic = null,
        [Description("Specific repo source ID, e.g. 'bullet-points'. Defaults to all available.")] string? source = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
```

**Implementation Notes**:

1. Use the GitHub Trees API to get directory listings: `GET /repos/{owner}/{repo}/git/trees/{branch}?recursive=1`
2. Filter for `.md` files within the exam-specific directory
3. Fetch individual file contents via: `GET /repos/{owner}/{repo}/contents/{path}`
4. **For undergroundwires/Azure-in-bullet-points**, the emoji taxonomy is significant:
   - 💡 = Best practice
   - ❗ = Limitation or gotcha
   - 📝 = Common exam topic
   - Parse and preserve these as metadata tags on returned content
5. **For arvigeus/AZ-204**, respect the directory structure: `Topics/`, `Questions/`, `Case Studies/`, `Exercises/`, `Knowledge Checks/` — expose these as filterable content types
6. When `topic` is provided, match against both file paths and content. File path matching is faster and should be tried first.
7. Return Markdown content as-is (the consuming agent can interpret it natively), but add structured metadata headers.

**Output**: JSON-serialized array of study note objects:

```csharp
public record StudyNote(
    string Title,           // derived from filename or first H1/H2
    string ExamCode,
    string Content,         // raw Markdown
    string SourceRepo,
    string SourceUrl,       // direct GitHub link to file
    string? ContentType,    // "notes", "case-study", "exercise", "knowledge-check"
    List<string> Tags       // parsed from emoji taxonomy or directory structure
);
```

---

### Toolset 3: `GitHubExamSyllabiToolset`

**Exposes tool**: `get_exam_syllabus`

**Source**: `FurkanKambay/ms-cert-exams-json` GitHub repo

**Description**: Returns structured exam syllabus data — topic areas, percentage weights, skill breakdowns, and individual items. Useful for building study plans and mapping questions to objectives.

**Critical Caveat**: This repo was last updated in 2019. The tool MUST include a freshness warning in every response. Encourage the consumer to cross-reference against the Microsoft Learn Catalog API's current skill outlines.

**Tool Parameters**:

| Parameter | Type | Required | Description |
|---|---|---|---|
| `examCode` | string | Yes | Exam code, e.g. `"AZ-900"` |

**Implementation Skeleton**:

```csharp
public class GitHubExamSyllabiToolset : IAIToolset
{
    private readonly GitHubContentService _github;

    public GitHubExamSyllabiToolset(GitHubContentService github) => _github = github;

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                GetExamSyllabusAsync,
                new AIFunctionFactoryCreateOptions
                {
                    Name = "get_exam_syllabus",
                    Description = "Returns structured exam syllabus with topic areas, percentage weights, and skill breakdowns. Note: data may be outdated; always cross-reference with Microsoft Learn."
                })
        };
        return Task.FromResult<IReadOnlyList<AITool>>(tools);
    }

    private async Task<string> GetExamSyllabusAsync(
        [Description("Exam code, e.g. 'AZ-900'")] string examCode,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
```

**Implementation Notes**:

1. Fetch from: `GET /repos/FurkanKambay/ms-cert-exams-json/contents/exams/` — list available JSON files
2. Each exam is a JSON file following this schema:

```json
{
  "label": "Topic Area (25-30%)",
  "skills": [
    {
      "label": "Skill Name",
      "items": ["Sub-item 1", "Sub-item 2"]
    }
  ]
}
```

3. Parse into strongly-typed model:

```csharp
public record ExamSyllabus(
    string ExamCode,
    List<TopicArea> Topics,
    string LastUpdated,     // from GitHub file commit date
    string FreshnessWarning // always include: "This syllabus data is from 2019 and may be outdated..."
);

public record TopicArea(
    string Label,
    int? WeightMin,     // parsed from "25-30%" → 25
    int? WeightMax,     // parsed from "25-30%" → 30
    List<Skill> Skills
);

public record Skill(string Label, List<string> Items);
```

4. Parse the percentage weight from the label using regex: `\((\d+)-(\d+)%\)` or `\((\d+)%\)`

**Output**: JSON-serialized `ExamSyllabus` object with structured topic/weight/skill hierarchy.

---

### Toolset 4: `GitHubAnkiDeckToolset`

**Exposes tool**: `get_flashcards`

**Source**: GitHub repos with Anki .apkg files and CrowdAnki JSON exports

**Description**: Extracts flashcards from open-source Anki decks published on GitHub. Returns question/answer pairs for spaced-repetition study.

**Repository Map**:

```json
{
  "AnkiDecks": [
    {
      "repo": "connorsayers/AZ-104-Study-Deck",
      "exam": "AZ-104",
      "format": "apkg",
      "path": "",
      "cardCount": 4000,
      "description": "~4,000 multiple-choice questions from Microsoft Learn content"
    },
    {
      "repo": "envico801/AI-102-Azure-AI-Engineer-Associate",
      "exam": "AI-102",
      "format": "apkg",
      "path": "anki/AI-102.apkg",
      "description": "Structured by exam domains with percentage weights"
    },
    {
      "repo": "forkmantis/anki-az-203-deck",
      "exam": "AZ-203",
      "format": "crowdanki",
      "path": "",
      "description": "CrowdAnki JSON format for collaborative editing"
    },
    {
      "repo": "mischavandenburg/az-104-azure-administrator",
      "exam": "AZ-104",
      "format": "apkg",
      "path": "",
      "description": "Obsidian-generated cards using term::definition syntax"
    }
  ]
}
```

**Tool Parameters**:

| Parameter | Type | Required | Description |
|---|---|---|---|
| `examCode` | string | Yes | Exam code |
| `count` | int | No | Number of flashcards to return (default: 10, max: 50) |
| `topic` | string | No | Filter by topic keyword |

**Implementation Skeleton**:

```csharp
public class GitHubAnkiDeckToolset : IAIToolset
{
    private readonly AnkiExtractorService _ankiExtractor;

    public GitHubAnkiDeckToolset(AnkiExtractorService ankiExtractor) => _ankiExtractor = ankiExtractor;

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                GetFlashcardsAsync,
                new AIFunctionFactoryCreateOptions
                {
                    Name = "get_flashcards",
                    Description = "Extracts flashcards from open-source Anki decks for spaced-repetition study. Returns question/answer pairs."
                })
        };
        return Task.FromResult<IReadOnlyList<AITool>>(tools);
    }

    private async Task<string> GetFlashcardsAsync(
        [Description("Exam code, e.g. 'AZ-104'")] string examCode,
        [Description("Number of flashcards to return (default: 10, max: 50)")] int count = 10,
        [Description("Filter by topic keyword")] string? topic = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
```

**Implementation Notes**:

1. **Anki .apkg files are ZIP archives containing a SQLite database** (`collection.anki2` or `collection.anki21`). The extraction process:
   - Download the .apkg file via GitHub raw content URL
   - Unzip in memory (it's a ZIP file)
   - Open the SQLite database using `Microsoft.Data.Sqlite`
   - Query the `notes` table: `SELECT flds, tags FROM notes`
   - The `flds` column contains fields separated by `\x1f` (unit separator character). Typically field 0 = front (question), field 1 = back (answer).
   - Strip HTML tags from field content (Anki stores rich text as HTML)
2. **CrowdAnki format** (forkmantis repo) is a directory with a `deck.json` file containing:

```json
{
  "name": "Deck Name",
  "notes": [
    {
      "fields": ["front text", "back text"],
      "tags": ["tag1", "tag2"]
    }
  ]
}
```

3. Build `AnkiExtractorService` with methods for both formats.
4. Cache extracted flashcard sets for 1 hour (these files rarely change).
5. Install NuGet: `Microsoft.Data.Sqlite` for .apkg parsing.

**Output**:

```csharp
public record FlashCard(
    string Front,           // question side
    string Back,            // answer side
    string ExamCode,
    List<string> Tags,
    string SourceRepo,
    string DeckName
);
```

---

### Toolset 5: `YouTubeStudyContentToolset`

**Exposes tool**: `search_youtube_study_content`

**Source**: YouTube Data API v3 + transcript extraction

**Description**: Searches free Microsoft Certification study videos and retrieves transcripts. Covers John Savill's Technical Training, freeCodeCamp, and Adam Marczak channels.

**Channel Map**:

```json
{
  "YouTubeChannels": [
    {
      "id": "UCpIn7ox7j7bH_OFj7tYouOQ",
      "name": "John Savill's Technical Training",
      "exams": ["AZ-900", "AZ-104", "AZ-305", "AZ-500", "AZ-700", "SC-100", "SC-300"],
      "notes": "944+ videos, 30 playlists, zero ads. Gold standard."
    },
    {
      "id": "UC8butISFwT-Wl7EV0hUK0BQ",
      "name": "freeCodeCamp.org",
      "exams": ["AZ-900", "AZ-104", "AI-900", "DP-900", "PL-900"],
      "notes": "Full-length exam prep courses by Andrew Brown/ExamPro"
    },
    {
      "id": "UCGWEnk25wGRiM5jaJaWHMPg",
      "name": "Adam Marczak - Azure for Everyone",
      "exams": ["AZ-900"],
      "notes": "Episodic AZ-900 course, companion site at marczak.io"
    }
  ]
}
```

**Tool Parameters**:

| Parameter | Type | Required | Description |
|---|---|---|---|
| `examCode` | string | No | Filter to videos about this exam |
| `query` | string | No | Search query within the channel set |
| `channelId` | string | No | Limit to specific channel |
| `includeTranscript` | bool | No | If true, fetch and include the transcript (default: false, as this is quota-heavy) |
| `maxResults` | int | No | Number of videos to return (default: 5, max: 15) |

**Implementation Skeleton**:

```csharp
public class YouTubeStudyContentToolset : IAIToolset
{
    private readonly YouTubeService _youtube;

    public YouTubeStudyContentToolset(YouTubeService youtube) => _youtube = youtube;

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                SearchYouTubeStudyContentAsync,
                new AIFunctionFactoryCreateOptions
                {
                    Name = "search_youtube_study_content",
                    Description = "Searches free Microsoft Certification study videos on YouTube and optionally retrieves transcripts."
                })
        };
        return Task.FromResult<IReadOnlyList<AITool>>(tools);
    }

    private async Task<string> SearchYouTubeStudyContentAsync(
        [Description("Exam code to filter results, e.g. 'AZ-104'")] string? examCode = null,
        [Description("Search query within the curated channel set")] string? query = null,
        [Description("Limit results to a specific channel ID")] string? channelId = null,
        [Description("If true, fetch and include the video transcript (quota-heavy; use sparingly)")] bool includeTranscript = false,
        [Description("Number of videos to return (default: 5, max: 15)")] int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
```

**Implementation Notes**:

1. **YouTube Data API v3** — Register a free API key in Google Cloud Console. Quota is **10,000 units/day**.
   - `search.list` = 100 units per call
   - `videos.list` = 1 unit per call
   - `captions.list` = 50 units per call
   - Budget carefully: ~100 search calls per day maximum

2. **Search endpoint**: `GET /youtube/v3/search?part=snippet&channelId={id}&q={exam} {query}&type=video&maxResults={n}&key={key}`

3. **Transcript extraction** — The official Captions API requires OAuth and video owner permission. Instead, use an alternative approach:
   - Use the undocumented `https://www.youtube.com/watch?v={videoId}` page to extract `timedtext` track URLs from the page source (look for `"captions"` in the `ytInitialPlayerResponse` JSON)
   - OR use a .NET port/wrapper of the `youtube-transcript-api` Python library pattern
   - Parse the XML timed text into segments: `<text start="1.23" dur="4.56">transcript text</text>`
   - **Fallback**: If transcript extraction fails, return video metadata only with a note that transcript is unavailable

4. **Quota management**: Track daily quota usage in a static counter. When `includeTranscript` is true, warn if quota is running low. Cache video metadata and transcripts aggressively (1+ hours).

5. For playlist-based browsing, use: `GET /youtube/v3/playlistItems?part=snippet&playlistId={id}&maxResults=50`

**Output**:

```csharp
public record StudyVideo(
    string VideoId,
    string Title,
    string Description,
    string ChannelName,
    string PublishedAt,
    string ThumbnailUrl,
    string VideoUrl,            // https://youtube.com/watch?v={id}
    TimeSpan? Duration,
    List<TranscriptSegment>? Transcript
);

public record TranscriptSegment(
    double StartSeconds,
    double DurationSeconds,
    string Text
);
```

---

### Toolset 6: `StackExchangeToolset`

**Exposes tool**: `search_stack_exchange`

**Source**: Stack Exchange API v2.3

**Description**: Searches Stack Overflow and related Stack Exchange sites for technical Q&A relevant to Microsoft certification exam topics. Useful for understanding real-world scenarios and common pitfalls.

**Tool Parameters**:

| Parameter | Type | Required | Description |
|---|---|---|---|
| `query` | string | Yes | Search query (e.g., `"Azure VNet peering"`, `"managed identity vs service principal"`) |
| `examCode` | string | No | Exam code — used to auto-append relevant tags (see tag map below) |
| `minScore` | int | No | Minimum answer score to include (default: 1) |
| `maxResults` | int | No | Number of questions to return (default: 5, max: 15) |
| `includeAnswers` | bool | No | Include top answer body for each question (default: true) |

**Tag Map** (auto-appended when examCode provided):

```json
{
  "ExamTagMap": {
    "AZ-900": ["azure"],
    "AZ-104": ["azure", "azure-active-directory", "azure-networking", "azure-storage", "azure-virtual-machines"],
    "AZ-204": ["azure", "azure-functions", "azure-cosmosdb", "azure-blob-storage", "azure-service-bus"],
    "AZ-305": ["azure", "azure-architecture", "azure-load-balancer", "azure-front-door"],
    "AZ-400": ["azure-devops", "azure-pipelines", "azure-repos"],
    "AZ-500": ["azure", "azure-security", "azure-keyvault", "azure-active-directory"],
    "SC-900": ["azure-active-directory", "microsoft-entra-id", "azure-security"],
    "AI-102": ["azure-cognitive-services", "azure-openai", "azure-ai"],
    "DP-900": ["azure-sql-database", "azure-cosmosdb", "azure-data-lake"],
    "MS-900": ["microsoft-365", "microsoft-graph-api"]
  }
}
```

**Implementation Skeleton**:

```csharp
public class StackExchangeToolset : IAIToolset
{
    private readonly StackExchangeService _stackExchange;

    public StackExchangeToolset(StackExchangeService stackExchange) => _stackExchange = stackExchange;

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                SearchStackExchangeAsync,
                new AIFunctionFactoryCreateOptions
                {
                    Name = "search_stack_exchange",
                    Description = "Searches Stack Overflow for technical Q&A relevant to Microsoft certification exam topics."
                })
        };
        return Task.FromResult<IReadOnlyList<AITool>>(tools);
    }

    private async Task<string> SearchStackExchangeAsync(
        [Description("Search query, e.g. 'Azure VNet peering'")] string query,
        [Description("Exam code — auto-appends relevant tags to the search")] string? examCode = null,
        [Description("Minimum answer score to include (default: 1)")] int minScore = 1,
        [Description("Number of questions to return (default: 5, max: 15)")] int maxResults = 5,
        [Description("If true, include the top-voted answer body for each question")] bool includeAnswers = true,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
```

**Implementation Notes**:

1. **Endpoint**: `GET https://api.stackexchange.com/2.3/search/advanced`
2. **Key parameters**:
   - `q` = search query
   - `tagged` = semicolon-separated tags (from tag map)
   - `sort=relevance` or `sort=votes`
   - `accepted=true` to prioritize answered questions
   - `site=stackoverflow`
   - `filter=withbody` to include question and answer bodies
   - `key={appKey}` (optional, raises rate limit from 300 to 10,000/day)
   - `pagesize={maxResults}`
3. **Response is gzip-compressed** — the API requires `Accept-Encoding: gzip` and always returns compressed responses. Use `HttpClientHandler` with `AutomaticDecompression = DecompressionMethods.GZip`.
4. Strip HTML tags from question and answer bodies before returning (bodies come as HTML). Use a simple regex or `System.Web.HttpUtility.HtmlDecode` + tag stripping.
5. **Rate limiting**: Respect the `backoff` field in responses — if present, wait that many seconds before the next request. Track `quota_remaining` from response headers.
6. Content is **CC BY-SA 4.0** licensed. Include attribution in output.

**Output**:

```csharp
public record QandAPost(
    int QuestionId,
    string Title,
    string QuestionBody,    // HTML stripped
    int Score,
    List<string> Tags,
    string Url,             // link to SO question
    string? TopAnswerBody,  // HTML stripped, highest-voted answer
    int? TopAnswerScore,
    bool IsAnswered,
    string Attribution       // "Content from Stack Overflow, CC BY-SA 4.0"
);
```

---

### Toolset 7: `PodcastFeedToolset`

**Exposes tool**: `search_podcast_episodes`

**Source**: Podcast RSS feeds (XML)

**Description**: Searches Microsoft-focused podcast episode catalogs for exam-relevant technical discussions. Returns episode metadata with links to audio.

**Feed Map**:

```json
{
  "PodcastFeeds": [
    {
      "id": "ms-cloud-it-pro",
      "name": "Microsoft Cloud IT Pro Podcast",
      "feedUrl": "https://feeds.podcastmirror.com/microsoftclouditpropodcast",
      "relevantExams": ["AZ-900", "AZ-104", "AZ-305", "MS-900", "SC-900"],
      "description": "420+ episodes on M365, Azure, security, compliance. Weekly."
    },
    {
      "id": "azure-security",
      "name": "Azure Security Podcast",
      "feedUrl": "https://rss.com/podcasts/azsecpodcast/",
      "relevantExams": ["AZ-500", "SC-900", "SC-100", "SC-200", "SC-300"],
      "description": "Security, privacy, compliance. Hosted by Microsoft security experts."
    },
    {
      "id": "azure-podcast",
      "name": "The Azure Podcast",
      "feedUrl": "http://feeds.feedburner.com/TheAzurePodcast",
      "relevantExams": ["AZ-900", "AZ-104", "AZ-204", "AZ-305"],
      "description": "521 episodes (archived). AI-powered transcript search at chat.azpodcast.com."
    },
    {
      "id": "azure-devops",
      "name": "Azure DevOps Podcast",
      "feedUrl": "http://feed.azuredevops.show/rss",
      "relevantExams": ["AZ-400"],
      "description": "Azure DevOps, cloud architecture, CI/CD."
    }
  ]
}
```

**Tool Parameters**:

| Parameter | Type | Required | Description |
|---|---|---|---|
| `query` | string | No | Keyword search against episode titles and descriptions |
| `podcastId` | string | No | Filter to specific podcast (e.g. `"ms-cloud-it-pro"`) |
| `examCode` | string | No | Filter to podcasts relevant to this exam |
| `maxResults` | int | No | Number of episodes to return (default: 5, max: 20) |
| `afterDate` | string | No | ISO date — only return episodes published after this date |

**Implementation Skeleton**:

```csharp
public class PodcastFeedToolset : IAIToolset
{
    private readonly PodcastFeedService _feedService;

    public PodcastFeedToolset(PodcastFeedService feedService) => _feedService = feedService;

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                SearchPodcastEpisodesAsync,
                new AIFunctionFactoryCreateOptions
                {
                    Name = "search_podcast_episodes",
                    Description = "Searches Microsoft-focused podcast episode catalogs for exam-relevant technical discussions."
                })
        };
        return Task.FromResult<IReadOnlyList<AITool>>(tools);
    }

    private async Task<string> SearchPodcastEpisodesAsync(
        [Description("Keyword to search against episode titles and descriptions")] string? query = null,
        [Description("Filter to a specific podcast by ID, e.g. 'ms-cloud-it-pro'")] string? podcastId = null,
        [Description("Filter to podcasts relevant to a specific exam code")] string? examCode = null,
        [Description("Number of episodes to return (default: 5, max: 20)")] int maxResults = 5,
        [Description("ISO date string — only return episodes published after this date")] string? afterDate = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
```

**Implementation Notes**:

1. Use `System.Xml.Linq` or `System.ServiceModel.Syndication.SyndicationFeed` to parse RSS/Atom feeds
2. RSS `<item>` elements contain: `<title>`, `<description>`, `<pubDate>`, `<enclosure>` (audio URL), `<link>`, `<itunes:duration>`, `<itunes:summary>`
3. **Parse and cache the full feed** (30-minute cache). Feeds are typically < 1MB.
4. Search implementation: case-insensitive substring match against `title` and `description` fields
5. For `afterDate`, parse `<pubDate>` (RFC 822 format) and compare
6. Some feeds use CDATA sections in descriptions — handle these with proper XML parsing
7. Prefer `SyndicationFeed.Load(XmlReader)` which handles most RSS variants automatically

**Output**:

```csharp
public record PodcastEpisode(
    string PodcastName,
    string EpisodeTitle,
    string Description,         // plain text, truncated to 500 chars
    string PublishedDate,
    string? Duration,           // e.g. "45:23"
    string? AudioUrl,           // direct link to audio file
    string EpisodeUrl,          // web link to episode page
    List<string> RelevantExams  // from feed config
);
```

---

### Toolset 8: `GitHubCommunityHubToolset`

**Exposes tool**: `get_community_resources`

**Source**: `mscerts/hub` GitHub repo and `shiftavenue/awesome-azure-learning`

**Description**: Returns curated community resource links for a given Microsoft certification exam — including free courses, labs, practice tests, video playlists, and study guides compiled by the community.

**Tool Parameters**:

| Parameter | Type | Required | Description |
|---|---|---|---|
| `examCode` | string | Yes | Exam code |
| `resourceType` | string | No | Filter: `"courses"`, `"labs"`, `"practice-tests"`, `"videos"`, `"study-guides"`, or `"all"` (default) |

**Implementation Skeleton**:

```csharp
public class GitHubCommunityHubToolset : IAIToolset
{
    private readonly GitHubContentService _github;
    private readonly MarkdownParserService _markdownParser;

    public GitHubCommunityHubToolset(GitHubContentService github, MarkdownParserService markdownParser)
    {
        _github = github;
        _markdownParser = markdownParser;
    }

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                GetCommunityResourcesAsync,
                new AIFunctionFactoryCreateOptions
                {
                    Name = "get_community_resources",
                    Description = "Returns curated community resource links for a Microsoft certification exam, including courses, labs, practice tests, and study guides."
                })
        };
        return Task.FromResult<IReadOnlyList<AITool>>(tools);
    }

    private async Task<string> GetCommunityResourcesAsync(
        [Description("Exam code, e.g. 'AZ-104'")] string examCode,
        [Description("Resource type filter: 'courses', 'labs', 'practice-tests', 'videos', 'study-guides', or 'all'")] string resourceType = "all",
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
```

**Implementation Notes**:

1. **mscerts/hub** — The repo's `docs/` directory contains per-exam Markdown pages (e.g., `docs/azure/AZ-104.md`). Use the GitHub Contents API to fetch the appropriate page.
2. **shiftavenue/awesome-azure-learning** — `topics/certifications/` contains per-exam Markdown pages with tables of links.
3. Parse the Markdown to extract links with categories. Both repos use consistent formatting with section headers (`##`) for categories and Markdown links `[title](url)`.
4. Merge results from both sources, deduplicating by URL.
5. Return structured resource objects, not raw Markdown.

**Output**:

```csharp
public record CommunityResource(
    string Title,
    string Url,
    string Category,        // "course", "lab", "practice-test", "video", "study-guide", "blog-post"
    string ExamCode,
    string SourceRepo,
    string? Description
);
```

---

### Toolset 9: `ExamTopicsToolset`

**Exposes tool**: `search_free_certification_offers`

**Source**: `cloudcommunity/Free-Certifications` GitHub repo

**Description**: Checks for currently available free Microsoft certification exam vouchers and offers. Microsoft periodically offers free exams through Virtual Training Days, Cloud Skills Challenges, and partner programs.

**Tool Parameters**:

| Parameter | Type | Required | Description |
|---|---|---|---|
| `vendor` | string | No | Filter by vendor, default `"Microsoft"`. Pass `"all"` for everything. |
| `includeExpired` | bool | No | Include expired offers for historical context (default: false) |

**Implementation Skeleton**:

```csharp
public class ExamTopicsToolset : IAIToolset
{
    private readonly GitHubContentService _github;
    private readonly MarkdownParserService _markdownParser;

    public ExamTopicsToolset(GitHubContentService github, MarkdownParserService markdownParser)
    {
        _github = github;
        _markdownParser = markdownParser;
    }

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                SearchFreeCertificationOffersAsync,
                new AIFunctionFactoryCreateOptions
                {
                    Name = "search_free_certification_offers",
                    Description = "Checks for currently available free Microsoft certification exam vouchers and promotional offers."
                })
        };
        return Task.FromResult<IReadOnlyList<AITool>>(tools);
    }

    private async Task<string> SearchFreeCertificationOffersAsync(
        [Description("Vendor to filter by (default: 'Microsoft'). Pass 'all' for all vendors.")] string vendor = "Microsoft",
        [Description("If true, include expired offers for historical context")] bool includeExpired = false,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
```

**Implementation Notes**:

1. The repo maintains a `README.md` with a Markdown table of free certification offers. Parse the table:
   - Columns: Provider, Description, Link, Expiration
   - Filter rows where Provider contains "Microsoft"
2. There is also an `Expired-Offers.md` for historical offers
3. Check the repo frequently (cache 1 hour) as offers are time-sensitive
4. Parse expiration dates and flag offers expiring within 7 days

**Output**:

```csharp
public record FreeCertOffer(
    string Provider,
    string Description,
    string Url,
    string? Expiration,
    bool IsExpired,
    bool ExpiringSoon   // within 7 days
);
```

---

### Toolset 10: `OfficialLabExercisesToolset`

**Exposes tool**: `get_official_lab_exercises`

**Source**: `github.com/MicrosoftLearning` organization repos

**Description**: Retrieves official hands-on lab instructions for Microsoft certification exams. These are MIT-licensed Markdown files maintained by Microsoft.

**Access Pattern**: Repos follow the naming convention `{ExamCode}-{ExamName}` e.g., `AZ-104-MicrosoftAzureAdministrator`, `SC-900-Microsoft-Security-Compliance-and-Identity-Fundamentals`

**Tool Parameters**:

| Parameter | Type | Required | Description |
|---|---|---|---|
| `examCode` | string | Yes | Exam code |
| `labNumber` | int | No | Specific lab number to retrieve |
| `listOnly` | bool | No | If true, return lab listing without full content (default: false) |

**Implementation Skeleton**:

```csharp
public class OfficialLabExercisesToolset : IAIToolset
{
    private readonly GitHubContentService _github;

    public OfficialLabExercisesToolset(GitHubContentService github) => _github = github;

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                GetOfficialLabExercisesAsync,
                new AIFunctionFactoryCreateOptions
                {
                    Name = "get_official_lab_exercises",
                    Description = "Retrieves official MIT-licensed hands-on lab instructions from Microsoft Learning GitHub repositories."
                })
        };
        return Task.FromResult<IReadOnlyList<AITool>>(tools);
    }

    private async Task<string> GetOfficialLabExercisesAsync(
        [Description("Exam code, e.g. 'AZ-104'")] string examCode,
        [Description("Specific lab number to retrieve (e.g. 1 for Lab_01)")] int? labNumber = null,
        [Description("If true, return only the list of available labs without full content")] bool listOnly = false,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
```

**Implementation Notes**:

1. First, discover the repo name: search GitHub org repos or maintain a mapping. The shortcut `https://aka.ms/{examcode}labs` redirects to the correct repo — but for API use, maintain a config map.
2. Lab instructions are in the `Instructions/` or `Allfiles/` directories as Markdown files
3. Labs are numbered: `Lab_01_...md`, `Lab_02_...md`, etc.
4. Parse the Markdown to extract: lab title, objectives, estimated time, prerequisites, step-by-step tasks
5. These labs often reference Azure Portal steps — they're still useful for understanding service configuration even without running them

**Output**:

```csharp
public record LabExercise(
    string ExamCode,
    int LabNumber,
    string Title,
    string Content,             // full Markdown content
    string? EstimatedTime,
    List<string> Objectives,
    string SourceUrl
);
```

---

## Shared Utilities

### MarkdownParserService

Build a general-purpose Markdown parsing utility used across multiple toolsets:

```csharp
public class MarkdownParserService
{
    // Extract all links from Markdown content
    public List<(string title, string url)> ExtractLinks(string markdown);

    // Extract sections by header level
    public Dictionary<string, string> ExtractSections(string markdown, int headerLevel = 2);

    // Parse a Markdown table into rows
    public List<Dictionary<string, string>> ParseTable(string markdown);

    // Strip Markdown formatting, return plain text
    public string StripFormatting(string markdown);

    // Extract question blocks from Ditectrev-style Q&A Markdown
    public List<PracticeQuestion> ParseDitectrevQuestions(string markdown, string examCode);
}
```

### GitHubContentService

Centralized GitHub API interaction with caching and rate-limit awareness:

```csharp
public class GitHubContentService
{
    // Get file content (raw or base64-decoded)
    public Task<string> GetFileContent(string owner, string repo, string path);

    // Get directory listing
    public Task<List<GitHubFile>> GetDirectoryListing(string owner, string repo, string path);

    // Get full repo tree (recursive)
    public Task<List<GitHubTreeEntry>> GetRepoTree(string owner, string repo, string branch = "main");

    // Download binary file (for .apkg files)
    public Task<byte[]> DownloadFile(string owner, string repo, string path);

    // Check rate limit status
    public Task<(int remaining, int limit, DateTimeOffset resetAt)> GetRateLimit();
}
```

---

## Error Handling and Edge Cases

1. **GitHub rate limiting**: If the rate limit is exceeded, return a clear error message string with the reset time. Never throw unhandled exceptions from tool methods — always return a serialized error or status object.
2. **Missing repos**: Repos may be deleted, renamed, or made private. Handle 404s gracefully with a message like `"Repository {repo} is no longer available. This resource may have moved."`
3. **Content format changes**: Ditectrev or other repos may change their Markdown format. Build parsers defensively with fallback to raw content if structured parsing fails.
4. **Large responses**: Some README files are 500KB+. When returning practice questions, always paginate — never return the entire parsed bank in one tool response.
5. **YouTube quota exhaustion**: Track daily quota usage in a static counter. When approaching the limit, disable transcript fetching and return metadata only.
6. **RSS feed downtime**: If a podcast feed is unreachable, return cached content with a staleness warning, or skip that feed and note it in the response.
7. **Encoding issues**: GitHub API returns base64-encoded content by default. Always decode properly. Some repos contain Unicode characters (emoji taxonomy in Azure-in-bullet-points).

---

## Testing Strategy

1. **Unit tests** for all parsers (Ditectrev Markdown, Anki .apkg, RSS XML, Stack Exchange JSON)
2. **Integration tests** with recorded HTTP responses (use `WireMock.Net` or fixture files) for each external API
3. **Include sample data files** in the test project:
   - A small Ditectrev-format Markdown file with 5 questions
   - A minimal .apkg file with 3 flashcards
   - A sample RSS feed XML with 3 episodes
   - A sample Stack Exchange API JSON response
4. **Toolset contract tests**: Verify that each `IAIToolset.GetToolsAsync()` returns non-empty tool lists, that tool names match expected values, and that tool methods return valid JSON-serializable strings rather than throwing exceptions for common inputs.

---

## Configuration Checklist

Before first run, configure the following:

- [ ] **GitHub PAT** (optional but recommended): `Settings > Developer settings > Personal access tokens > Fine-grained tokens` — scope to `public_repo` read-only. Without PAT: 60 requests/hour. With PAT: 5,000/hour.
- [ ] **YouTube Data API key** (required for Toolset 5): Google Cloud Console > Enable YouTube Data API v3 > Create API key. Free tier: 10,000 units/day.
- [ ] **Stack Exchange app key** (optional): Register at `stackapps.com` for 10,000 requests/day (vs. 300 anonymous).
