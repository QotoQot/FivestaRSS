namespace FivestaRss;

public class Worker : BackgroundService
{
    readonly ILogger<Worker> _logger;
    readonly IConfiguration _configuration;
    readonly RssFeedService _rssFeedService;
    readonly GooglePlayReviewService _googlePlayService;
    readonly AppStoreReviewService _appStoreService;

    public Worker(
        ILogger<Worker> logger,
        IConfiguration configuration,
        RssFeedService rssFeedService,
        GooglePlayReviewService googlePlayService,
        AppStoreReviewService appStoreService)
    {
        _logger = logger;
        _configuration = configuration;
        _rssFeedService = rssFeedService;
        _googlePlayService = googlePlayService;
        _appStoreService = appStoreService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FivestaRSS Worker Service started");
        
        var pollingInterval = TimeSpan.FromMinutes(
            _configuration.GetValue<int>("FeedSettings:PollingIntervalMinutes", 60));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting review polling cycle at {Time}", DateTime.UtcNow);
                await ProcessAllAppsAsync();
                _logger.LogInformation("Completed review polling cycle");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during review polling cycle");
            }

            try
            {
                await Task.Delay(pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Worker service cancellation requested");
                break;
            }
        }

        _logger.LogInformation("FivestaRSS Worker Service stopped");
    }

    async Task ProcessAllAppsAsync()
    {
        var monitoredApps = _configuration.GetSection("MonitoredApps").Get<List<MonitoredApp>>();
        
        if (monitoredApps == null || !monitoredApps.Any())
        {
            _logger.LogWarning("No monitored apps configured");
            return;
        }

        foreach (var app in monitoredApps)
        {
            await ProcessSingleAppAsync(app);
        }
    }

    async Task ProcessSingleAppAsync(MonitoredApp app)
    {
        _logger.LogInformation("Processing app: {AppName}", app.Name);

        try
        {
            var existingReviews = await _rssFeedService.ReadExistingReviewsAsync(app.FeedFileName);
            var allReviews = new List<ReviewItem>(existingReviews);
            var newReviewsAdded = false;

            if (!string.IsNullOrEmpty(app.GooglePlayId))
            {
                try
                {
                    _logger.LogInformation("Fetching Google Play reviews for {AppName}", app.Name);
                    var googlePlayReviews = await _googlePlayService.GetReviewsAsync(app.GooglePlayId, app.Name);
                    
                    // Only add reviews that don't already exist (by ID) to avoid duplicates
                    var newGooglePlayReviews = googlePlayReviews
                        .Where(r => !existingReviews.Any(e => e.Id == r.Id))
                        .ToList();

                    if (newGooglePlayReviews.Any())
                    {
                        allReviews.AddRange(newGooglePlayReviews);
                        newReviewsAdded = true;
                        _logger.LogInformation("Added {Count} new Google Play reviews for {AppName}", 
                            newGooglePlayReviews.Count, app.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch Google Play reviews for {AppName}", app.Name);
                    await _rssFeedService.AddServiceAlertAsync(app.FeedFileName, 
                        $"Google Play API Error: {ex.Message}");
                }
            }

            if (!string.IsNullOrEmpty(app.AppStoreId))
            {
                try
                {
                    _logger.LogInformation("Fetching App Store reviews for {AppName}", app.Name);
                    var appStoreReviews = await _appStoreService.GetReviewsAsync(app.AppStoreId, app.Name);
                    
                    var newAppStoreReviews = appStoreReviews
                        .Where(r => !existingReviews.Any(e => e.Id == r.Id))
                        .ToList();

                    if (newAppStoreReviews.Any())
                    {
                        allReviews.AddRange(newAppStoreReviews);
                        newReviewsAdded = true;
                        _logger.LogInformation("Added {Count} new App Store reviews for {AppName}", 
                            newAppStoreReviews.Count, app.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch App Store reviews for {AppName}", app.Name);
                    await _rssFeedService.AddServiceAlertAsync(app.FeedFileName, 
                        $"App Store API Error: {ex.Message}");
                }
            }

            if (newReviewsAdded || !existingReviews.Any())
            {
                var sortedReviews = allReviews
                    .OrderByDescending(r => r.ReviewDate)
                    .ToList();

                await _rssFeedService.WriteRssFeedAsync(app.FeedFileName, sortedReviews);
                _logger.LogInformation("Updated RSS feed for {AppName} with {Count} total reviews", 
                    app.Name, sortedReviews.Count);
            }
            else
            {
                _logger.LogInformation("No new reviews found for {AppName}", app.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process app {AppName}", app.Name);
            
            try
            {
                await _rssFeedService.AddServiceAlertAsync(app.FeedFileName, 
                    $"General Processing Error: {ex.Message}");
            }
            catch (Exception alertEx)
            {
                _logger.LogError(alertEx, "Failed to add service alert for {AppName}", app.Name);
            }
        }
    }
}

public class MonitoredApp
{
    public string Name { get; set; } = string.Empty;
    public string? GooglePlayId { get; set; }
    public string? AppStoreId { get; set; }
    public string FeedFileName { get; set; } = string.Empty;
}
