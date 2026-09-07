using System;
using System.Collections.Generic;

namespace SaluExamPortal.Application.Services;

/// <summary>Toast notification type.</summary>
public enum ToastType { Success, Error, Warning, Info }

/// <summary>A single toast message record.</summary>
public record ToastItem(Guid Id, string Message, ToastType Type);

/// <summary>Interface for showing portal-wide toast notifications.</summary>
public interface IToastService
{
    event Action? OnToastsChanged;
    IReadOnlyList<ToastItem> Toasts { get; }

    void ShowSuccess(string message);
    void ShowError(string message);
    void ShowWarning(string message);
    void ShowInfo(string message);
    void Remove(Guid id);
}

/// <summary>Scoped toast notification service (one instance per SignalR circuit).</summary>
public class ToastService : IToastService
{
    private readonly List<ToastItem> _toasts = new();
    public event Action? OnToastsChanged;
    public IReadOnlyList<ToastItem> Toasts => _toasts.AsReadOnly();

    public void ShowSuccess(string message) => Add(message, ToastType.Success);
    public void ShowError(string message)   => Add(message, ToastType.Error);
    public void ShowWarning(string message) => Add(message, ToastType.Warning);
    public void ShowInfo(string message)    => Add(message, ToastType.Info);

    private void Add(string message, ToastType type)
    {
        var item = new ToastItem(Guid.NewGuid(), message, type);
        _toasts.Add(item);
        OnToastsChanged?.Invoke();

        // Auto-remove after 4.2 seconds
        _ = Task.Delay(4200).ContinueWith(_ =>
        {
            Remove(item.Id);
        });
    }

    public void Remove(Guid id)
    {
        var toast = _toasts.FirstOrDefault(t => t.Id == id);
        if (toast is not null)
        {
            _toasts.Remove(toast);
            OnToastsChanged?.Invoke();
        }
    }
}
