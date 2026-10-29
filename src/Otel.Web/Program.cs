using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Otel.Web.Components;
using Otel.Web.Data;
using Otel.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddDbContextFactory<AppDb>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDb>>().CreateDbContext());

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<I18n>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<MessagingService>();
builder.Services.AddScoped<AiIntelligenceService>();
builder.Services.AddScoped<AiCopilotService>();
builder.Services.AddScoped<NearbyService>();
builder.Services.AddScoped<HotelState>();
builder.Services.AddScoped<TripPlannerService>();
builder.Services.AddScoped<YieldRadarService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddSingleton<MachineLearningEngine>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

await DbInitializer.InitializeAsync(app.Services);

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
