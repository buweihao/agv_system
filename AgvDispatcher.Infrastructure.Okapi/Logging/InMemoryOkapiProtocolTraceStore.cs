namespace AgvDispatcher.Infrastructure.Okapi
{
    public sealed class InMemoryOkapiProtocolTraceStore : IOkapiProtocolTraceStore
    {
        private const int Capacity = 200;
        private readonly Queue<OkapiProtocolTraceRecord> _records = new(Capacity);
        private readonly object _syncRoot = new();

        public void Add(OkapiProtocolTraceRecord record)
        {
            lock (_syncRoot)
            {
                while (_records.Count >= Capacity)
                {
                    _records.Dequeue();
                }

                _records.Enqueue(record);
            }
        }

        public IReadOnlyList<OkapiProtocolTraceRecord> GetLatest(int count = 200)
        {
            lock (_syncRoot)
            {
                return _records
                    .Reverse()
                    .Take(Math.Clamp(count, 0, Capacity))
                    .ToArray();
            }
        }
    }
}
