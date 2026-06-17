using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.MapManagement.Requests;
using AgvDispatcher.Core.Contracts.MapManagement.Results;

namespace AgvDispatcher.Core.Contracts.MapManagement.Interfaces
{
    /// <summary>
    /// Defines the contract for map draft editing, validation, publishing, version management, import, and export.
    /// </summary>
    public interface IMapManagementService
    {
        /// <summary>
        /// Creates a new editable map draft.
        /// </summary>
        Task<AgvResult<MapDraftDto>> CreateDraftAsync(
            CreateMapDraftRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets an editable map draft by identifier.
        /// </summary>
        Task<AgvResult<MapDraftDto>> GetDraftAsync(
            GetMapDraftRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves an editable map draft without publishing it.
        /// </summary>
        Task<AgvResult<MapDraftDto>> SaveDraftAsync(
            SaveMapDraftRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates a map draft before publishing or import.
        /// </summary>
        Task<AgvResult<MapValidationResultDto>> ValidateDraftAsync(
            ValidateMapDraftRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes a validated draft as the current runtime map version.
        /// </summary>
        Task<AgvResult<MapPublishResultDto>> PublishDraftAsync(
            PublishMapDraftRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets known map versions for management screens.
        /// </summary>
        Task<AgvResult<IReadOnlyList<MapVersionDto>>> GetMapVersionsAsync(
            GetMapVersionsRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Rolls the current runtime map back to a previously published version.
        /// </summary>
        Task<AgvResult<MapRollbackResultDto>> RollbackToVersionAsync(
            RollbackMapVersionRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports a map version or draft for backup or offline editing.
        /// </summary>
        Task<AgvResult<MapExportResultDto>> ExportMapAsync(
            ExportMapRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Imports a map payload as a new draft.
        /// </summary>
        Task<AgvResult<MapImportResultDto>> ImportMapAsync(
            ImportMapRequest request,
            CancellationToken cancellationToken = default);
    }
}
