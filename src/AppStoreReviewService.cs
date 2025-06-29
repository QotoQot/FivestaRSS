using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace FivestaRss;

public class AppStoreReviewService
{
    readonly HttpClient _httpClient;
    readonly IConfiguration _configuration;
    readonly ILogger<AppStoreReviewService> _logger;

    public AppStoreReviewService(HttpClient httpClient, IConfiguration configuration, ILogger<AppStoreReviewService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<ReviewItem>> GetReviewsAsync(string appId, string appName)
    {
        try
        {
            var jwt = await GenerateJwtAsync();
            if (string.IsNullOrEmpty(jwt))
            {
                _logger.LogError("Failed to generate JWT token for App Store Connect API");
                return [];
            }

            // Request maximum allowed reviews (200) to minimize missing reviews between polling intervals
            var url = $"https://api.appstoreconnect.apple.com/v1/apps/{appId}/customerReviews?limit=200&sort=-createdDate&fields[customerReviews]=rating,title,body,reviewerNickname,createdDate,territory";
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {jwt}");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "FivestaRSS/1.0");

            _logger.LogInformation("Fetching App Store reviews for app {AppId}", appId);
            
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("HTTP {StatusCode} from App Store Connect API endpoint", response.StatusCode);
                throw new HttpRequestException($"App Store API request failed: {response.StatusCode}");
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("App Store API response received with {Length} characters", jsonContent.Length);
            }
            
            AppStoreReviewsResponse? apiResponse;
            try
            {
                apiResponse = JsonSerializer.Deserialize<AppStoreReviewsResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse App Store API response. First 500 chars: {ResponsePreview}", 
                    jsonContent.Length > 500 ? jsonContent[..500] : jsonContent);
                throw new HttpRequestException($"App Store API response parsing failed: {ex.Message}", ex);
            }

            var reviews = new List<ReviewItem>();
            
            if (apiResponse?.Data != null)
            {
                foreach (var review in apiResponse.Data)
                {
                    var reviewItem = ConvertToReviewItem(review, appName);
                    reviews.Add(reviewItem);
                }
            }

            _logger.LogInformation("Retrieved {Count} App Store reviews for app {AppId}", reviews.Count, appId);
            return reviews;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve App Store reviews for app {AppId}", appId);
            throw;
        }
    }

    async Task<string?> GenerateJwtAsync()
    {
        var keyPath = _configuration["ApiKeys:AppStoreConnect:PrivateKeyPath"];
        if (string.IsNullOrEmpty(keyPath))
        {
            _logger.LogError("App Store Connect private key path not configured");
            return null;
        }

        if (!File.Exists(keyPath))
        {
            _logger.LogError("App Store Connect private key file not found at {KeyPath}", keyPath);
            return null;
        }

        var issuerId = _configuration["ApiKeys:AppStoreConnect:IssuerId"];
        var keyId = _configuration["ApiKeys:AppStoreConnect:KeyId"];

        if (string.IsNullOrEmpty(issuerId) || string.IsNullOrEmpty(keyId))
        {
            _logger.LogError("Missing required configuration for JWT generation");
            return null;
        }

        byte[]? keyBytes = null;
        try
        {
            // Load private key as bytes to avoid string lingering in memory
            keyBytes = await File.ReadAllBytesAsync(keyPath);
            
            // Extract the base64 content between PEM headers
            var keyText = Encoding.UTF8.GetString(keyBytes);
            var base64Key = keyText
                .Replace("-----BEGIN PRIVATE KEY-----", "")
                .Replace("-----END PRIVATE KEY-----", "")
                .Replace("\n", "")
                .Replace("\r", "")
                .Trim();

            var pkcs8Bytes = Convert.FromBase64String(base64Key);
            
            try
            {
                using var key = ECDsa.Create();
                key.ImportPkcs8PrivateKey(pkcs8Bytes, out _);

                var securityKey = new ECDsaSecurityKey(key) { KeyId = keyId };
                var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256);

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Issuer = issuerId,
                    Audience = "appstoreconnect-v1", // Required audience for App Store Connect API
                    Claims = new Dictionary<string, object>(),
                    Expires = DateTime.UtcNow.AddMinutes(20), // Apple allows max 20 minutes
                    SigningCredentials = credentials
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var token = tokenHandler.CreateToken(tokenDescriptor);
                return tokenHandler.WriteToken(token);
            }
            finally
            {
                // Clear the PKCS8 bytes
                Array.Clear(pkcs8Bytes, 0, pkcs8Bytes.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate JWT token");
            return null;
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

    static ReviewItem ConvertToReviewItem(AppStoreReview review, string appName)
    {
        var attributes = review.Attributes ?? new AppStoreReviewAttributes();
        
        var reviewDate = DateTime.UtcNow;
        if (DateTime.TryParse(attributes.CreatedDate, out var parsedDate))
        {
            reviewDate = parsedDate;
        }

        // Create unique ID combining review ID and creation timestamp
        var timestampPart = reviewDate.ToString("yyyyMMddHHmmss");
        var reviewId = $"app-store-{review.Id}-{timestampPart}";

        return new ReviewItem
        {
            Id = reviewId,
            Title = !string.IsNullOrEmpty(attributes.Title) 
                ? attributes.Title 
                : GenerateReviewTitle(attributes.Body ?? ""),
            ReviewText = attributes.Body ?? "",
            Rating = attributes.Rating,
            AuthorName = attributes.ReviewerNickname ?? "Anonymous",
            ReviewDate = reviewDate,
            Version = null,
            Territory = attributes.Territory,
            Device = null, // App Store doesn't provide device information
            AppName = appName,
            Store = "App Store"
        };
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

public class AppStoreReviewsResponse
{
    public List<AppStoreReview>? Data { get; set; }
}

public class AppStoreReview
{
    public string Id { get; set; } = string.Empty;
    public AppStoreReviewAttributes? Attributes { get; set; }
}

public class AppStoreReviewAttributes
{
    public int Rating { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? ReviewerNickname { get; set; }
    public string? CreatedDate { get; set; }
    public string? Territory { get; set; }
}