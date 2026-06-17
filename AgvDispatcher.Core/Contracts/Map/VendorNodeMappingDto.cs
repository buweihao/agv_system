namespace AgvDispatcher.Core.Contracts.Map
{
    public sealed class VendorNodeMappingDto
    {
        public string VendorCode { get; init; } = string.Empty;

        public string SystemNodeId { get; init; } = string.Empty;

        public string VendorNodeCode { get; init; } = string.Empty;
    }
}
