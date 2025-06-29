using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FivestaRss;

public partial class RssFeedService
{
    readonly IConfiguration _configuration;
    readonly ILogger<RssFeedService> _logger;

    public RssFeedService(IConfiguration configuration, ILogger<RssFeedService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<ReviewItem>> ReadExistingReviewsAsync(string feedFileName)
    {
        var feedPath = GetFeedPath(feedFileName);
        if (!File.Exists(feedPath))
        {
            _logger.LogInformation("Feed file {FeedPath} does not exist, starting with empty list", feedPath);
            return [];
        }

        try
        {
            var xmlContent = await File.ReadAllTextAsync(feedPath);
            var doc = XDocument.Parse(xmlContent);
            var items = new List<ReviewItem>();

            foreach (var item in doc.Descendants("item"))
            {
                var titleElement = item.Element("title");
                var title = titleElement?.Value ?? "";
                
                // Critical: Extract rating from stored stars (★) to preserve data integrity across read-write cycles
                var rating = ExtractRatingFromTitle(title);
                var cleanTitle = RemoveRatingFromTitle(title);

                var description = item.Element("description")?.Value ?? "";

                var reviewItem = new ReviewItem
                {
                    Id = item.Element("guid")?.Value ?? "",
                    Title = ExtractReviewTitleFromTitle(cleanTitle), // Extract just the review text part
                    ReviewText = ExtractReviewTextFromDescription(description),
                    Rating = rating,
                    AuthorName = ExtractAuthorFromDescription(description) ?? "missing name",
                    ReviewDate = ParsePubDate(item.Element("pubDate")?.Value ?? ""),
                    // Re-extract metadata from HTML description since RSS doesn't have native fields for these
                    Version = ExtractVersionFromDescription(description),
                    Territory = ExtractTerritoryFromDescription(description),
                    Device = ExtractDeviceFromDescription(description),
                    AppName = ExtractAppNameFromTitle(cleanTitle),
                    Store = ExtractStoreFromGuid(item.Element("guid")?.Value ?? "")
                };

                items.Add(reviewItem);
            }

            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse existing RSS feed {FeedPath}", feedPath);
            return [];
        }
    }

    public async Task WriteRssFeedAsync(string feedFileName, List<ReviewItem> reviews)
    {
        var feedPath = GetFeedPath(feedFileName);
        var feedDirectory = Path.GetDirectoryName(feedPath);
        
        if (!Directory.Exists(feedDirectory))
        {
            Directory.CreateDirectory(feedDirectory!);
        }

        var maxReviews = _configuration.GetValue("FeedSettings:MaxReviewsPerFeed", 100);
        var reviewsToWrite = reviews.Take(maxReviews).ToList();

        var rss = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("rss",
                new XAttribute("version", "2.0"),
                new XElement("channel",
                    new XElement("title", $"App Reviews - {feedFileName}"),
                    new XElement("link", "http://localhost:5000"),
                    new XElement("description", "Latest app reviews"),
                    new XElement("language", "en-us"),
                    new XElement("lastBuildDate", DateTime.UtcNow.ToString("R")), // RFC 822 date format required by RSS spec
                    reviewsToWrite.Select(CreateRssItem)
                )
            )
        );

        await File.WriteAllTextAsync(feedPath, rss.ToString(), Encoding.UTF8);
        _logger.LogInformation("Written {Count} reviews to feed {FeedPath}", reviewsToWrite.Count, feedPath);
    }

    public async Task AddServiceAlertAsync(string feedFileName, string errorMessage)
    {
        var existingReviews = await ReadExistingReviewsAsync(feedFileName);
        
        var serviceAlert = new ReviewItem
        {
            Id = $"SERVICE_ALERT_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
            Title = "🚨 SERVICE ALERT",
            ReviewText = $"Service error at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC: {System.Web.HttpUtility.HtmlEncode(errorMessage)}",
            Rating = 0,
            AuthorName = "FivestaRSS Service",
            ReviewDate = DateTime.UtcNow,
            AppName = "System",
            Store = "System"
        };

        existingReviews.Insert(0, serviceAlert);
        await WriteRssFeedAsync(feedFileName, existingReviews);
    }

    static XElement CreateRssItem(ReviewItem review)
    {
        var title = CreateTitleWithRating(review);
        var description = CreateDescription(review);

        return new XElement("item",
            new XElement("title", title),
            new XElement("description", description),
            new XElement("pubDate", review.ReviewDate.ToString("R")),
            new XElement("guid", review.Id)
        );
    }

    static string CreateTitleWithRating(ReviewItem review)
    {
        // Use filled (★) and empty (☆) stars to create visual rating that's parseable on read
        var stars = new string('★', review.Rating) + new string('☆', 5 - review.Rating);
        var sanitizedTitle = System.Web.HttpUtility.HtmlEncode(review.Title);
        return $"{stars} {sanitizedTitle}";
    }

    static string CreateDescription(ReviewItem review)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<p><strong>Store:</strong> {System.Web.HttpUtility.HtmlEncode(review.Store)}</p>");

        if (!string.IsNullOrEmpty(review.Version))
        {
            sb.AppendLine($"<p><strong>Version:</strong> {System.Web.HttpUtility.HtmlEncode(review.Version)}</p>");
        }
        
        if (!string.IsNullOrEmpty(review.Territory))
        {
            sb.AppendLine($"<p><strong>Territory:</strong> {System.Web.HttpUtility.HtmlEncode(review.Territory)}</p>");
        }
        
        if (!string.IsNullOrEmpty(review.Device))
        {
            sb.AppendLine($"<p><strong>Device:</strong> {System.Web.HttpUtility.HtmlEncode(review.Device)}</p>");
        }

        sb.AppendLine($"<p><strong>Author:</strong> {System.Web.HttpUtility.HtmlEncode(review.AuthorName)}</p>");
        // Double-encode review text to prevent XSS attacks in RSS readers
        var sanitizedReviewText = System.Web.HttpUtility.HtmlEncode(review.ReviewText);
        sb.AppendLine($"<p>{sanitizedReviewText}</p>");
        
        return sb.ToString();
    }

    static int ExtractRatingFromTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return 0;
        
        var starCount = title.Count(c => c == '★');
        return Math.Max(0, Math.Min(5, starCount));
    }

    static string RemoveRatingFromTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return "";
        
        // Skip the star rating prefix (e.g., "★★★☆☆ ") to get the actual title
        var index = title.IndexOf(' ');
        return index > 0 ? title[(index + 1)..] : title;
    }

    static string ExtractReviewTitleFromTitle(string cleanTitle)
    {
        // No author name to remove since titles no longer include author names
        return cleanTitle ?? "";
    }

    static string ExtractAppNameFromTitle(string cleanTitle)
    {
        // No author name to remove since titles no longer include author names
        return cleanTitle ?? "";
    }

    static string ExtractStoreFromGuid(string guid)
    {
        if (guid.Contains("google-play"))
            return "🅖 Google Play";
        
        return guid.Contains("app-store") 
            ? " App Store" 
            : "Unknown";
    }

    static DateTime ParsePubDate(string pubDate)
    {
        return DateTime.TryParse(pubDate, out var date) 
            ? date 
            : DateTime.UtcNow;
    }

    static string? ExtractAuthorFromDescription(string description)
    {
        if (string.IsNullOrEmpty(description)) return null;

        var authorMatch = AuthorRegex().Match(description);
        return authorMatch.Success ? authorMatch.Groups[1].Value.Trim() : null;
    }
    [GeneratedRegex(@"<p><strong>Author:</strong>\s*([^<]+)</p>")]
    private static partial Regex AuthorRegex();

    static string? ExtractVersionFromDescription(string description)
    {
        if (string.IsNullOrEmpty(description)) return null;
        
        var versionMatch = VersionRegex().Match(description);
        return versionMatch.Success ? versionMatch.Groups[1].Value.Trim() : null;
    }
    [GeneratedRegex(@"<p><strong>Version:</strong>\s*([^<]+)</p>")]
    private static partial Regex VersionRegex();

    static string? ExtractTerritoryFromDescription(string description)
    {
        if (string.IsNullOrEmpty(description)) return null;
        
        var territoryMatch = TerritoryRegex().Match(description);
        return territoryMatch.Success ? territoryMatch.Groups[1].Value.Trim() : null;
    }
    [GeneratedRegex(@"<p><strong>Territory:</strong>\s*([^<]+)</p>")]
    private static partial Regex TerritoryRegex();

    static string? ExtractDeviceFromDescription(string description)
    {
        if (string.IsNullOrEmpty(description)) return null;
        
        var deviceMatch = DeviceRegex().Match(description);
        return deviceMatch.Success ? deviceMatch.Groups[1].Value.Trim() : null;
    }
    [GeneratedRegex(@"<p><strong>Device:</strong>\s*([^<]+)</p>")]
    private static partial Regex DeviceRegex();

    static string ExtractReviewTextFromDescription(string description)
    {
        if (string.IsNullOrEmpty(description)) return "";

        // Find the last <p> tag that contains the actual review text (not metadata)
        // This should be after all the metadata <p><strong>...</strong></p> tags
        var regex = ReviewTextRegex();
        var match = regex.Match(description);

        // Decode HTML entities in the review text
        return match.Success ?
            System.Web.HttpUtility.HtmlDecode(match.Groups[1].Value.Trim()) 
            : "";
    }
    [GeneratedRegex(@"<p>(?!<strong>)([^<].*?)</p>\s*$", RegexOptions.Singleline)]
    private static partial Regex ReviewTextRegex();

    string GetFeedPath(string feedFileName)
    {
        // Sanitize filename to prevent path traversal attacks
        var sanitizedFileName = Path.GetFileName(feedFileName);
        if (string.IsNullOrEmpty(sanitizedFileName) || sanitizedFileName != feedFileName)
        {
            throw new ArgumentException($"Invalid feed filename: {feedFileName}", nameof(feedFileName));
        }
        
        var feedDirectory = _configuration.GetValue<string>("FeedSettings:FeedDirectory", "feeds");
        var fullPath = Path.Combine(feedDirectory, sanitizedFileName);
        
        // Ensure the resolved path is within the feed directory to prevent directory traversal
        var resolvedPath = Path.GetFullPath(fullPath);
        var resolvedDirectory = Path.GetFullPath(feedDirectory);
        
        if (!resolvedPath.StartsWith(resolvedDirectory + Path.DirectorySeparatorChar) && 
            !resolvedPath.Equals(resolvedDirectory))
        {
            throw new ArgumentException($"Feed path outside allowed directory: {feedFileName}", nameof(feedFileName));
        }
        
        return resolvedPath;
    }
}
