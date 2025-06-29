using FivestaRss;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<RssFeedService>();

// Register HTTP services with IHttpClientFactory for proper connection pooling and resilience
builder.Services.AddHttpClient<GooglePlayReviewService>();
builder.Services.AddHttpClient<AppStoreReviewService>();

builder.Services.AddHostedService<Worker>();

var app = builder.Build();

var feedDirectory = app.Configuration.GetValue<string>("FeedSettings:FeedDirectory", "feeds");
var feedPath = Path.GetFullPath(feedDirectory);

if (!Directory.Exists(feedPath))
{
    Directory.CreateDirectory(feedPath);
}

app.MapGet("/feeds/{fileName}", async (string fileName, IConfiguration configuration) =>
{
    var monitoredApps = configuration.GetSection("MonitoredApps").Get<List<MonitoredApp>>();
    
    // Security: Validate fileName against configured apps to prevent path traversal
    if (monitoredApps == null || monitoredApps.All(item => item.FeedFileName != fileName))
    {
        return Results.NotFound("Feed not found or not configured");
    }

    var feedFilePath = Path.Combine(feedPath, fileName);
    
    if (!File.Exists(feedFilePath))
    {
        return Results.NotFound("Feed file does not exist");
    }

    try
    {
        var feedContent = await File.ReadAllTextAsync(feedFilePath);
        return Results.Content(feedContent, "application/rss+xml; charset=utf-8");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error reading feed file: {ex.Message}");
    }
});

app.MapGet("/", () => 
{
    var configuration = app.Services.GetRequiredService<IConfiguration>();
    var monitoredApps = configuration.GetSection("MonitoredApps").Get<List<MonitoredApp>>();
    
    if (monitoredApps == null || monitoredApps.Count == 0)
    {
        return Results.Content(@"
<!DOCTYPE html>
<html>
<head>
    <title>FivestaRSS Service</title>
</head>
<body>
    <h1>FivestaRSS Service</h1>
    <p>No monitored apps configured.</p>
</body>
</html>", "text/html");
    }

    var feedsList = string.Join("", monitoredApps.Select(item => 
        $"<li><a href='/feeds/{item.FeedFileName}'>{item.Name}</a></li>"));

    return Results.Content($@"
<!DOCTYPE html>
<html>
<head>
    <title>FivestaRSS Service</title>
</head>
<body>
    <h1>FivestaRSS Service</h1>
    <h2>Available Feeds:</h2>
    <ul>
        {feedsList}
    </ul>
</body>
</html>", "text/html");
});

app.Run();
