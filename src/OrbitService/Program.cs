using OrbitService.Game;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:6001");

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<GameWebSocketHandler>();

var app = builder.Build();

app.UseCors();
app.UseWebSockets();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Map("/ws", async (HttpContext context, GameWebSocketHandler handler) =>
{
    await handler.HandleAsync(context);
});

app.Run();
