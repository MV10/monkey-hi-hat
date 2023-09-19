
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
// this manager.
//
// The class also manages TextureUnit assignments, because optimal performance
// is to avoid changing TUs on the fly, and a given renderer won't know what TUs
// may have already been used by a different renderer (for example, the crossfade
// renderer controls two other renderers that aren't aware of each other). The
// assigned TUs won't necessarily be sequential.
//
// Any future TextureUnit requirements should also be managed by this class (for
// example, if support is added for loading external image files).

/// <summary>
/// Do not instantiate this object. Access it via RenderManager's static 
/// ResourceManager property, which is what invokes the static factory.
/// </summary>
public class GLResourceManager : IDisposable
{
    // Can we make it any more obvious?
    internal static GLResourceManager GetInstanceForRenderManager()
    {
        if (Instance is not null) throw new InvalidOperationException("FramebufferManager should be accessed through RenderManager only");
        Instance = new();
        return Instance;
    }
    private static GLResourceManager Instance = null;

    private Dictionary<Guid, IReadOnlyList<GLResourceGroup>> AllocatedResourceGroups = new();
    private Dictionary<Guid, IReadOnlyList<GLImageTexture>> AllocatedImageTextures = new();
    private List<int> AvailableTextureUnits = new(Caching.MaxAvailableTextureUnit);

    private GLResourceManager()
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
    public IReadOnlyList<GLResourceGroup> CreateResourceGroups(Guid ownerName, int totalRequired, Vector2 viewportResolution)
        => CreateResourceGroups(ownerName, totalRequired, (int)viewportResolution.X, (int)viewportResolution.Y);

    /// <summary>
    /// This request for new framebuffers returns a list collection of buffers. An exception
    /// is thrown if buffers are already allocated to the owner.
    /// </summary>
    public IReadOnlyList<GLResourceGroup> CreateResourceGroups(Guid ownerName, int totalRequired, int viewportWidth, int viewportHeight)
    {
        if (AllocatedResourceGroups.ContainsKey(ownerName)) throw new InvalidOperationException($"GL resources already allocated to owner name {ownerName}");
        if (totalRequired < 1) throw new ArgumentOutOfRangeException("GL resource allocation request must be 1 or greater");

        List<GLResourceGroup> list = new(totalRequired);

        for(int i = 0; i < totalRequired; i++)
        {
            var info = new GLResourceGroup
            {
                OwnerName = ownerName,
                DrawbufferIndex = i,
                TextureUnitOrdinal = AssignTextureUnit(),
            };

            info.FramebufferHandle = GL.GenFramebuffer();
            info.TextureHandle = GL.GenTexture();

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, info.FramebufferHandle);
            GL.BindTexture(TextureTarget.Texture2D, info.TextureHandle);
            AllocateFramebufferTexture(info.TextureHandle, viewportWidth, viewportHeight);
            ValidateFramebuffer();

            list.Add(info);
        }

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        AllocatedResourceGroups.Add(ownerName, list);
        return list;
    }

    public IReadOnlyList<GLImageTexture> CreateTextureResources(Guid ownerName, int totalRequired)
    {
        if (AllocatedImageTextures.ContainsKey(ownerName)) throw new InvalidOperationException($"GL texture resources already allocated to owner name {ownerName}");
        if (totalRequired < 1) throw new ArgumentOutOfRangeException("GL texture resource allocation request must be 1 or greater");

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

        AllocatedImageTextures.Add(ownerName, list);
        return list;
    }

    /// <summary>
    /// Cleans up all framebuffers associated with the caller's owner identifier. The caller should
    /// destroy any local copy of the list object that was returned by the create method.
    /// </summary>
    public void DestroyAllResources(Guid ownerName)
    {
        if (AllocatedResourceGroups.ContainsKey(ownerName))
        {
            DestroyResourceGroupsInternal(AllocatedResourceGroups[ownerName]);
            AllocatedResourceGroups.Remove(ownerName);
        }

        if (AllocatedImageTextures.ContainsKey(ownerName))
        {
            DestroyImageTexturesInternal(AllocatedImageTextures[ownerName]);
            AllocatedImageTextures.Remove(ownerName);
        }
    }

    /// <summary>
    /// Called by renderers whenever the viewport size has changed. If old viewport
    /// dimensions are provided, this is a signal to copy (scale) the old content, otherwise
    /// the new content is uninitialized (blank).
    /// </summary>
    public void ResizeTextures(Guid ownerName, Vector2 viewportResolution, Vector2 oldResolution = default)
        => ResizeTextures(ownerName, (int)viewportResolution.X, (int)viewportResolution.Y, (int)oldResolution.X, (int)oldResolution.Y);

    /// <summary>
    /// Called by renderers whenever the viewport size has changed. If old viewport
    /// dimensions are provided, this is a signal to copy (scale) the old content, otherwise
    /// the new content is uninitialized (blank).
    /// </summary>
    public void ResizeTextures(Guid ownerName, int viewportWidth, int viewportHeight, int oldWidth = 0, int oldHeight = 0)
    {
        if (!AllocatedResourceGroups.ContainsKey(ownerName)) return;
        var copyContent = oldWidth > 0 && oldHeight > 0;
        int oldFramebufferHandle = 0;
        int oldTextureHandle = 0;

        foreach (var info in AllocatedResourceGroups[ownerName])
        {
            // When copying, we store the old FBO and texture handles and the
            // GLResourceGroup ends up with brand new ones. The old ones are used
            // for the copy and are then released.
            if(copyContent)
            {
                oldFramebufferHandle = info.FramebufferHandle;
                oldTextureHandle = info.TextureHandle;
                info.FramebufferHandle = GL.GenFramebuffer();
                info.TextureHandle = GL.GenTexture();
            }

            // Attach a new texture of a new size to the framebuffer
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, info.FramebufferHandle);
            GL.BindTexture(TextureTarget.Texture2D, info.TextureHandle);
            AllocateFramebufferTexture(info.TextureHandle, viewportWidth, viewportHeight);
            ValidateFramebuffer();

            // Do the copy, if requested, then delete the old buffers
            if(copyContent)
            {
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, oldFramebufferHandle);
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, info.FramebufferHandle);
                GL.BlitFramebuffer(
                    0, 0, oldWidth, oldHeight,
                    0, 0, viewportWidth, viewportHeight,
                    ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                GL.DeleteTexture(oldTextureHandle);
                GL.DeleteFramebuffer(oldFramebufferHandle);
            }
        }

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    // assumes caller has bound the texture handle
    private void AllocateFramebufferTexture(int textureHandle, int viewportWidth, int viewportHeight, TextureWrapMode wrapMode = TextureWrapMode.Repeat)
    {
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrapMode);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrapMode);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, viewportWidth, viewportHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, textureHandle, 0);
    }

    private int AssignTextureUnit()
    {
        if(AvailableTextureUnits.Count == 0) throw new InvalidOperationException($"Avalable TextureUnit slots exhausted (from {Caching.MaxAvailableTextureUnit} available)");
        var tu = AvailableTextureUnits[0];
        AvailableTextureUnits.RemoveAt(0);
        return tu;
    }

    // failure instantly crashes the program (yay!)
    private void ValidateFramebuffer()
    {
        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (!status.Equals(FramebufferErrorCode.FramebufferComplete) && !status.Equals(FramebufferErrorCode.FramebufferCompleteExt))
        {
            Console.WriteLine($"Error creating framebuffer: {status}");
            Thread.Sleep(250);
            Environment.Exit(-1);
        }
    }

    private void DestroyResourceGroupsInternal(IReadOnlyList<GLResourceGroup> list)
    {
        int[] handles = list.Select(i => i.FramebufferHandle).ToArray();
        GL.DeleteFramebuffers(handles.Length, handles);

        handles = list.Select(i => i.TextureHandle).ToArray();
        GL.DeleteTextures(handles.Length, handles);

        AvailableTextureUnits.AddRange(list.Select(i => i.TextureUnitOrdinal).ToList());
    }

    private void DestroyImageTexturesInternal(IReadOnlyList<GLImageTexture> list)
    {
        int[] handles = list.Select(i => i.TextureHandle).ToArray();
        GL.DeleteTextures(handles.Length, handles);

        AvailableTextureUnits.AddRange(list.Select(i => i.TextureUnitOrdinal).ToList());
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

        foreach(var kvp in AllocatedImageTextures)
        {
            LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() ImageTexture owner {kvp.Key}");
            DestroyImageTexturesInternal(kvp.Value);
        }
        AllocatedImageTextures.Clear();

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
