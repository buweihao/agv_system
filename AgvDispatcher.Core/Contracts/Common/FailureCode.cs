namespace AgvDispatcher.Core.Contracts.Common
{
    public enum FailureCode
    {
        None = 0,

        UnknownError = 1000,
        InvalidRequest = 1001,
        InvalidState = 1002,
        Timeout = 1003,
        Canceled = 1004,
        NotSupported = 1005,

        MapNotLoaded = 2000,
        MapVersionMismatch = 2001,
        MapNodeNotFound = 2002,
        MapEdgeNotFound = 2003,
        MapNodeDisabled = 2004,
        MapEdgeDisabled = 2005,
        VendorNodeMappingNotFound = 2006
    }
}
