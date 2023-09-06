
using OpenTK.Graphics.OpenGL;

namespace mhh.Utils;

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

    private Dictionary<Guid, IReadOnlyList<GLResources>> AllocatedResources = new();

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
    public IReadOnlyList<GLResources> CreateResources(Guid ownerName, int totalRequired)
    {
        if (AllocatedResources.ContainsKey(ownerName)) throw new InvalidOperationException($"GL resources already allocated to owner name {ownerName}");
        if (totalRequired < 1) throw new ArgumentOutOfRangeException("GL resource allocation request must be 1 or greater");

        List<GLResources> list = new(totalRequired);

        var viewportWidth = Program.AppWindow.ClientSize.X;
        var viewportHeight = Program.AppWindow.ClientSize.Y;

        for(int i = 0; i < totalRequired; i++)
        {
            var info = new GLResources
            {
                OwnerName = ownerName,
                Index = i,
                TextureUnitOrdinal = AssignTextureUnit(),
            };

            info.BufferHandle = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, info.BufferHandle);

            info.TextureHandle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, info.TextureHandle);

            AllocateFramebufferTexture(info.TextureHandle, viewportWidth, viewportHeight);

            ValidateFramebuffer();

            list.Add(info);
        }

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        AllocatedResources.Add(ownerName, list);
        return list;
    }

    /// <summary>
    /// Cleans up all framebuffers associated with the caller's owner identifier. The caller should
    /// destroy any local copy of the list object that was returned by the create method.
    /// </summary>
    public void DestroyResources(Guid ownerName)
    {
        if (!AllocatedResources.ContainsKey(ownerName)) return;
        DestroyResources(AllocatedResources[ownerName]);
        AllocatedResources.Remove(ownerName);
    }

    /// <summary>
    /// Called by RenderManager whenever the display area size has changed.
    /// </summary>
    public void ResizeTextures(int viewportWidth, int viewportHeight)
    {
        if (AllocatedResources.Count == 0) return;

        foreach(var kvp in AllocatedResources)
        {
            foreach(var info in kvp.Value)
            {
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, info.BufferHandle);
                GL.BindTexture(TextureTarget.Texture2D, info.TextureHandle);
                AllocateFramebufferTexture(info.TextureHandle, viewportWidth, viewportHeight);
                ValidateFramebuffer();
            }
        }

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    // assumes caller has bound the texture handle
    private void AllocateFramebufferTexture(int textureHandle, int viewportWidth, int viewportHeight)
    {
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
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

    private void DestroyResources(IReadOnlyList<GLResources> list)
    {
        int[] handles = list.Select(i => i.BufferHandle).ToArray();
        GL.DeleteFramebuffers(handles.Length, handles);

        handles = list.Select(i => i.TextureHandle).ToArray();
        GL.DeleteTextures(handles.Length, handles);

        AvailableTextureUnits.AddRange(list.Select(i => i.TextureUnitOrdinal).ToList());
    }

    public void Dispose()
    {
        if (IsDisposed) return;

        if(AllocatedResources?.Count > 0)
        {
            foreach (var kvp in AllocatedResources)
            {
                DestroyResources(kvp.Value);
            }
            AllocatedResources.Clear();
        }

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
