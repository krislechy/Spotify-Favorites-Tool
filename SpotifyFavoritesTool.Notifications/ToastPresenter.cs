namespace SpotifyFavoritesTool;

public sealed class ToastPresenter : IDisposable
{
    private ToastWindow? _currentToast;

    public void Show(FavoriteToggleResult result)
    {
        ShowCore(new ToastWindow(result));
    }

    public void ShowFavoriteStatus(PlaybackTrack track)
    {
        ShowCore(new ToastWindow(track, "Играет"));
    }

    public void ShowTrackChanged(PlaybackTrack track)
    {
        ShowCore(new ToastWindow(track));
    }

    public void ShowError(string title, string message)
    {
        ShowCore(new ToastWindow(title, message));
    }

    public void Dispose()
    {
        _currentToast?.Close();
        _currentToast = null;
    }

    private void ShowCore(ToastWindow toast)
    {
        _currentToast?.Close();
        _currentToast = toast;
        _currentToast.Closed += (_, _) =>
        {
            if (ReferenceEquals(_currentToast, toast))
            {
                _currentToast = null;
            }
        };
        _currentToast.Show();
    }
}
