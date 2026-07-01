using System;
using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Core.Models
{
    public class MapVersionEntity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string MapId { get; set; } = string.Empty;

        public string MapVersion { get; set; } = "v1";

        public string Name { get; set; } = string.Empty;

        public MapState State { get; set; } = MapState.Draft;

        public string? BaseMapId { get; set; }

        public string? BaseMapVersion { get; set; }

        public bool IsActive { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

        public DateTimeOffset? PublishedAt { get; set; }

        public DateTimeOffset? ActivatedAt { get; set; }

        public string? CreatedBy { get; set; }

        public string? Description { get; set; }
    }
}
