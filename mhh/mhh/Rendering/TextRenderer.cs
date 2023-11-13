
using eyecandy;
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace mhh;

// This always renders last, even after Crossfade. Thus, it grabs the
// default GL backbuffer as the source_image and mixes the text over
// that content, and the mix becomes the default GL backbuffer. The
// RenderFrame and OnResize calls are issued by RenderManager via the
// instance owned by TextManager.

public class TextRenderer : IRenderer
{
    public bool IsValid { get; set; } = true;
    public string InvalidReason { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;

    // IRenderer requirements which are not applicable to this renderer.
    public GLResourceGroup OutputBuffers { get => null; }
    public Vector2 Resolution { get => RenderingHelper.ClientSize; }
    public bool OutputIntercepted { set { } }
    public ConfigFile ConfigSource { get => null; }

    private string OwnerName = RenderingHelper.MakeOwnerName("TextRenderer");
    private GLImageTexture FontTexture;
    private GLResourceGroup BaseImage = null;
    private GLResourceGroup TextData = null;

    private DateTime LastUpdateCopied;
    private IVertexSource VertQuad;
    private Shader TextShader;

    public TextRenderer()
    {
        TextShader = Caching.TextShader;

        FontTexture = RenderManager.ResourceManager.CreateTextureResources(OwnerName, 1)[0];
        FontTexture.Filename = "font.png";
        FontTexture.UniformName = "font";
        FontTexture.ImageLoaded = RenderingHelper.LoadImageFile(FontTexture, TextureWrapMode.ClampToEdge, ApplicationConfiguration.InternalShaderPath);

        VertQuad = new VertexQuad();
        VertQuad.Initialize(null, TextShader); // fragquad doesn't have settings, so null is safe

        OnResize();
    }

    public void RenderFrame(ScreenshotWriter screenshotWriter = null)
    {
        // TODO - fade modes
        float fade_level = 1f;

        if (!RenderManager.TextManager.HasContent) return;
        CopyTextBufferToTexture();

        // Copy the GL backbuffer as our base_image uniform
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, BaseImage.FramebufferHandle);
        GL.BlitFramebuffer(
            0, 0, RenderingHelper.ClientSize.X, RenderingHelper.ClientSize.Y,
            0, 0, RenderingHelper.ClientSize.X, RenderingHelper.ClientSize.Y,
            ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);

        // Draw to the GL backbuffer
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.Viewport(0, 0, RenderingHelper.ClientSize.X, RenderingHelper.ClientSize.Y);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        TextShader.Use();
        TextShader.SetUniform("resolution", Resolution);
        TextShader.SetTexture("base_image", BaseImage.TextureHandle, BaseImage.TextureUnit);
        TextShader.SetTexture("text", TextData.TextureHandle, TextData.TextureUnit);
        TextShader.SetTexture("font", FontTexture.TextureHandle, FontTexture.TextureUnit);
        TextShader.SetUniform("dimensions", RenderManager.TextManager.Dimensions);
        TextShader.SetUniform("start_position", RenderManager.TextManager.StartPosition);
        TextShader.SetUniform("char_size", RenderManager.TextManager.CharSize);
        TextShader.SetUniform("fade_level", fade_level);

        VertQuad.RenderFrame(TextShader);

        // Now AppWindow's OnRenderFrame swaps the backbuffer to the display
    }

    public void OnResize()
    {
        if(BaseImage is null)
        {
            var rg = RenderManager.ResourceManager.CreateResourceGroups(OwnerName, 2, Resolution);
            BaseImage = rg[0];
            TextData = rg[1];
        }
        else
        {
            RenderManager.ResourceManager.ResizeTextures(OwnerName, Resolution);
        }

        VertQuad.BindBuffers(TextShader);

        LastUpdateCopied = DateTime.MinValue;
    }

    public void StartClock()
    { }

    public void StopClock()
    { }

    public float TrueElapsedTime()
        => 0f;

    public Dictionary<string, float> GetFXUniforms(string fxFilename)
        => new();

    private void CopyTextBufferToTexture()
    {
        if (LastUpdateCopied > RenderManager.TextManager.LastUpdate) return;

        lock(AudioTextureEngine.GLTextureLock)
        {
            GL.ActiveTexture(TextData.TextureUnit);
            GL.BindTexture(TextureTarget.Texture2D, TextData.TextureHandle);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R32i,
                RenderManager.TextManager.Dimensions.X, RenderManager.TextManager.Dimensions.Y, 0,
                PixelFormat.RedInteger, PixelType.Int, RenderManager.TextManager.TextBuffer);
        }

        LastUpdateCopied = DateTime.Now;
    }

    public void Dispose()
    {
        if (IsDisposed) return;

        LogHelper.Logger?.LogTrace($"{GetType()}.Dispose() ----------------------------");

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() IVertexSource");
        VertQuad?.Dispose();

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() Resources");
        RenderManager.ResourceManager.DestroyAllResources(OwnerName);

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
