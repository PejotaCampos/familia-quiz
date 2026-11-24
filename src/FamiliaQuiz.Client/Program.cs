using FamiliaQuiz.Client;
using FamiliaQuiz.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// In WebAssembly there is one user per app instance, so "scoped" behaves like a singleton.
builder.Services.AddScoped<SessionStore>();
builder.Services.AddScoped<KeyboardService>();
builder.Services.AddScoped<GameClient>();

await builder.Build().RunAsync();
