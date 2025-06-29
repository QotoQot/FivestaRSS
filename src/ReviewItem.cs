namespace FivestaRss;

public class ReviewItem
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string ReviewText { get; init; } = string.Empty;
    public int Rating { get; init; }
    public string AuthorName { get; init; } = string.Empty;
    public DateTime ReviewDate { get; init; }
    public string? Version { get; init; }
    public string? Territory { get; init; }
    public string? Device { get; init; }
    public string AppName { get; set; } = string.Empty;
    public string Store { get; init; } = string.Empty;
}