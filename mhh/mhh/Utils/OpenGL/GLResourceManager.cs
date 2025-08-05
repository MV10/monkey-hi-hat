
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
    private Dictionary<string, IReadOnlyList<GLResourceGroup>> AllocatedResourceGroups = new();
    private Dictionary<string, IReadOnlyList<GLImageTexture>> AllocatedTextures = new();
    private List<int> AvailableTextureUnits = new(Caching.MaxAvailableTextureUnit);

    public GLResourceManager()
    {
        for(int i = 0; i <= Caching.MaxAvailableTextureUnit; i++)
        {
            AvailableTextureUnits.Add(i);
        }
    }

    /// <summary>
    /// This request for new framebuffers returns a list collection of buffers. An exception
    /// is thrown if buffers are already allocated to the owner.
    /// </summary>
    public IReadOnlyList<GLResourceGroup> CreateResourceGroups(string ownerName, int totalRequired, Vector2 viewportResolution)
        => CreateResourceGroups(ownerName, totalRequired, (int)viewportResolution.X, (int)viewportResolution.Y);

    /// <summary>
    /// This request for new framebuffers returns a list collection of buffers. An exception
    /// is thrown if buffers are already allocated to the owner.
    /// </summary>
    public IReadOnlyList<GLResourceGroup> CreateResourceGroups(string ownerName, int totalRequired, int viewportWidth, int viewportHeight)
    {
        if (AllocatedResourceGroups.ContainsKey(ownerName)) throw new InvalidOperationException($"GL resources already allocated to owner name {ownerName}");
        if (totalRequired < 1) throw new ArgumentOutOfRangeException("GL resource allocation request must be 1 or greater");

        LogHelper.Logger?.LogTrace($"{nameof(GLResourceManager)} creating {totalRequired} resource groups for {ownerName}");

        List<GLResourceGroup> list = new(totalRequired);

        for(int i = 0; i < totalRequired; i++)
        {
            var info = new GLResourceGroup
            {
                OwnerName = ownerName,
                DrawPassIndex = i,
                TextureUnitOrdinal = AssignTextureUnit(),
            };

            LogHelper.Logger?.LogTrace($"...Creating resource group for draw pass index {i}");

            info.FramebufferHandle = GL.GenFramebuffer();
            info.TextureHandle = GL.GenTexture();

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, info.FramebufferHandle);
            GL.ActiveTexture(info.TextureUnit);
            GL.BindTexture(TextureTarget.Texture2D, info.TextureHandle);
            AllocateFramebufferTexture(info.TextureHandle, viewportWidth, viewportHeight);
            ValidateFramebuffer();

            list.Add(info);
        }

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        AllocatedResourceGroups.Add(ownerName, list);
        return list;
    }

    public IReadOnlyList<GLImageTexture> CreateTextureResources(string ownerName, int totalRequired)
    {
        if (AllocatedTextures.ContainsKey(ownerName)) throw new InvalidOperationException($"GL texture resources already allocated to owner name {ownerName}");
        if (totalRequired < 1) throw new ArgumentOutOfRangeException("GL texture resource allocation request must be 1 or greater");

        LogHelper.Logger?.LogTrace($"{nameof(GLResourceManager)} creating {totalRequired} texture resources for {ownerName}");

        List<GLImageTexture> list = new(totalRequired);

        for(int i = 0; i < totalRequired; i++)
        {
            var info = new GLImageTexture
            {
                OwnerName = ownerName,
                TextureHandle = GL.GenTexture(),
                TextureUnitOrdinal = AssignTextureUnit(),
            };

            list.Add(info);
        }

        AllocatedTextures.Add(ownerName, list);
        return list;
    }

    /// <summary>
    /// Cleans up all framebuffers associated with the caller's owner identifier. The caller should
    /// destroy any local copy of the list object that was returned by the create method.
    /// </summary>
    public void DestroyAllResources(string ownerName)
    {
        if (AllocatedResourceGroups.ContainsKey(ownerName))
        {
            LogHelper.Logger?.LogTrace($"{nameof(GLResourceManager)} destroying resource groups for {ownerName}");
            DestroyResourceGroupsInternal(AllocatedResourceGroups[ownerName]);
            AllocatedResourceGroups.Remove(ownerName);
        }

        if (AllocatedTextures.ContainsKey(ownerName))
        {
            LogHelper.Logger?.LogTrace($"{nameof(GLResourceManager)} destroying texture resources for {ownerName}");
            DestroyLoadedTexturesInternal(AllocatedTextures[ownerName]);
            AllocatedTextures.Remove(ownerName);
        }
    }

    /// <summary>
    /// Called by renderers whenever the viewport size has changed. If old viewport
    /// dimensions are provided, this is a signal to copy (scale) the old content, otherwise
    /// the new content is uninitialized (blank).
    /// </summary>
    public void ResizeTextures(string ownerName, Vector2 viewportResolution, bool copyContent = false)
        => ResizeTextures(ownerName, (int)viewportResolution.X, (int)viewportResolution.Y, copyContent);

    /// <summary>
    /// Called by renderers whenever the viewport size has changed. If old viewport
    /// dimensions are provided, this is a signal to copy (scale) the old content, otherwise
    /// the new content is uninitialized (blank).
    /// </summary>
    public void ResizeTextures(string ownerName, int viewportWidth, int viewportHeight, bool copyContent = false)
    {
        if (!AllocatedResourceGroups.ContainsKey(ownerName)) return;

        LogHelper.Logger?.LogTrace($"{nameof(GLResourceManager)} resizing framebuffer textures for {ownerName}");

        foreach (var resources in AllocatedResourceGroups[ownerName])
        {
            ResizeTexture(resources, viewportWidth, viewportHeight, copyContent);
        }
    }

    /// <summary>
    /// Resize a specific framebuffer texture. If old viewport dimensions are provided, this is a 
    /// signal to copy (scale) the old content, otherwise the new content is uninitialized (blank).
    /// </summary>
    public void ResizeTexture(GLResourceGroup resources, int viewportWidth, int viewportHeight, bool copyContent = false)
    {
        LogHelper.Logger?.LogTrace($"...Resizing framebuffer texture for draw pass index {resources.DrawPassIndex} to ({viewportWidth},{viewportHeight})");

        int oldFramebufferHandle = 0;
        int oldTextureHandle = 0;

        // When copying, we store the old FBO and texture handles and the
        // GLResourceGroup ends up with brand new ones. The old ones are used
        // for the copy and are then released.
        if (copyContent)
        {
            oldFramebufferHandle = resources.FramebufferHandle;
            oldTextureHandle = resources.TextureHandle;
            resources.FramebufferHandle = GL.GenFramebuffer();
            resources.TextureHandle = GL.GenTexture();
        }

        // Attach a new texture of a new size to the framebuffer
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, resources.FramebufferHandle);
        GL.ActiveTexture(resources.TextureUnit);
        GL.BindTexture(TextureTarget.Texture2D, resources.TextureHandle);
        AllocateFramebufferTexture(resources.TextureHandle, viewportWidth, viewportHeight);
        ValidateFramebuffer();

        // Do the copy, if requested, then delete the old buffers
        if (copyContent)
        {
            LogHelper.Logger?.LogTrace("...Copying old framebuffer content to new framebuffer");

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, oldFramebufferHandle);
            GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureWidth, out int oldWidth);
            GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureHeight, out int oldHeight);

            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, resources.FramebufferHandle);
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
            //StringBuilder keys = new(AllocatedResourceGroups.Count + AllocatedImageTextures.Count);
            //foreach (var kvp in AllocatedResourceGroups) keys.Append("  RG: ").AppendLine(kvp.Key);
            //foreach (var kvp in AllocatedImageTextures) keys.Append("  TX: ").AppendLine(kvp.Key);
            //throw new InvalidOperationException($"Avalable TextureUnit slots exhausted (from {Caching.MaxAvailableTextureUnit} available)\n  Allocations:\n{keys}");
        }
        var tu = AvailableTextureUnits[0];
        AvailableTextureUnits.RemoveAt(0);
        return tu;
    }

    // assumes caller has activated and bound the texture handle
    private void AllocateFramebufferTexture(int textureHandle, int viewportWidth, int viewportHeight, TextureWrapMode wrapMode = TextureWrapMode.Repeat)
    {
        LogHelper.Logger?.LogTrace($"...Allocating framebuffer texture");

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrapMode);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrapMode);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, viewportWidth, viewportHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, textureHandle, 0);
    }

    // failure instantly crashes the program (yay!)
    private void ValidateFramebuffer()
    {
        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (!status.Equals(FramebufferErrorCode.FramebufferComplete) && !status.Equals(FramebufferErrorCode.FramebufferCompleteExt))
        {
            Console.WriteLine($"Error creating or resizing framebuffer: {status}");
            LogHelper.Logger?.LogError($"Error creating or resizing framebuffer: {status}");
            Thread.Sleep(250);
            Environment.Exit(-1);
        }
    }

    private void DestroyResourceGroupsInternal(IReadOnlyList<GLResourceGroup> list)
    {
        var handles = list.Select(i => i.FramebufferHandle).ToArray();
        LogHelper.Logger?.LogTrace($"   Deleting {handles.Length} framebuffer handles");
        GL.DeleteFramebuffers(handles.Length, handles);

        handles = list.Select(i => i.TextureHandle).ToArray();
        LogHelper.Logger?.LogTrace($"   Deleting {handles.Length} texture handles");
        GL.DeleteTextures(handles.Length, handles);

        handles = list.Select(i => i.TextureUnitOrdinal).ToArray();
        LogHelper.Logger?.LogTrace($"   Releasing {handles.Length} texture units");
        AvailableTextureUnits.AddRange(handles.ToList());
    }

    private void DestroyLoadedTexturesInternal(IReadOnlyList<GLImageTexture> list)
    {
        var videos = list.Where(i => i.VideoData is not null).ToList();
        LogHelper.Logger?.LogTrace($"   Releasing {videos.Count} video file resources");
        foreach (var video in videos)
        {
            video.VideoData.File?.Dispose();
            video.VideoData = null;
        }

        var handles = list.Select(i => i.TextureHandle).ToArray();
        LogHelper.Logger?.LogTrace($"   Deleting {handles.Length} texture handles");
        GL.DeleteTextures(handles.Length, handles);

        handles = list.Select(i => i.TextureUnitOrdinal).ToArray();
        LogHelper.Logger?.LogTrace($"   Releasing {handles.Length} texture units");
        AvailableTextureUnits.AddRange(handles.ToList());
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        LogHelper.Logger?.LogTrace($"{GetType()}.Dispose() ----------------------------");

        foreach (var kvp in AllocatedResourceGroups)
        {
            LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() ResourceGroup owner {kvp.Key}");
            DestroyResourceGroupsInternal(kvp.Value);
        }
        AllocatedResourceGroups.Clear();

        foreach(var kvp in AllocatedTextures)
        {
            LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() ImageTexture owner {kvp.Key}");
            DestroyLoadedTexturesInternal(kvp.Value);
        }
        AllocatedTextures.Clear();

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
