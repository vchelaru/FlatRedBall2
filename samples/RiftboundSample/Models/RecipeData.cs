namespace RiftboundSample.Models;

public class RecipeData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public Dictionary<string, int> Materials { get; set; } = [];
    public string OutputItemId { get; set; } = "";
    public int OutputCount { get; set; } = 1;
    public string World { get; set; } = "overworld";
}
