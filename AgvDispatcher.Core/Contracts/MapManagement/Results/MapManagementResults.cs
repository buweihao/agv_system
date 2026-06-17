using System;
using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Map;

namespace AgvDispatcher.Core.Contracts.MapManagement.Results
{
    /// <summary>
    /// Describes an editable map draft.
    /// </summary>
    public sealed class MapDraftDto
    {
        /// <summary>
        /// Gets the draft identifier.
        /// </summary>
        public string DraftId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the map content of the draft.
        /// </summary>
        public MapSnapshotDto Map { get; init; } = new();

        /// <summary>
        /// Gets the draft creation time.
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

        /// <summary>
        /// Gets the last save time.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;

        /// <summary>
        /// Gets the operator that last changed the draft.
        /// </summary>
        public string? OperatorId { get; init; }
    }

    /// <summary>
    /// Describes the validation result of a map draft.
    /// </summary>
    public sealed class MapValidationResultDto
    {
        /// <summary>
        /// Gets a value indicating whether the draft can be published.
        /// </summary>
        public bool IsValid { get; init; }

        /// <summary>
        /// Gets validation messages.
        /// </summary>
        public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Describes a published map version.
    /// </summary>
    public sealed class MapVersionDto
    {
        /// <summary>
        /// Gets the map identifier.
        /// </summary>
        public string MapId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the map name.
        /// </summary>
        public string MapName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the map version.
        /// </summary>
        public string Version { get; init; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether this version is the current runtime map.
        /// </summary>
        public bool IsCurrent { get; init; }

        /// <summary>
        /// Gets the publish time, if this version has been published.
        /// </summary>
        public DateTimeOffset? PublishedAt { get; init; }

        /// <summary>
        /// Gets the operator that published the version, if any.
        /// </summary>
        public string? OperatorId { get; init; }
    }

    /// <summary>
    /// Describes a successful map publish operation.
    /// </summary>
    public sealed class MapPublishResultDto
    {
        /// <summary>
        /// Gets the map identifier.
        /// </summary>
        public string MapId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the published version.
        /// </summary>
        public string Version { get; init; } = string.Empty;

        /// <summary>
        /// Gets the publish time.
        /// </summary>
        public DateTimeOffset PublishedAt { get; init; } = DateTimeOffset.Now;

        /// <summary>
        /// Gets the operator that published the map.
        /// </summary>
        public string? OperatorId { get; init; }
    }

    /// <summary>
    /// Describes a successful map rollback operation.
    /// </summary>
    public sealed class MapRollbackResultDto
    {
        /// <summary>
        /// Gets the map identifier.
        /// </summary>
        public string MapId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the version that became current after rollback.
        /// </summary>
        public string Version { get; init; } = string.Empty;

        /// <summary>
        /// Gets the rollback time.
        /// </summary>
        public DateTimeOffset RolledBackAt { get; init; } = DateTimeOffset.Now;

        /// <summary>
        /// Gets the operator that requested rollback.
        /// </summary>
        public string? OperatorId { get; init; }
    }

    /// <summary>
    /// Describes an exported map payload.
    /// </summary>
    public sealed class MapExportResultDto
    {
        /// <summary>
        /// Gets the export format.
        /// </summary>
        public string Format { get; init; } = "json";

        /// <summary>
        /// Gets the exported payload.
        /// </summary>
        public string Payload { get; init; } = string.Empty;

        /// <summary>
        /// Gets the export time.
        /// </summary>
        public DateTimeOffset ExportedAt { get; init; } = DateTimeOffset.Now;
    }

    /// <summary>
    /// Describes the result of importing a map payload as a draft.
    /// </summary>
    public sealed class MapImportResultDto
    {
        /// <summary>
        /// Gets the imported draft.
        /// </summary>
        public MapDraftDto Draft { get; init; } = new();

        /// <summary>
        /// Gets import warnings, if any.
        /// </summary>
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    }
}
