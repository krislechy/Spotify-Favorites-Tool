namespace SpotifyFavoritesTool;

public sealed class AsyncActionGate
{
    private bool _isRunning;

    public bool TryEnter(out IDisposable lease)
    {
        if (_isRunning)
        {
            lease = EmptyLease.Instance;
            return false;
        }

        _isRunning = true;
        lease = new Lease(() => _isRunning = false);
        return true;
    }

    private sealed class Lease : IDisposable
    {
        private readonly Action _release;
        private bool _disposed;

        public Lease(Action release)
        {
            _release = release;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _release();
        }
    }

    private sealed class EmptyLease : IDisposable
    {
        public static readonly EmptyLease Instance = new();

        public void Dispose()
        {
        }
    }
}
