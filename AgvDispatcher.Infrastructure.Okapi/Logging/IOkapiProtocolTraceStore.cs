namespace AgvDispatcher.Infrastructure.Okapi
{
    public interface IOkapiProtocolTraceStore
    {
        void Add(OkapiProtocolTraceRecord record);

        IReadOnlyList<OkapiProtocolTraceRecord> GetLatest(int count = 200);
    }
}
