using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Map;

namespace AgvDispatcher.Core.Contracts.MapManagement.Requests
{
    /// <summary>
    /// Requests creation of a new editable map draft.
    /// </summary>
    public sealed class CreateMapDraftRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// </summary>
        public RequestContext Context { get; init; } = new();

        /// <summary>
        /// Gets the map name for the new draft.
        /// </summary>
        public string MapName { get; init; } = string.Empty;

        /// <summary>
        /// Gets an optional source map version to copy.
        /// </summary>
        public string? SourceVersion { get; init; }
    }

    /// <summary>
    /// Requests retrieval of an editable map draft.
    /// </summary>
    public sealed class GetMapDraftRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// </summary>
        public RequestContext Context { get; init; } = new();

        /// <summary>
        /// Gets the draft identifier.
        /// </summary>
        public string DraftId { get; init; } = string.Empty;
    }

    /// <summary>
    /// Requests saving of an editable map draft.
    /// </summary>
    public sealed class SaveMapDraftRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// </summary>
        public RequestContext Context { get; init; } = new();

        /// <summary>
        /// Gets the draft identifier.
        /// </summary>
        public string DraftId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the draft map content.
        /// </summary>
        public MapSnapshotDto Map { get; init; } = new();

        /// <summary>
        /// Gets an optional save note.
        /// </summary>
        public string? Comment { get; init; }
    }

    /// <summary>
    /// Requests validation of a map draft.
    /// </summary>
    public sealed class ValidateMapDraftRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// </summary>
        public RequestContext Context { get; init; } = new();

        /// <summary>
        /// Gets the draft identifier.
        /// </summary>
        public string DraftId { get; init; } = string.Empty;
    }

    /// <summary>
    /// Requests publishing of a map draft as the current runtime map.
    /// </summary>
    public sealed class PublishMapDraftRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// </summary>
        public RequestContext Context { get; init; } = new();

        /// <summary>
        /// Gets the draft identifier.
        /// </summary>
        public string DraftId { get; init; } = string.Empty;

        /// <summary>
        /// Gets an optional publish note.
        /// </summary>
        public string? Comment { get; init; }
    }

    /// <summary>
    /// Requests known map versions.
    /// </summary>
    public sealed class GetMapVersionsRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// </summary>
        public RequestContext Context { get; init; } = new();

        /// <summary>
        /// Gets an optional map identifier filter.
        /// </summary>
        public string? MapId { get; init; }
    }

    /// <summary>
    /// Requests rollback to a previously published map version.
    /// </summary>
    public sealed class RollbackMapVersionRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// </summary>
        public RequestContext Context { get; init; } = new();

        /// <summary>
        /// Gets the map identifier.
        /// </summary>
        public string MapId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the target version.
        /// </summary>
        public string Version { get; init; } = string.Empty;

        /// <summary>
        /// Gets an optional rollback reason.
        /// </summary>
        public string? Reason { get; init; }
    }

    /// <summary>
    /// Requests export of a map draft or version.
    /// </summary>
    public sealed class ExportMapRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// </summary>
        public RequestContext Context { get; init; } = new();

        /// <summary>
        /// Gets the map identifier.
        /// </summary>
        public string MapId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the optional map version. If omitted, the current map may be exported.
        /// </summary>
        public string? Version { get; init; }

        /// <summary>
        /// Gets the export format, for example json.
        /// </summary>
        public string Format { get; init; } = "json";
    }

    /// <summary>
    /// Requests import of a map payload as a draft.
    /// </summary>
    public sealed class ImportMapRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// </summary>
        public RequestContext Context { get; init; } = new();

        /// <summary>
        /// Gets the imported map payload.
        /// </summary>
        public string Payload { get; init; } = string.Empty;

        /// <summary>
        /// Gets the payload format, for example json.
        /// </summary>
        public string Format { get; init; } = "json";

        /// <summary>
        /// Gets an optional import note.
        /// </summary>
        public string? Comment { get; init; }
    }
}
