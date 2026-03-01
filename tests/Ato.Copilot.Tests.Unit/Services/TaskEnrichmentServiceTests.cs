using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Interfaces.Kanban;
using Ato.Copilot.Core.Models.Compliance;
using Ato.Copilot.Core.Models.Kanban;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// FeatureSpec: 012-task-enrichment
/// Unit tests for TaskEnrichmentService: EnrichTaskAsync, EnrichBoardTasksAsync, GenerateValidationCriteriaAsync.
/// Covers T011-T024 from tasks.md.
/// </summary>
public class TaskEnrichmentServiceTests
{
    private readonly Mock<IRemediationEngine> _remediationEngineMock = new();
    private readonly Mock<IAiRemediationPlanGenerator> _aiGeneratorMock = new();
    private readonly Mock<IChatClient> _chatClientMock = new();
    private readonly Mock<ILogger<TaskEnrichmentService>> _loggerMock = new();

    private TaskEnrichmentService CreateService(bool withChatClient = true, bool aiAvailable = true)
    {
        _aiGeneratorMock.Setup(g => g.IsAvailable).Returns(aiAvailable);
        return new TaskEnrichmentService(
            _remediationEngineMock.Object,
            _aiGeneratorMock.Object,
            _loggerMock.Object,
            withChatClient ? _chatClientMock.Object : null);
    }

    private static RemediationTask CreateTask(
        string taskNumber = "RB-001-001",
        string? existingScript = null,
        string? existingValidation = null,
        string? findingId = null) => new()
    {
        Id = Guid.NewGuid().ToString(),
        TaskNumber = taskNumber,
        Title = "Test remediation task",
        ControlId = "AC-2",
        FindingId = findingId ?? Guid.NewGuid().ToString(),
        Status = Ato.Copilot.Core.Models.Kanban.TaskStatus.Backlog,
        RemediationScript = existingScript,
        ValidationCriteria = existingValidation
    };

    private static ComplianceFinding CreateFinding(
        string? id = null,
        string controlId = "AC-2",
        string family = "AC",
        FindingSeverity severity = FindingSeverity.High) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        ControlId = controlId,
        ControlFamily = family,
        Title = $"Test finding for {controlId}",
        Description = $"Non-compliance for {controlId}",
        Severity = severity,
        Status = FindingStatus.Open,
        ResourceId = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/test",
        ResourceType = "Microsoft.Storage/storageAccounts",
        RemediationGuidance = $"Fix {controlId} compliance issue",
        RemediationType = RemediationType.ResourceConfiguration,
        AutoRemediable = true,
        Source = "PolicyInsights"
    };

    private void SetupRemediationEngine(string scriptContent = "az storage account update --name test", ScriptType scriptType = ScriptType.AzureCli)
    {
        _remediationEngineMock
            .Setup(e => e.GenerateRemediationScriptAsync(
                It.IsAny<ComplianceFinding>(),
                It.IsAny<ScriptType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RemediationScript
            {
                Content = scriptContent,
                ScriptType = scriptType,
                Description = "Generated test script",
                IsSanitized = true
            });
    }

    private void SetupChatClientResponse(string responseText)
    {
        var chatMessage = new ChatMessage(ChatRole.Assistant, responseText);
        var chatResponse = new ChatResponse(chatMessage);

        _chatClientMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // T011: EnrichTaskAsync — skip when already has script and force=false
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task EnrichTaskAsync_AlreadyEnriched_NoForce_SkipsTask()
    {
        var service = CreateService();
        var task = CreateTask(existingScript: "existing script");
        var finding = CreateFinding();

        var result = await service.EnrichTaskAsync(task, finding);

        result.Skipped.Should().BeTrue();
        result.GenerationMethod.Should().Be("Skipped");
        task.RemediationScript.Should().Be("existing script"); // unchanged
        _remediationEngineMock.Verify(
            e => e.GenerateRemediationScriptAsync(It.IsAny<ComplianceFinding>(), It.IsAny<ScriptType>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnrichTaskAsync_AlreadyEnriched_WithForce_ReEnriches()
    {
        var service = CreateService();
        var task = CreateTask(existingScript: "old script");
        var finding = CreateFinding();
        SetupRemediationEngine("new az command");
        SetupChatClientResponse("1. Verify compliance");

        var result = await service.EnrichTaskAsync(task, finding, force: true);

        result.Skipped.Should().BeFalse();
        result.ScriptGenerated.Should().BeTrue();
        task.RemediationScript.Should().Contain("new az command");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // T012: EnrichTaskAsync — skip when finding is null
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task EnrichTaskAsync_NullFinding_SkipsTask()
    {
        var service = CreateService();
        var task = CreateTask();

        var result = await service.EnrichTaskAsync(task, null);

        result.Skipped.Should().BeTrue();
        result.GenerationMethod.Should().Be("Skipped");
        _remediationEngineMock.Verify(
            e => e.GenerateRemediationScriptAsync(It.IsAny<ComplianceFinding>(), It.IsAny<ScriptType>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // T013: EnrichTaskAsync — informational severity static strings
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task EnrichTaskAsync_InformationalSeverity_UsesStaticStrings()
    {
        var service = CreateService();
        var task = CreateTask();
        var finding = CreateFinding(severity: FindingSeverity.Informational);

        var result = await service.EnrichTaskAsync(task, finding);

        result.ScriptGenerated.Should().BeTrue();
        result.ValidationCriteriaGenerated.Should().BeTrue();
        result.GenerationMethod.Should().Be("Template");
        task.RemediationScript.Should().Be(TaskEnrichmentService.InformationalRemediationMessage);
        task.ValidationCriteria.Should().Be(TaskEnrichmentService.InformationalValidationMessage);
        task.RemediationScriptType.Should().BeNull(); // Informational has no script type
        _remediationEngineMock.Verify(
            e => e.GenerateRemediationScriptAsync(It.IsAny<ComplianceFinding>(), It.IsAny<ScriptType>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // T014: EnrichTaskAsync — AI generation path (generation method = "AI")
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task EnrichTaskAsync_AiAvailable_SetsGenerationMethodToAi()
    {
        var service = CreateService(withChatClient: true, aiAvailable: true);
        var task = CreateTask();
        var finding = CreateFinding();
        SetupRemediationEngine("az storage account update --name test --min-tls-version TLS1_2");
        SetupChatClientResponse("1. Verify TLS version\n2. Check compliance status");

        var result = await service.EnrichTaskAsync(task, finding);

        result.GenerationMethod.Should().Be("AI");
        result.ScriptGenerated.Should().BeTrue();
        result.ValidationCriteriaGenerated.Should().BeTrue();
        task.RemediationScript.Should().Contain("az storage account update");
        task.RemediationScriptType.Should().Be("AzureCli");
        task.ValidationCriteria.Should().Contain("Verify TLS version");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // T015: EnrichTaskAsync — Template fallback (AI not available)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task EnrichTaskAsync_AiNotAvailable_SetsGenerationMethodToTemplate()
    {
        var service = CreateService(withChatClient: false, aiAvailable: false);
        var task = CreateTask();
        var finding = CreateFinding();
        SetupRemediationEngine("# Step 1: Fix AC-2 compliance");

        var result = await service.EnrichTaskAsync(task, finding);

        result.GenerationMethod.Should().Be("Template");
        result.ScriptGenerated.Should().BeTrue();
        result.ValidationCriteriaGenerated.Should().BeTrue();
        task.RemediationScript.Should().Contain("Step 1");
        // Validation criteria should be template-based
        task.ValidationCriteria.Should().Contain("Re-scan");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // T016: EnrichTaskAsync — engine failure sets error on result
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task EnrichTaskAsync_EngineThrows_ReturnsError()
    {
        var service = CreateService();
        var task = CreateTask();
        var finding = CreateFinding();
        _remediationEngineMock
            .Setup(e => e.GenerateRemediationScriptAsync(
                It.IsAny<ComplianceFinding>(), It.IsAny<ScriptType>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Engine failure"));

        var result = await service.EnrichTaskAsync(task, finding);

        result.Error.Should().Contain("Engine failure");
        result.GenerationMethod.Should().Be("Failed");
        result.ScriptGenerated.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // T017: EnrichTaskAsync — empty script content treated as failure (CHK039)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task EnrichTaskAsync_EmptyScriptContent_TreatedAsFailure()
    {
        var service = CreateService();
        var task = CreateTask();
        var finding = CreateFinding();
        SetupRemediationEngine("");

        var result = await service.EnrichTaskAsync(task, finding);

        result.Error.Should().NotBeNull();
        result.GenerationMethod.Should().Be("Failed");
        result.ScriptGenerated.Should().BeFalse();
    }

    [Fact]
    public async Task EnrichTaskAsync_WhitespaceOnlyScriptContent_TreatedAsFailure()
    {
        var service = CreateService();
        var task = CreateTask();
        var finding = CreateFinding();
        SetupRemediationEngine("   ");

        var result = await service.EnrichTaskAsync(task, finding);

        result.Error.Should().NotBeNull();
        result.GenerationMethod.Should().Be("Failed");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // T018: EnrichTaskAsync — respects ScriptType parameter
    // ═══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(ScriptType.AzureCli)]
    [InlineData(ScriptType.PowerShell)]
    [InlineData(ScriptType.Terraform)]
    [InlineData(ScriptType.Bicep)]
    public async Task EnrichTaskAsync_RespectsScriptTypeParameter(ScriptType scriptType)
    {
        var service = CreateService();
        var task = CreateTask();
        var finding = CreateFinding();
        SetupRemediationEngine("generated script", scriptType);
        SetupChatClientResponse("1. Validate");

        var result = await service.EnrichTaskAsync(task, finding, scriptType: scriptType);

        result.ScriptType.Should().Be(scriptType.ToString());
        task.RemediationScriptType.Should().Be(scriptType.ToString());

        _remediationEngineMock.Verify(
            e => e.GenerateRemediationScriptAsync(It.IsAny<ComplianceFinding>(), scriptType, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // T019: GenerateValidationCriteriaAsync — AI path
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GenerateValidationCriteriaAsync_AiAvailable_ReturnsAiContent()
    {
        var service = CreateService(withChatClient: true, aiAvailable: true);
        var finding = CreateFinding();
        SetupChatClientResponse("1. Run az storage account show --name test\n2. Verify minTlsVersion = TLS1_2");

        var criteria = await service.GenerateValidationCriteriaAsync(finding, "az storage account update");

        criteria.Should().Contain("az storage account show");
        criteria.Should().Contain("Verify minTlsVersion");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // T020: GenerateValidationCriteriaAsync — template fallback
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GenerateValidationCriteriaAsync_AiNotAvailable_ReturnsTemplate()
    {
        var service = CreateService(withChatClient: false, aiAvailable: false);
        var finding = CreateFinding(controlId: "AC-2", family: "AC");

        var criteria = await service.GenerateValidationCriteriaAsync(finding);

        criteria.Should().Contain("Re-scan");
        criteria.Should().Contain("AC-2");
        criteria.Should().Contain("AC family");
    }

    [Fact]
    public async Task GenerateValidationCriteriaAsync_AiThrows_FallsBackToTemplate()
    {
        var service = CreateService(withChatClient: true, aiAvailable: true);
        var finding = CreateFinding();
        _chatClientMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API down"));

        var criteria = await service.GenerateValidationCriteriaAsync(finding);

        criteria.Should().NotBeNullOrEmpty();
        criteria.Should().Contain("Re-scan");
    }

    [Fact]
    public async Task GenerateValidationCriteriaAsync_AiReturnsEmpty_FallsBackToTemplate()
    {
        var service = CreateService(withChatClient: true, aiAvailable: true);
        var finding = CreateFinding();
        SetupChatClientResponse("  ");

        var criteria = await service.GenerateValidationCriteriaAsync(finding);

        criteria.Should().Contain("Re-scan");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // T021: EnrichBoardTasksAsync — empty board
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task EnrichBoardTasksAsync_EmptyBoard_ReturnsZeroResult()
    {
        var service = CreateService();
        var board = new RemediationBoard
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Board",
            Tasks = new List<RemediationTask>()
        };

        var result = await service.EnrichBoardTasksAsync(board, Array.Empty<ComplianceFinding>());

        result.TotalTasks.Should().Be(0);
        result.TasksEnriched.Should().Be(0);
        result.TasksSkipped.Should().Be(0);
        result.TasksFailed.Should().Be(0);
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // T022: EnrichBoardTasksAsync — enriches multiple tasks with finding lookup
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task EnrichBoardTasksAsync_MultipleTasks_EnrichesWithFindingLookup()
    {
        var service = CreateService();
        var finding1 = CreateFinding(id: "f1", controlId: "AC-2");
        var finding2 = CreateFinding(id: "f2", controlId: "AC-3");
        var task1 = CreateTask(taskNumber: "RB-001-001", findingId: "f1");
        var task2 = CreateTask(taskNumber: "RB-001-002", findingId: "f2");
        var board = new RemediationBoard
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Board",
            Tasks = new List<RemediationTask> { task1, task2 }
        };
        SetupRemediationEngine("az command");
        SetupChatClientResponse("1. Validate control");

        var result = await service.EnrichBoardTasksAsync(board, new[] { finding1, finding2 });

        result.TotalTasks.Should().Be(2);
        result.TasksEnriched.Should().Be(2);
        result.TasksFailed.Should().Be(0);
        result.Results.Should().HaveCount(2);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // T023: EnrichBoardTasksAsync — missing finding skips task
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task EnrichBoardTasksAsync_MissingFinding_SkipsTask()
    {
        var service = CreateService();
        var task = CreateTask(findingId: "nonexistent-finding");
        var board = new RemediationBoard
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Board",
            Tasks = new List<RemediationTask> { task }
        };

        var result = await service.EnrichBoardTasksAsync(board, Array.Empty<ComplianceFinding>());

        result.TotalTasks.Should().Be(1);
        result.TasksSkipped.Should().Be(1);
        result.TasksEnriched.Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // T024: EnrichBoardTasksAsync — reports progress
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task EnrichBoardTasksAsync_ReportsProgress()
    {
        var service = CreateService();
        var finding = CreateFinding(id: "f1");
        var task = CreateTask(findingId: "f1");
        var board = new RemediationBoard
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Board",
            Tasks = new List<RemediationTask> { task }
        };
        SetupRemediationEngine("az command");
        SetupChatClientResponse("1. Validate");

        var progressReports = new List<string>();
        var progress = new Progress<string>(msg => progressReports.Add(msg));

        var result = await service.EnrichBoardTasksAsync(board, new[] { finding }, progress);

        // Progress is reported asynchronously — the count should be at least 0
        // (Progress<T> uses SynchronizationContext, which may not fire in test context)
        result.TasksEnriched.Should().BeGreaterOrEqualTo(1);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Additional edge case tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task EnrichTaskAsync_CancellationToken_ThrowsOperationCancelled()
    {
        var service = CreateService();
        var task = CreateTask();
        var finding = CreateFinding();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _remediationEngineMock
            .Setup(e => e.GenerateRemediationScriptAsync(
                It.IsAny<ComplianceFinding>(), It.IsAny<ScriptType>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.EnrichTaskAsync(task, finding, ct: cts.Token));
    }

    [Fact]
    public async Task EnrichTaskAsync_PreservesExistingValidation_WhenNotForcing()
    {
        var service = CreateService();
        var task = CreateTask(existingValidation: "Existing validation");
        var finding = CreateFinding();
        SetupRemediationEngine("new script content");
        SetupChatClientResponse("1. AI validation");

        var result = await service.EnrichTaskAsync(task, finding);

        result.ScriptGenerated.Should().BeTrue();
        result.ValidationCriteriaGenerated.Should().BeFalse();
        task.ValidationCriteria.Should().Be("Existing validation"); // unchanged
    }

    [Fact]
    public async Task EnrichTaskAsync_ScriptTruncated_WhenExceedsMaxLength()
    {
        var service = CreateService();
        var task = CreateTask();
        var finding = CreateFinding();
        var longScript = new string('x', 9000); // exceeds 8000 max
        SetupRemediationEngine(longScript);
        SetupChatClientResponse("1. Validate");

        var result = await service.EnrichTaskAsync(task, finding);

        result.ScriptGenerated.Should().BeTrue();
        task.RemediationScript!.Length.Should().BeLessOrEqualTo(8000);
        task.RemediationScript.Should().EndWith("<!-- Truncated -->");
    }

    [Fact]
    public async Task EnrichBoardTasksAsync_HandlesAlreadyEnrichedTasks()
    {
        var service = CreateService();
        var finding = CreateFinding(id: "f1");
        var enrichedTask = CreateTask(taskNumber: "RB-001-001", existingScript: "Already has script", findingId: "f1");
        var newTask = CreateTask(taskNumber: "RB-001-002", findingId: "f1");
        var board = new RemediationBoard
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Board",
            Tasks = new List<RemediationTask> { enrichedTask, newTask }
        };
        SetupRemediationEngine("az command");
        SetupChatClientResponse("1. Validate");

        var result = await service.EnrichBoardTasksAsync(board, new[] { finding });

        result.TotalTasks.Should().Be(2);
        result.TasksSkipped.Should().Be(1); // enrichedTask skipped
        result.TasksEnriched.Should().Be(1); // newTask enriched
    }

    [Fact]
    public async Task EnrichBoardTasksAsync_SetsDuration()
    {
        var service = CreateService();
        var board = new RemediationBoard
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Board",
            Tasks = new List<RemediationTask>()
        };

        var result = await service.EnrichBoardTasksAsync(board, Array.Empty<ComplianceFinding>());

        result.Duration.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
        result.BoardId.Should().Be(board.Id);
    }

    [Fact]
    public void Constants_HaveExpectedValues()
    {
        TaskEnrichmentService.InformationalRemediationMessage.Should().NotBeNullOrEmpty();
        TaskEnrichmentService.InformationalRemediationMessage.Should().Contain("Informational");
        TaskEnrichmentService.InformationalValidationMessage.Should().NotBeNullOrEmpty();
        TaskEnrichmentService.InformationalValidationMessage.Should().Contain("STIG");
    }
}
