
using FFMediaToolkit.Decoding;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace mhh;

public class VideoMediaData
{
    /// <summary>
    /// File-level data for the video.
    /// </summary>
    public MediaFile? File;

    /// <summary>
    /// Active video stream
    /// </summary>
    public VideoStream? Stream;

    public int Width;

    public int Height;

    public Vector2 Resolution;

    public TimeSpan Duration => Stream?.Info.Duration ?? TimeSpan.Zero;

    public TimeSpan LastUpdateTime = TimeSpan.Zero;

    public TimeSpan LastStreamPosition = TimeSpan.MaxValue;

    /// <summary>
    /// The video feature manages its own clock to control playback timing.
    /// </summary>
    public Stopwatch Clock = new();

    /// <summary>
    /// The clock will auto-start unless the video is paused. Do not update this directly,
    /// instead call VideoRenderingHelper's Pause / Unpause methods.
    /// </summary>
    public bool IsPaused = false;
}
