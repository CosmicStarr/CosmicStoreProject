namespace Models.AngularDTOs;

public class PublishBulkRequest
{
    public List<string> ProductIds { get; set; } = new();
    public decimal MarkupMultiplier { get; set; } = 1.4m;
}

public class StoreSettingsDto
{
    public decimal DefaultMarkup { get; set; } = 1.4m;
}
