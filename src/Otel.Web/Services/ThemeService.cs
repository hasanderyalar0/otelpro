using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;

namespace Otel.Web.Services;

public class ThemeService(ProtectedLocalStorage storage, IJSRuntime js)
{
    public string CurrentTheme { get; private set; } = "dark";
    public bool IsDark => CurrentTheme == "dark";
    public bool IsLight => CurrentTheme == "light";

    public event Action? Changed;
    private bool _loaded;

    public async Task InitAsync()
    {
        if (_loaded) return;
        try
        {
            var r = await storage.GetAsync<string>("theme");
            if (r.Success && r.Value is "dark" or "light")
            {
                CurrentTheme = r.Value!;
            }
            else
            {
                try
                {
                    var jsTheme = await js.InvokeAsync<string>("eval", "localStorage.getItem('otel_theme') || 'dark'");
                    if (jsTheme is "dark" or "light") CurrentTheme = jsTheme;
                }
                catch { }
            }
        }
        catch
        {
            try
            {
                var jsTheme = await js.InvokeAsync<string>("eval", "localStorage.getItem('otel_theme') || 'dark'");
                if (jsTheme is "dark" or "light") CurrentTheme = jsTheme;
            }
            catch { }
        }

        try
        {
            await js.InvokeVoidAsync("eval", $"document.documentElement.setAttribute('data-theme', '{CurrentTheme}'); localStorage.setItem('otel_theme', '{CurrentTheme}');");
        }
        catch { }

        _loaded = true;
    }

    public async Task SetThemeAsync(string theme)
    {
        if (theme != "dark" && theme != "light") theme = "dark";
        if (CurrentTheme == theme && _loaded) return;

        CurrentTheme = theme;
        try
        {
            await storage.SetAsync("theme", theme);
        }
        catch { }

        try
        {
            await js.InvokeVoidAsync("eval", $"document.documentElement.setAttribute('data-theme', '{theme}'); localStorage.setItem('otel_theme', '{theme}');");
        }
        catch { }

        Changed?.Invoke();
    }

    public async Task ToggleThemeAsync()
    {
        await SetThemeAsync(CurrentTheme == "dark" ? "light" : "dark");
    }
}
