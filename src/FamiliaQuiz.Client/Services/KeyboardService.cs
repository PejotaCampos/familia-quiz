using Microsoft.JSInterop;

namespace FamiliaQuiz.Client.Services;

/// <summary>
/// Forwards global key presses (1-9, Enter) from the browser to whichever panel is listening.
/// Panels subscribe to <see cref="KeyPressed"/> and unsubscribe when disposed.
/// </summary>
public sealed class KeyboardService(IJSRuntime js) : IAsyncDisposable
{
    private DotNetObjectReference<KeyboardService>? _self;
    private IJSObjectReference? _registration;

    public event Func<string, Task>? KeyPressed;

    public async Task InitializeAsync()
    {
        if (_registration is not null) return;
        _self = DotNetObjectReference.Create(this);
        _registration = await js.InvokeAsync<IJSObjectReference>("familiaQuiz.registerKeys", _self);
    }

    [JSInvokable]
    public async Task OnKey(string key)
    {
        if (KeyPressed is null) return;
        foreach (var handler in KeyPressed.GetInvocationList().Cast<Func<string, Task>>())
            await handler(key);
    }

    public async ValueTask DisposeAsync()
    {
        if (_registration is not null)
        {
            await _registration.InvokeVoidAsync("dispose");
            await _registration.DisposeAsync();
        }
        _self?.Dispose();
    }
}
