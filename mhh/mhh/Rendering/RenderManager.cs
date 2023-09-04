
using OpenTK.Graphics.OpenGL;

namespace mhh;

// viz confs
// viz objs
// viz shaders & cache management
// fx shaders
// lib shaders
// framebuffers & texture units
// uniforms
// eyecandy audio texture management

public class RenderManager : IDisposable
{
    public IRenderer ActiveRenderer { get; private set; }
    public IRenderer NewRenderer { get; private set; }

    private List<(int hBuffer, int hTexture)> Framebuffers;

    public IRenderer PrepareNewRenderer(VisualizerConfig visualizerConfig)
    {
        IRenderer renderer;
        if (visualizerConfig.ConfigSource.Content.ContainsKey("multipass"))
        {
            renderer = new MultipassRenderer(visualizerConfig);
        }
        else
        {
            renderer = new SingleVisualizerRenderer(visualizerConfig);
        }

        if (!renderer.IsValid) return renderer;

        if (ActiveRenderer is null)
        {
            ActiveRenderer = renderer;
        }
        else
        {
            if (NewRenderer is not null) NewRenderer.Dispose();
            NewRenderer = renderer;
        }

        // TODO recalc framebuffer requirements here?

        return renderer;
    }

    public void RenderFrame()
    {
        if(NewRenderer is not null)
        {
            ActiveRenderer.Dispose();
            ActiveRenderer = NewRenderer;
            NewRenderer = null;
        }

        ActiveRenderer?.RenderFrame();
    }

    public void ViewportResized(int viewportWidth, int viewportHeight)
    {
        //GenerateFramebuffers(viewportWidth, viewportHeight);
    }

    public string GetInfo()
    {
        return "TODO";
    }

    private void GenerateFramebuffers(int viewportWidth, int viewportHeight)
    {
        if (Framebuffers is null) return;
        Framebuffers.Clear(); // TODO Framebuffers need to be deleted?

        for (int i = 0; i < Framebuffers.Capacity; i++)
        {
            var framebufferHandle = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebufferHandle);

            var textureHandle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, textureHandle);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, viewportWidth, viewportHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, textureHandle, 0);

            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (!status.Equals(FramebufferErrorCode.FramebufferComplete) && !status.Equals(FramebufferErrorCode.FramebufferCompleteExt))
            {
                Console.WriteLine($"Error creating framebuffer {i}: {status}");
                Thread.Sleep(250);
                Environment.Exit(-1);
            }

            Framebuffers.Add((framebufferHandle, textureHandle));
        }

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private bool IsDisposed = false;
    public void Dispose()
    {
        if (IsDisposed) return;

        NewRenderer?.Dispose();
        NewRenderer = null;

        ActiveRenderer?.Dispose();
        ActiveRenderer = null;

        // TODO delete framebuffers and textures

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}
