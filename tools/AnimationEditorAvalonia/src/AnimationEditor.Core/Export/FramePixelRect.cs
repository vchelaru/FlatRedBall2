namespace AnimationEditor.Core.Export;

/// <summary>Integer pixel rectangle of a frame within its source texture.</summary>
public readonly struct FramePixelRect
{
    public FramePixelRect(int x, int y, int w, int h)
    {
        X = x;
        Y = y;
        W = w;
        H = h;
    }

    public int X { get; }
    public int Y { get; }
    public int W { get; }
    public int H { get; }
}
