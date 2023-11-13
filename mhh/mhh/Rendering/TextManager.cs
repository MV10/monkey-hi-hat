
using Microsoft.Extensions.Logging;
using OpenTK.Mathematics;

namespace mhh;

// Manages string to text buffer conversions, static and crossfading
// text content, and the TextRenderer object. Currently only supports
// ASCII 32 (space) through ASCII 126 (tilde). Other characters will
// render as a question-mark. Newlines are handled correctly. Content
// exceeding the Rows/Columns count is truncated.

public class TextManager : IDisposable
{
    // Can we make it any more obvious?
    internal static TextManager GetInstanceForRenderManager()
    {
        if (Instance is not null) throw new InvalidOperationException($"{nameof(TextManager)} should be accessed through {nameof(RenderManager)} only");
        Instance = new();
        return Instance;
    }
    private static TextManager Instance = null;

    // TODO move all max-dimension details to config

    // Prefer power-of-two dimensions
    private static readonly int BufferWidth = 64;
    private static readonly int BufferHeight = 8;

    private static readonly int MinChar = ' ';
    private static readonly int MaxChar = '~';
    private static readonly int BadChar = '?';
    private static readonly int NewLineChar = "\n"[0];
    private static readonly int NewLineFlag = 0;

    public int[] TextBuffer;
    public Vector2i Dimensions;
    public float CharSize;
    public Vector2 StartPosition;

    public TextRenderer Renderer { get; private set; }
    public bool HasContent { get; private set; }
    public DateTime LastUpdate { get; private set; }

    private TextManager()
    {
        Dimensions = new(BufferWidth, BufferHeight);
        CharSize = 0.02f;
        StartPosition = (-0.95f, 0.5f);

        Clear();
        Renderer = new();
    }

    public void Clear()
    {
        TextBuffer = new int[BufferWidth * BufferHeight];
        HasContent = false;
        LastUpdate = DateTime.Now;
    }

    public void Write(int starting_row, string content, bool clear = false)
    {
        if (clear) Clear();
        if (string.IsNullOrEmpty(content)) return;

        HasContent = true;
        LastUpdate = DateTime.Now;

        content = content.Replace("\r\n", "\n"); // fucking DOS

        int row = starting_row;
        int col = 0;
        int i = starting_row * Dimensions.X;

        foreach (char c in content)
        {
            if (c == NewLineChar)
            {
                TextBuffer[i] = NewLineFlag;
                row++;
                col = 0;
            }
            else
            {
                TextBuffer[i] = (c < MinChar || c > MaxChar) ? BadChar : c;
                col++;
                if (col == Dimensions.X)
                {
                    row++;
                    col = 0;
                }
            }
            i = row * Dimensions.X + col;
            if (i >= TextBuffer.Length || row >= Dimensions.Y) return;
        }
        if(!content.EndsWith("\n")) TextBuffer[i] = NewLineFlag;
    }

    public void Write(string content, bool clear = true)
        => Write(0, content, clear);

    public void Dispose()
    {
        if (IsDisposed) return;

        LogHelper.Logger?.LogTrace($"{GetType()}.Dispose() ----------------------------");

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() TextRenderer");
        Renderer.Dispose();

        GC.SuppressFinalize(this);
        IsDisposed = true;
    }
    private bool IsDisposed = false;
}
