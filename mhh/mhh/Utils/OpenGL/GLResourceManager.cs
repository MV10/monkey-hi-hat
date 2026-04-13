
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace mhh;

// Manages creation, assignment, tracking, and cleanup of OpenGL resources such
// as framebuffers, textures, and TextureUnits.
//
// Framebuffers (FBOs) are used during multi-pass rendering, including multi-pass
// visualizers, cross-fade operations, and various post-processing effects. Since
// the total number of required FBOs may involve multiple simultaneous usages, any
// FBO reference (such as the numbers in the [multipass] section of a visualizer
// config file) are "virtual" and will be mapped to a physical FBO index number by
// this manager. A texture must be attached to the FBO as the render target.
//
// The class also manages TextureUnit assignments, because optimal performance
// is to avoid changing TUs on the fly, and a given renderer won't know what TUs
// may have already been used by a different renderer (for example, the crossfade
// renderer controls two other renderers that aren't aware of each other). The
// assigned TUs won't necessarily be sequential.
//
// Any future TextureUnit requirements should also be managed by this class (for
// example, when support was added for loading external image and video files).

/// <summary>
/// Do not instantiate this object. Access it via RenderManager's static ResourceManager property.
/// </summary>
public class GLResourceManager : IDisposable
{
    private Dictionary<string, IReadOnlyList<GLFBOTexture>> AllocatedFBOTextures = new();
    private Dictionary<string, IReadOnlyList<GLImageTexture>> AllocatedImageTextures = new();
    private List<int> AvailableTextureUnits = new(Caching.MaxAvailableTextureUnit);

    private static readonly ILogger Logger = LogHelper.CreateLogger(nameof(GLResourceManager));

    public GLResourceManager()
    {
        Logger?.LogTrace("Constructor");

        for (int i = 0; i <= Caching.MaxAvailableTextureUnit; i++)
        {
            AvailableTextureUnits.Add(i);
        }
    }

    /// <summary>
    /// This request for new framebuffers returns a list collection of buffers. An exception
    /// is thrown if buffers are already allocated to the owner.
    /// </summary>
    public IReadOnlyList<GLFBOTexture> CreateFBOTextures(string ownerName, int totalRequired, Vector2 viewportResolution)
        => CreateFBOTextures(ownerName, totalRequired, (int)viewportResolution.X, (int)viewportResolution.Y);

    /// <summary>
    /// This request for new framebuffers returns a list collection of buffers. An exception
    /// is thrown if buffers are already allocated to the owner.
    /// </summary>
    public IReadOnlyList<GLFBOTexture> CreateFBOTextures(string ownerName, int totalRequired, int viewportWidth, int viewportHeight)
    {
        Logger?.LogTrace($"{nameof(CreateFBOTextures)}: Creating {totalRequired} FBOTextures for {ownerName}");

        if (AllocatedFBOTextures.ContainsKey(ownerName)) throw new InvalidOperationException($"GL FBOTextures already allocated to owner name {ownerName}");
        if (totalRequired < 1) throw new ArgumentOutOfRangeException("GL FBOTexture allocation request must be 1 or greater");

        List<GLFBOTexture> list = new(totalRequired);

        for(int i = 0; i < totalRequired; i++)
        {
            var info = new GLFBOTexture
            {
                OwnerName = ownerName,
                DrawPassIndex = i,
                TextureUnitOrdinal = AssignTextureUnit(),
            };

            info.FramebufferHandle = GL.GenFramebuffer();
            info.TextureHandle = GL.GenTexture();

            Logger?.LogTrace($"...(Draw pass index {i}) TextureUnit:{info.TextureUnitOrdinal}, TextureHandle:{info.TextureHandle}, FramebufferHandle:{info.FramebufferHandle}");

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, info.FramebufferHandle);
            GL.ActiveTexture(info.TextureUnit);
            GL.BindTexture(TextureTarget.Texture2D, info.TextureHandle);
            AttachBlankFramebufferTexture(info.TextureHandle, viewportWidth, viewportHeight);
            ValidateFramebuffer(nameof(CreateFBOTextures));

            list.Add(info);
        }

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        AllocatedFBOTextures.Add(ownerName, list);
        return list;
    }

    /// <summary>
    /// This request for new textures returns a list collection of texture objects with
    /// handle and TextureUnit assignments. The actual texture buffer is not allocated here.
    /// The assumption is that these textures are for content (ie. not for framebuffers).
    /// </summary>
    public IReadOnlyList<GLImageTexture> CreateImageTextures(string ownerName, int totalRequired)
    {
        Logger?.LogTrace($"{nameof(CreateImageTextures)}: Creating {totalRequired} ImageTextures for {ownerName}");

        if (AllocatedImageTextures.ContainsKey(ownerName)) throw new InvalidOperationException($"GL ImageTextures already allocated to owner name {ownerName}");
        if (totalRequired < 1) throw new ArgumentOutOfRangeException("GL ImageTexture allocation request must be 1 or greater");

        List<GLImageTexture> list = new(totalRequired);

        for(int i = 0; i < totalRequired; i++)
        {
            var info = new GLImageTexture
            {
                OwnerName = ownerName,
                TextureHandle = GL.GenTexture(),
                TextureUnitOrdinal = AssignTextureUnit(),
            };

            Logger?.LogTrace($"...(Texture index {i}) TextureUnit:{info.TextureUnitOrdinal}, TextureHandle:{info.TextureHandle}");

            list.Add(info);
        }

        AllocatedImageTextures.Add(ownerName, list);
        return list;
    }

    /// <summary>
    /// Cleans up all framebuffers associated with the caller's owner identifier. The caller should
    /// destroy any local copy of the list object that was returned by the create method.
    /// </summary>
    public void DestroyAllResources(string ownerName, bool keepImageTextures = false)
    {
        Logger?.LogTrace($"{nameof(DestroyAllResources)}: Destroying all resources for {ownerName}");

        if (AllocatedFBOTextures.ContainsKey(ownerName))
        {
            Logger?.LogTrace($"...Destroying FBOTextures for {ownerName}");
            DestroyFBOTexturesInternal(AllocatedFBOTextures[ownerName]);
            AllocatedFBOTextures.Remove(ownerName);
        }

        if (!keepImageTextures && AllocatedImageTextures.ContainsKey(ownerName))
        {
            Logger?.LogTrace($"...Destroying ImageTextures for {ownerName}");
            DestroyImageTexturesInternal(AllocatedImageTextures[ownerName]);
            AllocatedImageTextures.Remove(ownerName);
        }
    }

    /// <summary>
    /// Called by renderers whenever the viewport size has changed. If old viewport
    /// dimensions are provided, this is a signal to copy (scale) the old content, otherwise
    /// the new content is uninitialized (blank).
    /// </summary>
    public void ResizeFramebufferTextures(string ownerName, Vector2 viewportResolution, bool copyContent = false)
        => ResizeFramebufferTextures(ownerName, (int)viewportResolution.X, (int)viewportResolution.Y, copyContent);

    /// <summary>
    /// Called by renderers whenever the viewport size has changed. If old viewport
    /// dimensions are provided, this is a signal to copy (scale) the old content, otherwise
    /// the new content is uninitialized (blank).
    /// </summary>
    public void ResizeFramebufferTextures(string ownerName, int viewportWidth, int viewportHeight, bool copyContent = false)
    {
        if (!AllocatedFBOTextures.ContainsKey(ownerName)) return;

        Logger?.LogTrace($"Resizing framebuffer viewport textures for {ownerName}");

        foreach (var fbo in AllocatedFBOTextures[ownerName])
        {
            ResizeFramebufferTexture(fbo, viewportWidth, viewportHeight, copyContent);
        }
    }

    /// <summary>
    /// Resize a specific framebuffer texture. If old viewport dimensions are provided, this is a 
    /// signal to copy (scale) the old content, otherwise the new content is uninitialized (blank).
    /// </summary>
    public void ResizeFramebufferTexture(GLFBOTexture fbotex, int viewportWidth, int viewportHeight, bool copyContent = false)
    {
        Logger?.LogTrace($"...Resizing framebuffer texture for draw pass index {fbotex.DrawPassIndex} to ({viewportWidth},{viewportHeight})");

        int oldFramebufferHandle = 0;
        int oldTextureHandle = 0;

        // When copying, we store the old FBO and texture handles and the
        // GLFBOTexture ends up with brand new ones. The old ones are used
        // for the copy and are then released.
        if (copyContent)
        {
            oldFramebufferHandle = fbotex.FramebufferHandle;
            oldTextureHandle = fbotex.TextureHandle;
            fbotex.FramebufferHandle = GL.GenFramebuffer();
            fbotex.TextureHandle = GL.GenTexture();
        }

        // Attach a new texture of a new size to the framebuffer
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbotex.FramebufferHandle);
        GL.ActiveTexture(fbotex.TextureUnit);
        GL.BindTexture(TextureTarget.Texture2D, fbotex.TextureHandle);
        AttachBlankFramebufferTexture(fbotex.TextureHandle, viewportWidth, viewportHeight);
        ValidateFramebuffer(nameof(ResizeFramebufferTexture));

        // Do the copy, if requested, then delete the old buffers
        if (copyContent)
        {
            Logger?.LogTrace("...Copying old framebuffer content to new framebuffer");

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, oldFramebufferHandle);
            GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureWidth, out int oldWidth);
            GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureHeight, out int oldHeight);

            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbotex.FramebufferHandle);
            GL.BlitFramebuffer(
                0, 0, oldWidth, oldHeight,
                0, 0, viewportWidth, viewportHeight,
                ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DeleteTexture(oldTextureHandle);
            GL.DeleteFramebuffer(oldFramebufferHandle);
        }

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private int AssignTextureUnit()
    {
        if (AvailableTextureUnits.Count == 0)
        {
            throw new InvalidOperationException($"Avalable TextureUnit slots exhausted (from {Caching.MaxAvailableTextureUnit} available)");
            //StringBuilder keys = new(AllocatedFBOTextures.Count + AllocatedImageTextures.Count);
            //foreach (var kvp in AllocatedFBOTextures) keys.Append("  RG: ").AppendLine(kvp.Key);
            //foreach (var kvp in AllocatedImageTextures) keys.Append("  TX: ").AppendLine(kvp.Key);
            //throw new InvalidOperationException($"Avalable TextureUnit slots exhausted (from {Caching.MaxAvailableTextureUnit} available)\n  Allocations:\n{keys}");
        }
        var tu = AvailableTextureUnits[0];
        AvailableTextureUnits.RemoveAt(0);
        return tu;
    }

    // assumes caller has activated and bound the texture handle
    private void AttachBlankFramebufferTexture(int textureHandle, int viewportWidth, int viewportHeight, TextureWrapMode wrapMode = TextureWrapMode.Repeat)
    {
        Logger?.LogTrace($"...{nameof(AttachBlankFramebufferTexture)} textureHandle: {textureHandle}");

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrapMode);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrapMode);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, viewportWidth, viewportHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, textureHandle, 0);
    }

    // failure instantly crashes the program (yay!)
    private void ValidateFramebuffer(string forMethodName)
    {
        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (!status.Equals(FramebufferErrorCode.FramebufferComplete) && !status.Equals(FramebufferErrorCode.FramebufferCompleteExt))
        {
            Logger?.LogCritical($"{forMethodName} error creating or resizing framebuffer: {status}");
            Console.WriteLine($"{forMethodName} error creating or resizing framebuffer: {status}");
            Thread.Sleep(250);
            Environment.Exit(-1);
        }
    }

    private void DestroyFBOTexturesInternal(IReadOnlyList<GLFBOTexture> list)
    {
        var IDs = list.Select(i => i.FramebufferHandle).ToArray();
        Logger?.LogTrace($"...Deleting {IDs.Length} framebuffer handles");
#if DEBUG
        Logger?.LogTrace($"....Handles: {string.Join(",", IDs)}");
#endif
        GL.DeleteFramebuffers(IDs.Length, IDs);

        IDs = list.Select(i => i.TextureHandle).ToArray();
        Logger?.LogTrace($"...Deleting {IDs.Length} texture handles");
#if DEBUG
        Logger?.LogTrace($"....Handles: {string.Join(",", IDs)}");
#endif
        GL.DeleteTextures(IDs.Length, IDs);

        IDs = list.Select(i => i.TextureUnitOrdinal).ToArray();
        Logger?.LogTrace($"...Releasing {IDs.Length} texture units");
#if DEBUG
        Logger?.LogTrace($"....Units: {string.Join(",", IDs)}");
#endif
        AvailableTextureUnits.AddRange(IDs.ToList());
    }

    private void DestroyImageTexturesInternal(IReadOnlyList<GLImageTexture> list)
    {
        var tex = list.Where(i => i.VideoData is not null).ToList();
        Logger?.LogTrace($"...Releasing {tex.Count} video file resources");
        foreach (var video in tex)
        {
            video.VideoData.File?.Dispose();
            video.VideoData = null;
        }

        var IDs = list.Select(i => i.TextureHandle).ToArray();
        Logger?.LogTrace($"...Deleting {IDs.Length} texture handles");
#if DEBUG
        Logger?.LogTrace($"....Handles: {string.Join(",", IDs)}");
#endif
        GL.DeleteTextures(IDs.Length, IDs);

        IDs = list.Select(i => i.TextureUnitOrdinal).ToArray();
        Logger?.LogTrace($"...Releasing {IDs.Length} texture units");
#if DEBUG
        Logger?.LogTrace($"....Units: {string.Join(",", IDs)}");
#endif
        AvailableTextureUnits.AddRange(IDs.ToList());
        
        foreach (var t in list) t.TextureHandle = -1; 
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        Logger?.LogTrace("Disposing");

        foreach (var kvp in AllocatedFBOTextures)
        {
            Logger?.LogTrace($"Disposing FBOTexture owner {kvp.Key}");
            DestroyFBOTexturesInternal(kvp.Value);
        }
        AllocatedFBOTextures.Clear();

        foreach(var kvp in AllocatedImageTextures)
        {
            Logger?.LogTrace($"Disposing ImageTexture owner {kvp.Key}");
            DestroyImageTexturesInternal(kvp.Value);
        }
        AllocatedImageTextures.Clear();

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
