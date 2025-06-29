using Google.Apis.AndroidPublisher.v3;
using Google.Apis.AndroidPublisher.v3.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;

namespace FivestaRss;

public class GooglePlayReviewService
{
    readonly HttpClient _httpClient;
    readonly IConfiguration _configuration;
    readonly ILogger<GooglePlayReviewService> _logger;
    AndroidPublisherService? _service;

    public GooglePlayReviewService(HttpClient httpClient, IConfiguration configuration, ILogger<GooglePlayReviewService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<ReviewItem>> GetReviewsAsync(string packageName, string appName)
    {
        try
        {
            var service = await GetServiceAsync();
            if (service == null)
            {
                _logger.LogError("Failed to initialize Google Play service");
                return [];
            }

            var reviews = new List<ReviewItem>();
            string? nextToken = null;
            int pageCount = 0;
            const int maxPages = 5;

            do
            {
                pageCount++;
                // Safety limit to prevent infinite loops if API misbehaves
                if (pageCount > maxPages)
                {
                    _logger.LogWarning("Reached maximum page limit ({MaxPages}) for Google Play reviews", maxPages);
                    break;
                }

                var request = service.Reviews.List(packageName);
                if (!string.IsNullOrEmpty(nextToken))
                {
                    request.Token = nextToken;
                }
                
                // Set max results per page (Google Play API only returns reviews from last 7 days)
                request.MaxResults = 100;

                _logger.LogInformation("Fetching Google Play reviews page {PageCount} for {PackageName}", pageCount, packageName);
                
                var response = await request.ExecuteAsync();
                
                if (response.Reviews != null)
                {
                    foreach (var review in response.Reviews)
                    {
                        var reviewItem = ConvertToReviewItem(review, appName);
                        reviews.Add(reviewItem);
                    }
                }

                // Google Play uses token-based pagination - nextPageToken will be null when no more pages
                nextToken = response.TokenPagination?.NextPageToken;
                
                _logger.LogInformation("Retrieved {Count} reviews from page {PageCount}, next token: {HasNextToken}", 
                    response.Reviews?.Count ?? 0, pageCount, !string.IsNullOrEmpty(nextToken) ? "Yes" : "No");

            } while (!string.IsNullOrEmpty(nextToken));

            _logger.LogInformation("Total Google Play reviews retrieved for {PackageName}: {Count}", packageName, reviews.Count);
            
            if (reviews.Count < 10)
            {
                _logger.LogWarning("Google Play API limitation: Only returns reviews from the last 7 days. Retrieved {Count} reviews for {PackageName}", reviews.Count, packageName);
            }
            
            return reviews.OrderByDescending(r => r.ReviewDate).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve Google Play reviews for {PackageName}", packageName);
            throw;
        }
    }

    async Task<AndroidPublisherService?> GetServiceAsync()
    {
        if (_service != null)
            return _service;

        try
        {
            var keyPath = _configuration["ApiKeys:GooglePlay:ServiceAccountKeyPath"];
            if (string.IsNullOrEmpty(keyPath))
            {
                _logger.LogError("Google Play service account key path not configured");
                return null;
            }

            if (!File.Exists(keyPath))
            {
                _logger.LogError("Google Play service account key file not found at {KeyPath}", keyPath);
                return null;
            }

            byte[]? keyBytes = null;
            try
            {
                // Load service account key as bytes to avoid string lingering in memory
                keyBytes = await File.ReadAllBytesAsync(keyPath);
                var keyStream = new MemoryStream(keyBytes);
                
                var credential = GoogleCredential.FromStream(keyStream)
                    .CreateScoped(AndroidPublisherService.Scope.Androidpublisher);

                _service = new AndroidPublisherService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "FivestaRSS"
                });

                _logger.LogInformation("Google Play service initialized successfully");
                return _service;
            }
            finally
            {
                // Clear the key bytes from memory
                if (keyBytes != null)
                {
                    Array.Clear(keyBytes, 0, keyBytes.Length);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Google Play service");
            return null;
        }
    }

    static ReviewItem ConvertToReviewItem(Review review, string appName)
    {
        var authorName = review.AuthorName ?? "Anonymous";
        var reviewText = review.Comments?.FirstOrDefault()?.UserComment?.Text ?? "";
        var rating = review.Comments?.FirstOrDefault()?.UserComment?.StarRating ?? 0;
        var version = review.Comments?.FirstOrDefault()?.UserComment?.AppVersionName;
        
        // Combine device name and Android OS version for richer device information
        var device = CreateDeviceString(
            review.Comments?.FirstOrDefault()?.UserComment?.Device,
            review.Comments?.FirstOrDefault()?.UserComment?.AndroidOsVersion
        );
        
        var reviewDate = DateTime.UtcNow;
        if (review.Comments?.FirstOrDefault()?.UserComment?.LastModified != null)
        {
            var lastModified = review.Comments.FirstOrDefault()?.UserComment?.LastModified;
            if (lastModified?.Seconds != null)
            {
                // Google provides timestamps as Unix epoch seconds - ensure we get UTC DateTime
                reviewDate = DateTimeOffset.FromUnixTimeSeconds(lastModified.Seconds.Value).UtcDateTime;
            }
        }

        // Create unique ID combining review ID and lastModified timestamp to detect edits
        var timestampPart = reviewDate.ToString("yyyyMMddHHmmss");
        var reviewId = $"google-play-{review.ReviewId}-{timestampPart}";

        return new ReviewItem
        {
            Id = reviewId,
            Title = GenerateReviewTitle(reviewText),
            ReviewText = reviewText,
            Rating = rating,
            AuthorName = authorName,
            ReviewDate = reviewDate,
            Version = version,
            Territory = null,
            Device = device,
            AppName = appName,
            Store = "Google Play"
        };
    }

    static string? CreateDeviceString(string? deviceName, int? androidOsVersion)
    {
        if (string.IsNullOrEmpty(deviceName) && !androidOsVersion.HasValue)
            return null;

        if (!string.IsNullOrEmpty(deviceName) && androidOsVersion.HasValue)
        {
            // Both device and OS version available: "Samsung Galaxy S22 (Android 13)"
            return $"{deviceName} (Android {androidOsVersion})";
        }

        if (!string.IsNullOrEmpty(deviceName))
        {
            // Only device name available: "Samsung Galaxy S22"
            return deviceName;
        }
        
        if (androidOsVersion.HasValue)
        {
            // Only OS version available: "Android 13"
            return $"Android {androidOsVersion}";
        }

        return null;
    }

    static string GenerateReviewTitle(string reviewText)
    {
        if (string.IsNullOrEmpty(reviewText))
            return "Review";

        var words = reviewText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var titleWords = words.Take(10).ToArray();
        var title = string.Join(" ", titleWords);
        
        if (words.Length > 10)
            title += "...";

        return title.Length > 100 ? title[..97] + "..." : title;
    }
}