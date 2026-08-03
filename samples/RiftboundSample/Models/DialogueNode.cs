namespace RiftboundSample.Models;

public class DialogueNode
{
    public string Id { get; set; } = "";
    public string Speaker { get; set; } = "";
    public string Text { get; set; } = "";
    public string? NextId { get; set; }
    public List<DialogueChoice>? Choices { get; set; }
}

public class DialogueChoice
{
    public string Text { get; set; } = "";
    public string NextId { get; set; } = "";
}
