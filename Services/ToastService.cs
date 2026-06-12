namespace PokemonGen3Hack.Services {
  public enum ToastType {
    Success,
    Loading,
    Error,
    Info
  }
  public class ToastMessage {
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Message { get; set; } = string.Empty;
    public ToastType Type { get; set; }
    public int DurationMs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
  }
  public class ToastService {
    private readonly List<ToastMessage> _toasts = [];
    private readonly Queue<ToastMessage> _pendingToasts = new();
    private const int MaxToasts = 3;
    public event Action? OnChange;
    public IReadOnlyList<ToastMessage> Toasts => _toasts.AsReadOnly();
    public int PendingCount => _pendingToasts.Count;
    public void ShowSuccess(string message, int duration = 3000) {
      // Remove all loading toasts when showing success
      var loadingToasts = _toasts.Where(t => t.Type == ToastType.Loading).ToList();
      foreach (var toast in loadingToasts) {
        _toasts.Remove(toast);
      }
      AddToast(new ToastMessage {
        Message = message,
        Type = ToastType.Success,
        DurationMs = duration
      });
    }
    public void ShowLoading(string message) {
      AddToast(new ToastMessage {
        Message = message,
        Type = ToastType.Loading,
        DurationMs = 0 // No auto-dismiss for loading toasts
      });
    }
    public void ShowError(string message, int duration = 5000) {
      AddToast(new ToastMessage {
        Message = message,
        Type = ToastType.Error,
        DurationMs = duration
      });
    }
    public void ShowInfo(string message, int duration = 3000) {
      AddToast(new ToastMessage {
        Message = message,
        Type = ToastType.Info,
        DurationMs = duration
      });
    }
    public void Dismiss(string id) {
      var toast = _toasts.FirstOrDefault(t => t.Id == id);
      if (toast != null) {
        _toasts.Remove(toast);
        TryShowNextPending();
        NotifyStateChanged();
      }
    }
    public void DismissAll() {
      _toasts.Clear();
      _pendingToasts.Clear();
      NotifyStateChanged();
    }
    private void AddToast(ToastMessage toast) {
      // If at max capacity, queue the toast
      if (_toasts.Count >= MaxToasts) {
        _pendingToasts.Enqueue(toast);
      } else {
        _toasts.Add(toast);
        NotifyStateChanged();
      }
    }
    private void TryShowNextPending() {
      // Try to show next pending toast if there's space
      while (_toasts.Count < MaxToasts && _pendingToasts.Count > 0) {
        var nextToast = _pendingToasts.Dequeue();
        _toasts.Add(nextToast);
      }
    }
    private void NotifyStateChanged() {
      OnChange?.Invoke();
    }
  }
}
