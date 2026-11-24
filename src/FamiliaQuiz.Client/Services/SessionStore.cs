using System.Text.Json;
using Microsoft.JSInterop;

namespace FamiliaQuiz.Client.Services;

/// <summary>What the browser remembers so a player can rejoin as themselves after a reload or drop.</summary>
public sealed record StoredSession(string RoomCode, string PlayerToken);

/// <summary>Thin wrapper over localStorage (via JS interop).</summary>
public sealed class SessionStore(IJSRuntime js)
{
    private const string SessionKey = "familiaquiz.session";
    private const string NameKey = "familiaquiz.name";

    public async Task<StoredSession?> LoadAsync()
    {
        var json = await js.InvokeAsync<string?>("familiaQuiz.storageGet", SessionKey);
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<StoredSession>(json); }
        catch (JsonException) { return null; }
    }

    public Task SaveAsync(StoredSession session) =>
        js.InvokeVoidAsync("familiaQuiz.storageSet", SessionKey, JsonSerializer.Serialize(session)).AsTask();

    public Task ClearAsync() => js.InvokeVoidAsync("familiaQuiz.storageRemove", SessionKey).AsTask();

    public async Task<string> LoadNameAsync() =>
        await js.InvokeAsync<string?>("familiaQuiz.storageGet", NameKey) ?? "";

    public Task SaveNameAsync(string name) =>
        js.InvokeVoidAsync("familiaQuiz.storageSet", NameKey, name).AsTask();
}
