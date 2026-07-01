using System.Text.Json;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Contracts.MapManagement.Interfaces;
using AgvDispatcher.Core.Contracts.MapManagement.Requests;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Modules.MapModule.Services;
using Prism.Events;
using Xunit;

namespace AgvDispatcher.Tests.Map;

public sealed class MapManagementServiceTests
{
    private readonly EventAggregator _events = new();
    private readonly IMapManagementService _service;

    public MapManagementServiceTests()
    {
        _service = new MockMapManagementService(_events);
    }

    [Fact]
    public async Task CreateDraftAsync_ShouldCreateEditableDraft()
    {
        var result = await _service.CreateDraftAsync(new CreateMapDraftRequest
        {
            Context = MapTestDataBuilder.Context,
            MapName = "Editable map"
        });

        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.DraftId));
        Assert.Equal("Editable map", result.Data.Map.MapName);
        Assert.Equal("draft", result.Data.Map.Version);
    }

    [Fact]
    public async Task GetDraftAsync_WhenDraftExists_ShouldReturnDraft()
    {
        var created = await _service.CreateDraftAsync(new CreateMapDraftRequest
        {
            Context = MapTestDataBuilder.Context,
            MapName = "Test map"
        });

        var result = await _service.GetDraftAsync(new GetMapDraftRequest
        {
            Context = MapTestDataBuilder.Context,
            DraftId = created.Data!.DraftId
        });

        Assert.True(result.Success);
        Assert.Equal(created.Data.DraftId, result.Data!.DraftId);
    }

    [Fact]
    public async Task GetDraftAsync_WhenDraftNotExists_ShouldFail()
    {
        var result = await _service.GetDraftAsync(new GetMapDraftRequest
        {
            Context = MapTestDataBuilder.Context,
            DraftId = "missing"
        });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task SaveDraftAsync_ShouldPersistDraftMapContent()
    {
        var saved = await MapTestDataBuilder.CreateAndSaveDraftAsync(_service);
        var result = await _service.GetDraftAsync(new GetMapDraftRequest
        {
            Context = MapTestDataBuilder.Context,
            DraftId = saved.DraftId
        });

        Assert.Equal(4, result.Data!.Map.Nodes.Count);
        Assert.Equal(3, result.Data.Map.Edges.Count);
        Assert.Single(result.Data.Map.Areas);
        Assert.Single(result.Data.Map.VendorNodeMappings);
    }

    [Fact]
    public async Task ValidateDraftAsync_WithValidDraft_ShouldReturnIsValidTrue()
    {
        var draft = await MapTestDataBuilder.CreateAndSaveDraftAsync(_service);

        var result = await ValidateAsync(draft.DraftId);

        Assert.True(result.Data!.IsValid);
        Assert.Empty(result.Data.Messages);
    }

    [Fact]
    public async Task ValidateDraftAsync_WithEmptyMapId_ShouldReturnValidationMessage()
    {
        var draft = await MapTestDataBuilder.CreateAndSaveDraftAsync(
            _service,
            MapTestDataBuilder.CreateMap(mapId: string.Empty));

        var result = await ValidateAsync(draft.DraftId);

        Assert.False(result.Data!.IsValid);
        Assert.Contains(result.Data.Messages, message => message.Contains("MapId"));
    }

    [Fact]
    public async Task ValidateDraftAsync_WithDuplicateNodeIds_ShouldReturnValidationMessage()
    {
        var draft = await MapTestDataBuilder.CreateAndSaveDraftAsync(
            _service,
            MapTestDataBuilder.CreateMap(duplicateNodeIds: true));

        var result = await ValidateAsync(draft.DraftId);

        Assert.False(result.Data!.IsValid);
        Assert.Contains(result.Data.Messages, message => message.Contains("Node") && message.Contains("N1"));
    }

    [Fact]
    public async Task ValidateDraftAsync_WithDuplicateEdgeIds_ShouldReturnValidationMessage()
    {
        var draft = await MapTestDataBuilder.CreateAndSaveDraftAsync(
            _service,
            MapTestDataBuilder.CreateMap(duplicateEdgeIds: true));

        var result = await ValidateAsync(draft.DraftId);

        Assert.False(result.Data!.IsValid);
        Assert.Contains(result.Data.Messages, message => message.Contains("Edge") && message.Contains("E1"));
    }

    [Fact]
    public async Task PublishDraftAsync_WithValidDraft_ShouldCreateMapVersion()
    {
        var draft = await MapTestDataBuilder.CreateAndSaveDraftAsync(_service);

        var publish = await PublishAsync(draft.DraftId);
        var versions = await _service.GetMapVersionsAsync(new GetMapVersionsRequest
        {
            Context = MapTestDataBuilder.Context,
            MapId = draft.Map.MapId
        });

        Assert.True(publish.Success);
        Assert.False(string.IsNullOrWhiteSpace(publish.Data!.MapId));
        Assert.False(string.IsNullOrWhiteSpace(publish.Data.Version));
        Assert.NotEqual(default, publish.Data.PublishedAt);
        Assert.Contains(versions.Data!, version => version.Version == publish.Data.Version && version.IsCurrent);
    }

    [Fact]
    public async Task PublishDraftAsync_WithInvalidDraft_ShouldFail()
    {
        var draft = await MapTestDataBuilder.CreateAndSaveDraftAsync(
            _service,
            MapTestDataBuilder.CreateMap(mapId: string.Empty));

        var result = await PublishAsync(draft.DraftId);

        Assert.False(result.Success);
        var versions = await _service.GetMapVersionsAsync(new GetMapVersionsRequest
        {
            Context = MapTestDataBuilder.Context,
            MapId = draft.Map.MapId
        });
        Assert.DoesNotContain(versions.Data!, version => version.Version == draft.Map.Version && version.IsCurrent);
    }

    [Fact]
    public async Task PublishDraftAsync_ShouldPublishMapPublishedEvent()
    {
        MapPublishedEvent? observed = null;
        _events.GetEvent<PubSubEvent<MapPublishedEvent>>().Subscribe(value => observed = value);
        var draft = await MapTestDataBuilder.CreateAndSaveDraftAsync(_service);

        var result = await PublishAsync(draft.DraftId);

        Assert.NotNull(observed);
        Assert.Equal(result.Data!.MapId, observed.MapId);
        Assert.Equal(result.Data.Version, observed.MapVersion);
        Assert.Equal(MapTestDataBuilder.Context.OperatorId, observed.OperatorId);
    }

    [Fact]
    public async Task RollbackToVersionAsync_ShouldMarkSelectedVersionCurrent_AndPublishEvent()
    {
        var firstDraft = await MapTestDataBuilder.CreateAndSaveDraftAsync(
            _service,
            MapTestDataBuilder.CreateMap(version: "v1"));
        var firstPublish = await PublishAsync(firstDraft.DraftId);
        var secondDraft = await MapTestDataBuilder.CreateAndSaveDraftAsync(
            _service,
            MapTestDataBuilder.CreateMap(version: "v2"));
        var secondPublish = await PublishAsync(secondDraft.DraftId);
        MapPublishedEvent? observed = null;
        _events.GetEvent<PubSubEvent<MapPublishedEvent>>().Subscribe(value => observed = value);

        var rollback = await _service.RollbackToVersionAsync(new RollbackMapVersionRequest
        {
            Context = MapTestDataBuilder.Context,
            MapId = firstDraft.Map.MapId,
            Version = firstPublish.Data!.Version
        });
        var versions = await _service.GetMapVersionsAsync(new GetMapVersionsRequest
        {
            Context = MapTestDataBuilder.Context,
            MapId = firstDraft.Map.MapId
        });
        var publishedVersions = Assert.IsAssignableFrom<IReadOnlyList<AgvDispatcher.Core.Contracts.MapManagement.Results.MapVersionDto>>(
            versions.Data);

        Assert.True(rollback.Success);
        Assert.True(publishedVersions.Single(version => version.Version == firstPublish.Data!.Version).IsCurrent);
        Assert.False(publishedVersions.Single(version => version.Version == secondPublish.Data!.Version).IsCurrent);
        Assert.Equal(firstPublish.Data!.Version, observed!.MapVersion);
    }

    [Fact]
    public async Task ExportMapAsync_ShouldReturnSerializablePayload()
    {
        var draft = await MapTestDataBuilder.CreateAndSaveDraftAsync(_service);

        var result = await _service.ExportMapAsync(new ExportMapRequest
        {
            Context = MapTestDataBuilder.Context,
            MapId = draft.Map.MapId,
            Format = "json"
        });

        Assert.True(result.Success);
        Assert.Equal("json", result.Data!.Format);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.Payload));
        Assert.NotNull(JsonDocument.Parse(result.Data.Payload));
    }

    [Fact]
    public async Task ImportMapAsync_ShouldCreateDraftFromPayload()
    {
        var map = MapTestDataBuilder.CreateMap();

        var result = await _service.ImportMapAsync(new ImportMapRequest
        {
            Context = MapTestDataBuilder.Context,
            Format = "json",
            Payload = JsonSerializer.Serialize(map)
        });

        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.Draft.DraftId));
        Assert.Equal(map.MapId, result.Data.Draft.Map.MapId);
        Assert.Equal(map.Nodes.Count, result.Data.Draft.Map.Nodes.Count);
        Assert.Equal(map.Edges.Count, result.Data.Draft.Map.Edges.Count);
    }

    private Task<AgvDispatcher.Core.Contracts.Common.AgvResult<AgvDispatcher.Core.Contracts.MapManagement.Results.MapValidationResultDto>>
        ValidateAsync(string draftId) => _service.ValidateDraftAsync(new ValidateMapDraftRequest
        {
            Context = MapTestDataBuilder.Context,
            DraftId = draftId
        });

    private Task<AgvDispatcher.Core.Contracts.Common.AgvResult<AgvDispatcher.Core.Contracts.MapManagement.Results.MapPublishResultDto>>
        PublishAsync(string draftId) => _service.PublishDraftAsync(new PublishMapDraftRequest
        {
            Context = MapTestDataBuilder.Context,
            DraftId = draftId
        });
}
