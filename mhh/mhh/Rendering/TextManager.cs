
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
    private static readonly int MinChar = ' ';
    private static readonly int MaxChar = '~';
    private static readonly int BadChar = '?';
    private static readonly int NewLineChar = "\n"[0];
    private static readonly int NewLineFlag = 0;

    // Uniforms set by TextRenderer
    public int[] TextBuffer;
    public Vector2i Dimensions;
    public float CharSize;
    public Vector2 StartPosition;
    public float OutlineWeight;
    public float FadeLevel;

    public TextRenderer Renderer { get; private set; }
    public bool HasContent { get; private set; }
    public DateTime LastUpdate { get; private set; }

    private bool PermanentOverlays = false;
    private Func<string> OverlayTextProvider;
    private DateTime ClearOverlay = DateTime.MaxValue;
    private DateTime UpdateOverlay = DateTime.MaxValue;
    
    // Stages:
    // 0 inactive
    // 1 fade-in (1sec)
    // 2 delay (2 sec)
    // 3 fade-out (1 sec)
    private int PopupStage = 0;
    private DateTime PopupStageStart;
    private DateTime PopupStageEnd;

    public TextManager()
    {
        Dimensions = new(Program.AppConfig.TextBufferX, Program.AppConfig.TextBufferY);
        CharSize = Program.AppConfig.CharacterSize;
        StartPosition = (Program.AppConfig.PositionX, Program.AppConfig.PositionY);
        OutlineWeight = Program.AppConfig.OutlineWeight;
        PermanentOverlays = Program.AppConfig.OverlayPermanent;

        Reset();
        Renderer = new();
    }

    /// <summary>
    /// Sets a string-returning function as the source for overlay text.
    /// This function will be re-queried every OverlayUpdateMS millisec. If
    /// overlays are in timed mode, it will last for OverlaySeconds, otherwise
    /// the user must dismiss the overlay. Popup content will also dismiss
    /// a timed overlay.
    /// </summary>
    public void SetOverlayText(Func<string> provider, bool forcePermanence = false)
    {
        if (forcePermanence) PermanentOverlays = true;
        Reset();
        ClearOverlay = DateTime.Now.AddSeconds(Program.AppConfig.OverlayVisibilitySeconds);
        UpdateOverlay = DateTime.Now.AddMilliseconds(Program.AppConfig.OverlayUpdateMilliseconds);
        OverlayTextProvider = provider;
        Write(OverlayTextProvider.Invoke());
    }

    /// <summary>
    /// Provides text that is faded in, displayed for PopupSeconds, then
    /// faded out. If an overlay is already visible, in timed mode that
    /// overlay will be cleared. In permanent mode, the popup will be
    /// ignored. Fade in/out speed is based on PopupFadeMS.
    /// </summary>
    public void SetPopupText(string content)
    {
        if (PermanentOverlays && HasContent && PopupStage == 0) return;
        Reset();
        Write(content);
        PopupStage = 1;
        PopupStageStart = DateTime.Now;
        PopupStageEnd = DateTime.Now.AddMilliseconds(Program.AppConfig.PopupFadeMilliseconds);
    }

    /// <summary>
    /// Toggles permanent overlay mode on and off. If an overlay is currently
    /// visible when permanence is restored, it will adhere to the original
    /// schedule (so at 10+ seconds it will be cleared).
    /// </summary>
    public string TogglePermanence()
    {
        PermanentOverlays = !PermanentOverlays;
        return $"ACK (overlays now {(PermanentOverlays ? "permanent" : "timed")})";
    }

    /// <summary>
    /// RenderManager calls this to notify TextManager that a frame is
    /// going to be rendered. Nothing is actually rendered by this function.
    /// Used to update time-based content and variables.
    /// </summary>
    public void BeforeRenderFrame()
    {
        if (!HasContent) return;

        if(!PermanentOverlays && DateTime.Now >= ClearOverlay)
        {
            Reset();
            return;
        }

        if(PopupStage > 0)
        {
            if(DateTime.Now >= PopupStageEnd)
            {
                if(PopupStage == 3)
                {
                    Reset();
                    return;
                }

                PopupStage++;
                PopupStageStart = DateTime.Now;
                PopupStageEnd = (PopupStage == 2) 
                    ? DateTime.Now.AddSeconds(Program.AppConfig.PopupVisibilitySeconds) 
                    : DateTime.Now.AddMilliseconds(Program.AppConfig.PopupFadeMilliseconds);

            }

            FadeLevel = PopupStage switch
            {
                1 => 1.0f - (float)PopupStageEnd.Subtract(DateTime.Now).TotalMilliseconds / (float)Program.AppConfig.PopupFadeMilliseconds,
                3 => (float)PopupStageEnd.Subtract(DateTime.Now).TotalMilliseconds / (float)Program.AppConfig.PopupFadeMilliseconds,
                _ => 1.0f
            };
        }

        if (DateTime.Now >= UpdateOverlay && OverlayTextProvider is not null)
        {
            UpdateOverlay = DateTime.Now.AddMilliseconds(Program.AppConfig.OverlayUpdateMilliseconds);
            Write(OverlayTextProvider.Invoke(), clear: true);
        }
    }

    /// <summary>
    /// Empties the text buffer and removes all text from the screen.
    /// </summary>
    public void Clear()
    {
        TextBuffer = new int[Dimensions.X * Dimensions.Y];
        HasContent = false;
        LastUpdate = DateTime.Now;
    }

    /// <summary>
    /// Clears all content and resets all timers/flags (unlike calling Clear alone).
    /// </summary>
    public void Reset()
    {
        Clear();
        ClearOverlay = DateTime.MaxValue;
        UpdateOverlay = DateTime.MaxValue;
        OverlayTextProvider = null;
        PopupStage = 0;
        FadeLevel = 1f;
    }

    /// <summary>
    /// Puts new text into the buffer and the screen.
    /// </summary>
    public void Write(string content, bool clear = false, int starting_row = 0)
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
