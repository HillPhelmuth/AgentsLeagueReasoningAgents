using System.Text.Json;
using AgentsLeagueReasoningAgents.Workflows;

namespace AgentsLeagueReasoningAgents.Services;

public interface IPreparationAssessmentStateStore
{
    Task SavePreparationStateAsync(string studentEmail, PreparationWorkflowResult result, CancellationToken cancellationToken = default);
    Task<PreparationWorkflowResult?> GetPreparationStateAsync(string studentEmail, CancellationToken cancellationToken = default);
    Task SaveAssessmentSessionAsync(string studentEmail, AssessmentSessionState state, CancellationToken cancellationToken = default);
    Task<AssessmentSessionState?> GetAssessmentSessionAsync(string studentEmail, CancellationToken cancellationToken = default);
    Task ClearAssessmentSessionAsync(string studentEmail, CancellationToken cancellationToken = default);
}

public sealed class PreparationAssessmentStateStore : IPreparationAssessmentStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;

    public PreparationAssessmentStateStore()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AgentsLeagueReasoningAgents");
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "student-state.json");
    }

    public async Task SavePreparationStateAsync(string studentEmail, PreparationWorkflowResult result, CancellationToken cancellationToken = default)
    {
        var key = NormalizeKey(studentEmail);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await LoadSnapshotUnsafeAsync(cancellationToken).ConfigureAwait(false);
            if (!snapshot.Students.TryGetValue(key, out var studentState))
            {
                studentState = new PersistedStudentState();
                snapshot.Students[key] = studentState;
            }

            studentState.PreparationResult = result;
            await SaveSnapshotUnsafeAsync(snapshot, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PreparationWorkflowResult?> GetPreparationStateAsync(string studentEmail, CancellationToken cancellationToken = default)
    {
        var key = NormalizeKey(studentEmail);
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await LoadSnapshotUnsafeAsync(cancellationToken).ConfigureAwait(false);
            return snapshot.Students.TryGetValue(key, out var studentState)
                ? studentState.PreparationResult
                : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAssessmentSessionAsync(string studentEmail, AssessmentSessionState state, CancellationToken cancellationToken = default)
    {
        var key = NormalizeKey(studentEmail);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await LoadSnapshotUnsafeAsync(cancellationToken).ConfigureAwait(false);
            if (!snapshot.Students.TryGetValue(key, out var studentState))
            {
                studentState = new PersistedStudentState();
                snapshot.Students[key] = studentState;
            }

            studentState.AssessmentSession = state;
            await SaveSnapshotUnsafeAsync(snapshot, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AssessmentSessionState?> GetAssessmentSessionAsync(string studentEmail, CancellationToken cancellationToken = default)
    {
        var key = NormalizeKey(studentEmail);
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await LoadSnapshotUnsafeAsync(cancellationToken).ConfigureAwait(false);
            return snapshot.Students.TryGetValue(key, out var studentState)
                ? studentState.AssessmentSession
                : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAssessmentSessionAsync(string studentEmail, CancellationToken cancellationToken = default)
    {
        var key = NormalizeKey(studentEmail);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await LoadSnapshotUnsafeAsync(cancellationToken).ConfigureAwait(false);
            if (snapshot.Students.TryGetValue(key, out var studentState))
            {
                studentState.AssessmentSession = null;
                await SaveSnapshotUnsafeAsync(snapshot, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<PersistedStateSnapshot> LoadSnapshotUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new PersistedStateSnapshot();
        }

        await using var stream = File.OpenRead(_filePath);
        var snapshot = await JsonSerializer.DeserializeAsync<PersistedStateSnapshot>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return snapshot ?? new PersistedStateSnapshot();
    }

    private async Task SaveSnapshotUnsafeAsync(PersistedStateSnapshot snapshot, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static string NormalizeKey(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private sealed class PersistedStateSnapshot
    {
        public Dictionary<string, PersistedStudentState> Students { get; set; } = [];
    }

    private sealed class PersistedStudentState
    {
        public PreparationWorkflowResult? PreparationResult { get; set; }
        public AssessmentSessionState? AssessmentSession { get; set; }
    }
}
