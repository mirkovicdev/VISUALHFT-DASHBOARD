using System.Net.WebSockets;
using VisualHFT.WebServer;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config", Watch = true)]

var log = log4net.LogManager.GetLogger(typeof(Program));

Console.WriteLine("=== VisualHFT WebSocket Server ===");
Console.WriteLine();

// Initialize license (COMMUNITY level — no-op, but required before plugin loading)
LicenseManager.Instance.LoadFromKeygen();
Console.WriteLine("[OK] License manager initialized");

// Load plugins
var pluginDir = args.Length > 0 ? args[0] : AppDomain.CurrentDomain.BaseDirectory;
Console.WriteLine($"[..] Loading plugins from: {pluginDir}");

var pluginLoader = new HeadlessPluginLoader();
pluginLoader.LoadPlugins(pluginDir);

Console.WriteLine($"[OK] {pluginLoader.AllPlugins.Count} plugins loaded:");
foreach (var p in pluginLoader.AllPlugins)
    Console.WriteLine($"     - {p.Name} ({p.PluginType})");
Console.WriteLine();

// Configure study plugins to match Binance connector (ProviderID=1, symbol=BTC/USD)
// Without this, studies have empty settings and silently drop all data
pluginLoader.ConfigureStudies("BTC/USD", 1, "Binance");
Console.WriteLine();

// Start Kestrel FIRST so the server is reachable while plugins connect
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5000");
builder.Logging.ClearProviders(); // suppress ASP.NET noise

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://127.0.0.1:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var broadcaster = new WebSocketBroadcaster();

var app = builder.Build();
app.UseCors();
app.UseWebSockets();

app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var ws = await context.WebSockets.AcceptWebSocketAsync();
        Console.WriteLine($"[WS] Client connected (total: {broadcaster.ClientCount + 1})");
        await broadcaster.HandleClientAsync(ws, context.RequestAborted);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    plugins = pluginLoader.AllPlugins.Count,
    clients = broadcaster.ClientCount
}));

// Start the web server in background
_ = app.StartAsync();
Console.WriteLine("[OK] WebSocket server listening on ws://localhost:5000/ws");
Console.WriteLine("[OK] Health check at http://localhost:5000/health");
Console.WriteLine();

// Now start plugins (this can take time — Binance connects via WebSocket)
Console.WriteLine("[..] Starting plugins (connecting to exchanges)...");
Console.WriteLine("     This may take 10-30 seconds for WebSocket handshakes.");
Console.WriteLine();

try
{
    // Give plugins 60 seconds max to start, then continue anyway
    var startTask = pluginLoader.StartPluginsAsync();
    if (await Task.WhenAny(startTask, Task.Delay(60_000)) != startTask)
    {
        Console.WriteLine("[!!] Some plugins still connecting — server is running anyway.");
    }
    else
    {
        await startTask; // propagate exceptions if any
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[!!] Plugin startup error: {ex.Message}");
    Console.WriteLine("     Server will continue — some data feeds may not work.");
}

// Wire up bridges (subscribe to data events)
var marketDataBridge = new MarketDataBridge(broadcaster);
var studyBridge = new StudyBridge(broadcaster, pluginLoader.AllPlugins);

Console.WriteLine();
Console.WriteLine("=================================");
Console.WriteLine("  Server is READY");
Console.WriteLine("  Open http://localhost:3000");
Console.WriteLine("  Press Ctrl+C to stop");
Console.WriteLine("=================================");
Console.WriteLine();

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("[..] Shutting down...");
    studyBridge.Dispose();
    marketDataBridge.Dispose();
    pluginLoader.UnloadPlugins();
    Console.WriteLine("[OK] Shutdown complete.");
});

await app.WaitForShutdownAsync();
