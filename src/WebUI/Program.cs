using AiGateway.Application.Common.Interfaces;
using AiGateway.Infrastructure.Data;
using WebUI.Components;
using WebUI.Services;

var builder = WebApplication.CreateBuilder(args);

// Register HttpContextAccessor & IUser implementation for AuditableEntityInterceptor
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUser, CurrentUser>();

// Add service defaults and application/infrastructure services
builder.AddServiceDefaults();
builder.AddApplicationServices();
builder.AddInfrastructureServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    await app.InitialiseDatabaseAsync();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
