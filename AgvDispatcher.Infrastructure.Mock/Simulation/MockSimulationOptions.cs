namespace AgvDispatcher.Infrastructure.Mock.Simulation
{
    public sealed class MockSimulationOptions
    {
        public int RollingWindowSize { get; init; } = 2;

        public TimeSpan WaitTimeout { get; init; } = TimeSpan.FromSeconds(10);

        public TimeSpan RetryInterval { get; init; } = TimeSpan.FromSeconds(1);

        public int MaxRetryCount { get; init; } = 3;

        public MockSimulationTimeoutPolicy OnTimeoutPolicy { get; init; } =
            MockSimulationTimeoutPolicy.RetryOnly;

        public bool AutoStartTasks { get; init; }

        public void Validate()
        {
            if (RollingWindowSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(RollingWindowSize), "RollingWindowSize must be positive.");
            }

            if (WaitTimeout < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(WaitTimeout), "WaitTimeout cannot be negative.");
            }

            if (RetryInterval < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(RetryInterval), "RetryInterval cannot be negative.");
            }

            if (MaxRetryCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxRetryCount), "MaxRetryCount cannot be negative.");
            }
        }

        public MockSimulationOptions Clone() => new()
        {
            RollingWindowSize = RollingWindowSize,
            WaitTimeout = WaitTimeout,
            RetryInterval = RetryInterval,
            MaxRetryCount = MaxRetryCount,
            OnTimeoutPolicy = OnTimeoutPolicy,
            AutoStartTasks = AutoStartTasks
        };
    }

    public enum MockSimulationTimeoutPolicy
    {
        RetryOnly = 0,
        FailTask = 1
    }

    public enum MockFaultPolicy
    {
        HoldResources = 0,
        ReleaseReservation = 1,
        FailTaskAndRelease = 2
    }
}
