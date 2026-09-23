using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Text.Json;
using ImGuiNET;
using CAudioVisualizer.Visualizers;

namespace CAudioVisualizer.Core;

public class PostProcessingConfig
{
    public bool EnableBloom { get; set; } = false;
    public float BloomThreshold { get; set; } = 0.5f;
    public float BloomIntensity { get; set; } = 1.0f;
    public float BloomRadius { get; set; } = 4.0f;

    // Bloom audio reactivity
    public bool BloomAudioReactive { get; set; } = false;
    public float BloomAudioStrength { get; set; } = 0.02f;
    public float BloomAudioDecay { get; set; } = 2.0f;

    public bool EnableChromaticAberration { get; set; } = false;
    public float ChromaticStrength { get; set; } = 0.005f;

    // Chromatic aberration audio reactivity
    public bool ChromaticAudioReactive { get; set; } = false;
    public float ChromaticAudioStrength { get; set; } = 0.02f;
    public float ChromaticAudioDecay { get; set; } = 2.0f;

    public bool EnableVignette { get; set; } = false;
    public float VignetteStrength { get; set; } = 0.8f;
    public float VignetteSize { get; set; } = 0.5f;

    public bool EnableColorGrading { get; set; } = false;
    public float Contrast { get; set; } = 1.0f;
    public float Brightness { get; set; } = 0.0f;
    public float Saturation { get; set; } = 1.0f;
    public Vector3 ColorTint { get; set; } = new Vector3(1.0f, 1.0f, 1.0f);
    public bool UseTimeColorTint { get; set; } = false;
    public bool UseRealTimeColorTint { get; set; } = false;
    public bool InvertColorTint { get; set; } = false;

    public bool EnableFilmGrain { get; set; } = false;
    public float GrainStrength { get; set; } = 0.2f;
}

public class PostProcessingRenderer : IConfigurable
{
    private PostProcessingConfig _config = new();
    private bool _initialized = false;
    private VisualizerManager? _visualizerManager;

    private int _mainFramebuffer, _mainColorTexture;
    private int _bloomFramebuffer, _bloomColorTexture;
    private int _tempFramebuffer, _tempColorTexture;

    private int _copyShaderProgram;
    private int _postProcessShaderProgram;
    private int _bloomExtractProgram;
    private int _gaussianBlurProgram;

    private int _quadVAO, _quadVBO;

    // Audio-reactive beat state
    private float _bloomAudioEnvelope = 0.0f;
    private float _chromaticAudioEnvelope = 0.0f;
    private float _maxBassLevel = 0.0f;

    private Vector2i CurrentWindowSize => _visualizerManager?.GetCurrentWindowSize() ?? new Vector2i(800, 600);

    public void SetVisualizerManager(VisualizerManager manager)
    {
        _visualizerManager = manager;
    }

    public void Initialize()
    {
        if (_initialized) return;

        SetupQuad();
        SetupShaders();
        SetupFramebuffers();
        _initialized = true;
    }

    private void SetupQuad()
    {
        // Fullscreen quad vertices
        float[] vertices = {
            -1.0f, -1.0f, 0.0f, 0.0f, // Bottom left
             1.0f, -1.0f, 1.0f, 0.0f, // Bottom right
             1.0f,  1.0f, 1.0f, 1.0f, // Top right
            -1.0f,  1.0f, 0.0f, 1.0f  // Top left
        };

        uint[] indices = { 0, 1, 2, 2, 3, 0 };

        _quadVAO = GL.GenVertexArray();
        _quadVBO = GL.GenBuffer();
        int ebo = GL.GenBuffer();

        GL.BindVertexArray(_quadVAO);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _quadVBO);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

        // Position attribute
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

        // Texture coordinate attribute
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        GL.BindVertexArray(0);
    }

    private void SetupShaders()
    {
        string copyVertexShader = @"
            #version 330 core
            layout(location = 0) in vec2 aPosition;
            layout(location = 1) in vec2 aTexCoord;

            out vec2 TexCoord;

            void main()
            {
                gl_Position = vec4(aPosition, 0.0, 1.0);
                TexCoord = aTexCoord;
            }";

        string copyFragmentShader = @"
            #version 330 core
            in vec2 TexCoord;
            out vec4 FragColor;

            uniform sampler2D screenTexture;

            void main()
            {
                FragColor = texture(screenTexture, TexCoord);
            }";

        // Main post-processing shader with all effects
        string postProcessFragmentShader = @"
            #version 330 core
            precision highp float;

            in vec2 TexCoord;
            out vec4 FragColor;

            uniform sampler2D screenTexture;
            uniform sampler2D bloomTexture;
            uniform vec2 resolution;
            uniform float time;

            // Effect toggles
            uniform bool enableBloom;
            uniform bool enableChromaticAberration;
            uniform bool enableVignette;
            uniform bool enableColorGrading;
            uniform bool enableFilmGrain;

            // Effect parameters
            uniform float bloomIntensity;
            uniform float chromaticStrength;
            uniform float vignetteStrength;
            uniform float vignetteSize;
            uniform float contrast;
            uniform float brightness;
            uniform float saturation;
            uniform vec3 colorTint;
            uniform float grainStrength;

            // Noise function for film grain
            float noise(vec2 coord) {
                return fract(sin(dot(coord + time * 0.1, vec2(12.9898, 78.233))) * 43758.5453);
            }

            void main()
            {
                vec2 uv = TexCoord;

                // Chromatic aberration
                vec3 color;
                if (enableChromaticAberration) {
                    float aberration = chromaticStrength;
                    color.r = texture(screenTexture, uv + vec2(aberration, 0.0)).r;
                    color.g = texture(screenTexture, uv).g;
                    color.b = texture(screenTexture, uv - vec2(aberration, 0.0)).b;
                } else {
                    color = texture(screenTexture, uv).rgb;
                }

                // Bloom (per-channel shifted to follow the chromatic aberration split)
                if (enableBloom) {
                    vec3 bloom;
                    if (enableChromaticAberration) {
                        float aberration = chromaticStrength;
                        bloom.r = texture(bloomTexture, TexCoord + vec2(aberration, 0.0)).r;
                        bloom.g = texture(bloomTexture, TexCoord).g;
                        bloom.b = texture(bloomTexture, TexCoord - vec2(aberration, 0.0)).b;
                    } else {
                        bloom = texture(bloomTexture, TexCoord).rgb;
                    }
                    color += bloom * bloomIntensity;
                }

                // Color grading
                if (enableColorGrading) {
                    // Brightness
                    color += brightness;

                    // Contrast
                    color = (color - 0.5) * contrast + 0.5;

                    // Saturation
                    float luminance = dot(color, vec3(0.299, 0.587, 0.114));
                    color = mix(vec3(luminance), color, saturation);

                    // Color tint
                    color *= colorTint;
                }

                // Vignette
                if (enableVignette) {
                    vec2 center = vec2(0.5);
                    float dist = distance(uv, center);
                    float vignette = 1.0 - smoothstep(vignetteSize, 1.0, dist);
                    vignette = mix(1.0 - vignetteStrength, 1.0, vignette);
                    color *= vignette;
                }

                // Film grain
                if (enableFilmGrain) {
                    float grain = noise(uv * resolution);
                    color += (grain - 0.5) * grainStrength;
                }

                FragColor = vec4(color, 1.0);
            }";

        string bloomExtractFragment = @"
            #version 330 core
            in vec2 TexCoord;
            out vec4 FragColor;

            uniform sampler2D screenTexture;
            uniform float threshold;

            void main()
            {
                vec3 color = texture(screenTexture, TexCoord).rgb;
                // Use the brightest channel so pure red/blue elements bloom too,
                // not just green (luminance weights green ~10x blue and would drop them)
                float brightness = max(color.r, max(color.g, color.b));

                if (brightness > threshold) {
                    FragColor = vec4(color, 1.0);
                } else {
                    FragColor = vec4(0.0, 0.0, 0.0, 1.0);
                }
            }";

        string gaussianBlurFragment = @"
            #version 330 core
            in vec2 TexCoord;
            out vec4 FragColor;

            uniform sampler2D image;
            uniform bool horizontal;
            uniform float weight[5] = float[] (0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216);

            void main()
            {
                vec2 tex_offset = 1.0 / textureSize(image, 0);
                vec3 result = texture(image, TexCoord).rgb * weight[0];

                if (horizontal) {
                    for (int i = 1; i < 5; ++i) {
                        result += texture(image, TexCoord + vec2(tex_offset.x * i, 0.0)).rgb * weight[i];
                        result += texture(image, TexCoord - vec2(tex_offset.x * i, 0.0)).rgb * weight[i];
                    }
                } else {
                    for (int i = 1; i < 5; ++i) {
                        result += texture(image, TexCoord + vec2(0.0, tex_offset.y * i)).rgb * weight[i];
                        result += texture(image, TexCoord - vec2(0.0, tex_offset.y * i)).rgb * weight[i];
                    }
                }

                FragColor = vec4(result, 1.0);
            }";

        _copyShaderProgram = CreateShaderProgram(copyVertexShader, copyFragmentShader);
        _postProcessShaderProgram = CreateShaderProgram(copyVertexShader, postProcessFragmentShader);
        _bloomExtractProgram = CreateShaderProgram(copyVertexShader, bloomExtractFragment);
        _gaussianBlurProgram = CreateShaderProgram(copyVertexShader, gaussianBlurFragment);
    }

    private int CreateShaderProgram(string vertexSource, string fragmentSource)
    {
        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexSource);
        GL.CompileShader(vertexShader);

        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentSource);
        GL.CompileShader(fragmentShader);

        int program = GL.CreateProgram();
        GL.AttachShader(program, vertexShader);
        GL.AttachShader(program, fragmentShader);
        GL.LinkProgram(program);

        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        return program;
    }

    private void SetupFramebuffers()
    {
        var windowSize = CurrentWindowSize;

        _mainFramebuffer = GL.GenFramebuffer();
        _mainColorTexture = GL.GenTexture();

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _mainFramebuffer);

        GL.BindTexture(TextureTarget.Texture2D, _mainColorTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb16f, windowSize.X, windowSize.Y, 0, PixelFormat.Rgb, PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _mainColorTexture, 0);

        _bloomFramebuffer = GL.GenFramebuffer();
        _bloomColorTexture = GL.GenTexture();

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _bloomFramebuffer);

        GL.BindTexture(TextureTarget.Texture2D, _bloomColorTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb16f, windowSize.X / 2, windowSize.Y / 2, 0, PixelFormat.Rgb, PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _bloomColorTexture, 0);

        _tempFramebuffer = GL.GenFramebuffer();
        _tempColorTexture = GL.GenTexture();

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _tempFramebuffer);

        GL.BindTexture(TextureTarget.Texture2D, _tempColorTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb16f, windowSize.X / 2, windowSize.Y / 2, 0, PixelFormat.Rgb, PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _tempColorTexture, 0);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Update(float[] waveformData, float[] fftData, double deltaTime)
    {
        bool bloomAudioActive = _config.EnableBloom && _config.BloomAudioReactive;
        bool chromaticAudioActive = _config.EnableChromaticAberration && _config.ChromaticAudioReactive;
        if (!bloomAudioActive && !chromaticAudioActive)
        {
            _bloomAudioEnvelope = 0.0f;
            _chromaticAudioEnvelope = 0.0f;
            _maxBassLevel = 0.0f;
            return;
        }

        // Use low-frequency FFT bins to capture the beat (kick/bass drum)
        int binCount = Math.Min(8, fftData.Length);
        if (binCount <= 1)
        {
            _bloomAudioEnvelope = 0.0f;
            _chromaticAudioEnvelope = 0.0f;
            return;
        }

        float sum = 0.0f;
        for (int i = 1; i < binCount; i++) // Skip DC bin
        {
            sum += fftData[i] * fftData[i];
        }
        float bassLevel = (float)Math.Sqrt(sum / (binCount - 1));

        // Adaptive reference level so the pulse stays normalized to ~0-1
        _maxBassLevel *= (float)Math.Pow(0.98, deltaTime);
        _maxBassLevel = Math.Max(_maxBassLevel, 0.001f);
        if (bassLevel > _maxBassLevel)
        {
            _maxBassLevel = bassLevel;
        }

        float normalizedLevel = Math.Min(1.0f, bassLevel / _maxBassLevel);

        ApplyBeatEnvelope(ref _bloomAudioEnvelope, normalizedLevel, bloomAudioActive, _config.BloomAudioDecay, deltaTime);
        ApplyBeatEnvelope(ref _chromaticAudioEnvelope, normalizedLevel, chromaticAudioActive, _config.ChromaticAudioDecay, deltaTime);
    }

    // Spike up instantly on the beat, then decay at the system's own rate when no beat comes
    private static void ApplyBeatEnvelope(ref float envelope, float level, bool active, float decaySeconds, double deltaTime)
    {
        if (!active)
        {
            envelope = 0.0f;
            return;
        }

        if (level > envelope)
        {
            envelope = level;
        }
        else
        {
            float releaseFactor = (float)Math.Pow(0.001, deltaTime / Math.Max(0.1, decaySeconds));
            envelope *= releaseFactor;
        }
        envelope = Math.Max(0.0f, Math.Min(1.0f, envelope));
    }

    public void BeginCapture()
    {
        var windowSize = CurrentWindowSize;

        // Always ensure framebuffers are properly sized - OpenGL will handle redundant operations efficiently
        if (_initialized)
        {
            ResizeFramebuffers(windowSize);
        }

        // Bind main framebuffer to capture all rendering
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _mainFramebuffer);
        GL.Viewport(0, 0, windowSize.X, windowSize.Y);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }

    public void EndCaptureAndRender()
    {
        var windowSize = CurrentWindowSize;

        if (_config.EnableBloom)
        {
            ProcessBloom();
        }

        // Render final post-processed image to screen
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.Viewport(0, 0, windowSize.X, windowSize.Y);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        GL.UseProgram(_postProcessShaderProgram);

        // Set uniforms
        GL.Uniform1(GL.GetUniformLocation(_postProcessShaderProgram, "screenTexture"), 0);
        GL.Uniform1(GL.GetUniformLocation(_postProcessShaderProgram, "bloomTexture"), 1);
        GL.Uniform2(GL.GetUniformLocation(_postProcessShaderProgram, "resolution"), (float)windowSize.X, (float)windowSize.Y);
        GL.Uniform1(GL.GetUniformLocation(_postProcessShaderProgram, "time"), (float)(DateTime.Now.TimeOfDay.TotalSeconds));

        // Effect toggles
        GL.Uniform1(GL.GetUniformLocation(_postProcessShaderProgram, "enableBloom"), _config.EnableBloom ? 1 : 0);
        GL.Uniform1(GL.GetUniformLocation(_postProcessShaderProgram, "enableChromaticAberration"), _config.EnableChromaticAberration ? 1 : 0);
        GL.Uniform1(GL.GetUniformLocation(_postProcessShaderProgram, "enableVignette"), _config.EnableVignette ? 1 : 0);
        GL.Uniform1(GL.GetUniformLocation(_postProcessShaderProgram, "enableColorGrading"), _config.EnableColorGrading ? 1 : 0);
        GL.Uniform1(GL.GetUniformLocation(_postProcessShaderProgram, "enableFilmGrain"), _config.EnableFilmGrain ? 1 : 0);

        // Effect parameters

        // Bloom intensity, boosted by the bloom's own audio-reactive beat pulse
        float bloomIntensity = _config.BloomIntensity;
        if (_config.BloomAudioReactive)
        {
            bloomIntensity += _bloomAudioEnvelope * _config.BloomAudioStrength;
            bloomIntensity = Math.Max(0.0f, bloomIntensity);
        }
        GL.Uniform1(GL.GetUniformLocation(_postProcessShaderProgram, "bloomIntensity"), bloomIntensity);

        // Chromatic aberration strength, boosted by the CA's own audio-reactive beat pulse
        float chromaticStrength = _config.ChromaticStrength;
        if (_config.ChromaticAudioReactive)
        {
            chromaticStrength += _chromaticAudioEnvelope * _config.ChromaticAudioStrength;
            chromaticStrength = Math.Max(0.0f, chromaticStrength);
        }
        GL.Uniform1(GL.GetUniformLocation(_postProcessShaderProgram, "chromaticStrength"), chromaticStrength);
        GL.Uniform1(GL.GetUniformLocation(_postProcessShaderProgram, "vignetteStrength"), _config.VignetteStrength);
        GL.Uniform1(GL.GetUniformLocation(_postProcessShaderProgram, "vignetteSize"), _config.VignetteSize);
        GL.Uniform1(GL.GetUniformLocation(_postProcessShaderProgram, "contrast"), _config.Contrast);
        GL.Uniform1(GL.GetUniformLocation(_postProcessShaderProgram, "brightness"), _config.Brightness);
        GL.Uniform1(GL.GetUniformLocation(_postProcessShaderProgram, "saturation"), _config.Saturation);

        // Use time-based color for tint if enabled
        Vector3 colorTint = _config.UseTimeColorTint ? TimeColorHelper.GetTimeBasedColor() :
                          _config.UseRealTimeColorTint ? TimeColorHelper.GetRealTimeBasedColor() : _config.ColorTint;
        if (_config.InvertColorTint) colorTint = TimeColorHelper.InvertColor(colorTint);

        GL.Uniform3(GL.GetUniformLocation(_postProcessShaderProgram, "colorTint"), colorTint);

        GL.Uniform1(GL.GetUniformLocation(_postProcessShaderProgram, "grainStrength"), _config.GrainStrength);

        // Bind textures
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _mainColorTexture);
        GL.ActiveTexture(TextureUnit.Texture1);
        GL.BindTexture(TextureTarget.Texture2D, _bloomColorTexture);

        // Render fullscreen quad
        GL.BindVertexArray(_quadVAO);
        GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
        GL.BindVertexArray(0);

        GL.UseProgram(0);
    }

    private void ProcessBloom()
    {
        var windowSize = CurrentWindowSize;
        Vector2i bloomSize = new(windowSize.X / 2, windowSize.Y / 2);

        // Extract bright areas
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _bloomFramebuffer);
        GL.Viewport(0, 0, bloomSize.X, bloomSize.Y);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        GL.UseProgram(_bloomExtractProgram);
        GL.Uniform1(GL.GetUniformLocation(_bloomExtractProgram, "screenTexture"), 0);
        GL.Uniform1(GL.GetUniformLocation(_bloomExtractProgram, "threshold"), _config.BloomThreshold);

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _mainColorTexture);

        GL.BindVertexArray(_quadVAO);
        GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
        GL.BindVertexArray(0);

        // Blur the bright areas (ping-pong between bloom and temp framebuffers)
        int iterations = (int)_config.BloomRadius;
        GL.UseProgram(_gaussianBlurProgram);

        for (int i = 0; i < iterations; i++)
        {
            // Horizontal blur
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _tempFramebuffer);
            GL.Uniform1(GL.GetUniformLocation(_gaussianBlurProgram, "horizontal"), 1);
            GL.Uniform1(GL.GetUniformLocation(_gaussianBlurProgram, "image"), 0);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _bloomColorTexture);

            GL.BindVertexArray(_quadVAO);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);

            // Vertical blur
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _bloomFramebuffer);
            GL.Uniform1(GL.GetUniformLocation(_gaussianBlurProgram, "horizontal"), 0);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _tempColorTexture);

            GL.BindVertexArray(_quadVAO);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);
        }

        GL.UseProgram(0);
    }

    private void ResizeFramebuffers(Vector2i newSize)
    {
        // Resize main texture
        GL.BindTexture(TextureTarget.Texture2D, _mainColorTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb16f, newSize.X, newSize.Y, 0, PixelFormat.Rgb, PixelType.Float, IntPtr.Zero);

        // Resize bloom textures (half resolution)
        GL.BindTexture(TextureTarget.Texture2D, _bloomColorTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb16f, newSize.X / 2, newSize.Y / 2, 0, PixelFormat.Rgb, PixelType.Float, IntPtr.Zero);

        GL.BindTexture(TextureTarget.Texture2D, _tempColorTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb16f, newSize.X / 2, newSize.Y / 2, 0, PixelFormat.Rgb, PixelType.Float, IntPtr.Zero);

        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    public void RenderConfigGui()
    {
        ImGui.Text("Post-Processing Effects");
        ImGui.Separator();

        if (ImGui.CollapsingHeader("Bloom", ImGuiTreeNodeFlags.DefaultOpen))
        {
            bool enableBloom = _config.EnableBloom;
            if (ImGui.Checkbox("Enable Bloom", ref enableBloom))
            {
                _config.EnableBloom = enableBloom;
            }

            if (_config.EnableBloom)
            {
                float threshold = _config.BloomThreshold;
                if (ImGui.SliderFloat("Threshold", ref threshold, 0.0f, 1.0f))
                {
                    _config.BloomThreshold = threshold;
                }

                float intensity = _config.BloomIntensity;
                if (ImGui.SliderFloat("Intensity", ref intensity, 0.0f, 3.0f))
                {
                    _config.BloomIntensity = intensity;
                }

                float radius = _config.BloomRadius;
                if (ImGui.SliderFloat("Radius", ref radius, 1.0f, 10.0f))
                {
                    _config.BloomRadius = radius;
                }

                ImGui.Spacing();
                ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Audio Reactive");
                ImGui.Separator();

                bool audioReactive = _config.BloomAudioReactive;
                if (ImGui.Checkbox("Audio Reactive##Bloom", ref audioReactive))
                {
                    _config.BloomAudioReactive = audioReactive;
                    if (!audioReactive)
                    {
                        _bloomAudioEnvelope = 0.0f;
                        if (!(_config.EnableChromaticAberration && _config.ChromaticAudioReactive))
                        {
                            _maxBassLevel = 0.0f;
                        }
                    }
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Bloom spikes up on the beat of the audio");
                }

                if (_config.BloomAudioReactive)
                {
                    float audioStrength = _config.BloomAudioStrength;
                    if (ImGui.SliderFloat("Audio Reaction Strength##Bloom", ref audioStrength, 0.0f, 3.0f, "%.4f"))
                    {
                        _config.BloomAudioStrength = audioStrength;
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("How strongly the bloom reacts to audio (max extra strength added on the beat)");
                    }

                    float decay = _config.BloomAudioDecay;
                    if (ImGui.SliderFloat("Decay Time (s)##Bloom", ref decay, 0.01f, 10.0f))
                    {
                        _config.BloomAudioDecay = decay;
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("How long the spike lasts after the beat passes (higher = slower decrease)");
                    }

                    ImGui.Spacing();
                    ImGui.Text($"Current Beat Pulse: {_bloomAudioEnvelope:F3}");
                }
            }

            if (ImGui.Button("Reset Bloom"))
            {
                ResetBloom();
            }
        }

        ImGui.Spacing();
        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Chromatic Aberration", ImGuiTreeNodeFlags.DefaultOpen))
        {
            bool enableCA = _config.EnableChromaticAberration;
            if (ImGui.Checkbox("Enable Chromatic Aberration", ref enableCA))
            {
                _config.EnableChromaticAberration = enableCA;
            }

            if (_config.EnableChromaticAberration)
            {
                float strength = _config.ChromaticStrength;
                if (ImGui.SliderFloat("Strength", ref strength, 0.0f, 0.05f))
                {
                    _config.ChromaticStrength = strength;
                }

                ImGui.Spacing();
                ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Audio Reactive");
                ImGui.Separator();

                bool audioReactive = _config.ChromaticAudioReactive;
                if (ImGui.Checkbox("Audio Reactive##CA", ref audioReactive))
                {
                    _config.ChromaticAudioReactive = audioReactive;
                    if (!audioReactive)
                    {
                        _chromaticAudioEnvelope = 0.0f;
                        if (!(_config.EnableBloom && _config.BloomAudioReactive))
                        {
                            _maxBassLevel = 0.0f;
                        }
                    }
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Chromatic aberration spikes up on the beat of the audio");
                }

                if (_config.ChromaticAudioReactive)
                {
                    float audioStrength = _config.ChromaticAudioStrength;
                    if (ImGui.SliderFloat("Audio Reaction Strength##CA", ref audioStrength, 0.0f, 0.05f, "%.4f"))
                    {
                        _config.ChromaticAudioStrength = audioStrength;
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("How strongly the aberration reacts to audio (max extra strength added on the beat)");
                    }

                    float decay = _config.ChromaticAudioDecay;
                    if (ImGui.SliderFloat("Decay Time (s)##CA", ref decay, 0.01f, 10.0f))
                    {
                        _config.ChromaticAudioDecay = decay;
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("How long the spike lasts after the beat passes (higher = slower decrease)");
                    }

                    ImGui.Spacing();
                    ImGui.Text($"Current Beat Pulse: {_chromaticAudioEnvelope:F3}");
                }
            }

            if (ImGui.Button("Reset Chromatic Aberration"))
            {
                ResetChromaticAberration();
            }
        }

        ImGui.Spacing();
        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Vignette", ImGuiTreeNodeFlags.DefaultOpen))
        {
            bool enableVignette = _config.EnableVignette;
            if (ImGui.Checkbox("Enable Vignette", ref enableVignette))
            {
                _config.EnableVignette = enableVignette;
            }

            if (_config.EnableVignette)
            {
                float strength = _config.VignetteStrength;
                if (ImGui.SliderFloat("Strength##Vignette", ref strength, 0.0f, 1.25f))
                {
                    _config.VignetteStrength = strength;
                }

                float size = _config.VignetteSize;
                if (ImGui.SliderFloat("Size", ref size, 0.0f, 1.0f))
                {
                    _config.VignetteSize = size;
                }
            }

            if (ImGui.Button("Reset Vignette"))
            {
                ResetVignette();
            }
        }

        ImGui.Spacing();
        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Color Grading", ImGuiTreeNodeFlags.DefaultOpen))
        {
            bool enableColorGrading = _config.EnableColorGrading;
            if (ImGui.Checkbox("Enable Color Grading", ref enableColorGrading))
            {
                _config.EnableColorGrading = enableColorGrading;
            }

            if (_config.EnableColorGrading)
            {
                float contrast = _config.Contrast;
                if (ImGui.SliderFloat("Contrast", ref contrast, 0.0f, 3.0f))
                {
                    _config.Contrast = contrast;
                }

                float brightness = _config.Brightness;
                if (ImGui.SliderFloat("Brightness", ref brightness, -0.5f, 0.5f))
                {
                    _config.Brightness = brightness;
                }

                float saturation = _config.Saturation;
                if (ImGui.SliderFloat("Saturation", ref saturation, 0.0f, 3.0f))
                {
                    _config.Saturation = saturation;
                }

                bool useTimeColorTint = _config.UseTimeColorTint;
                if (ImGui.Checkbox("Rainbow Color Tint", ref useTimeColorTint))
                {
                    _config.UseTimeColorTint = useTimeColorTint;
                    if (useTimeColorTint) _config.UseRealTimeColorTint = false; // Disable other color mode
                }
                ImGui.SameLine();
                bool useRealTimeColorTint = _config.UseRealTimeColorTint;
                if (ImGui.Checkbox("Time-based RGB Tint (H:M:S)", ref useRealTimeColorTint))
                {
                    _config.UseRealTimeColorTint = useRealTimeColorTint;
                    if (useRealTimeColorTint) _config.UseTimeColorTint = false; // Disable other color mode
                }
                ImGui.SameLine();
                bool invertColorTint = _config.InvertColorTint;
                if (ImGui.Checkbox("Invert Color Tint", ref invertColorTint))
                {
                    _config.InvertColorTint = invertColorTint;
                }

                if (!_config.UseTimeColorTint && !_config.UseRealTimeColorTint)
                {
                    var colorTint = new System.Numerics.Vector3(_config.ColorTint.X, _config.ColorTint.Y, _config.ColorTint.Z);
                    if (ImGui.ColorEdit3("Color Tint", ref colorTint))
                    {
                        _config.ColorTint = new Vector3(colorTint.X, colorTint.Y, colorTint.Z);
                    }
                }
            }

            if (ImGui.Button("Reset Color Grading"))
            {
                ResetColorGrading();
            }
        }

        ImGui.Spacing();
        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Film Grain", ImGuiTreeNodeFlags.DefaultOpen))
        {
            bool enableGrain = _config.EnableFilmGrain;
            if (ImGui.Checkbox("Enable Film Grain", ref enableGrain))
            {
                _config.EnableFilmGrain = enableGrain;
            }

            if (_config.EnableFilmGrain)
            {
                float strength = _config.GrainStrength;
                if (ImGui.SliderFloat("Strength##Grain", ref strength, 0.0f, 0.5f))
                {
                    _config.GrainStrength = strength;
                }
            }

            if (ImGui.Button("Reset Film Grain"))
            {
                ResetFilmGrain();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("Reset to Defaults"))
        {
            ResetToDefaults();
        }
    }

    public string SaveConfiguration()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new VectorJsonConverter() }
            };
            return JsonSerializer.Serialize(_config, options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save PostProcessing config: {ex.Message}");
            return "{}";
        }
    }

    public void LoadConfiguration(string json)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                Converters = { new VectorJsonConverter() }
            };
            var config = JsonSerializer.Deserialize<PostProcessingConfig>(json, options);
            if (config != null)
            {
                _config = config;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load PostProcessing config: {ex.Message}");
        }
    }

    public void ResetToDefaults()
    {
        _config = new PostProcessingConfig();
        _bloomAudioEnvelope = 0.0f;
        _chromaticAudioEnvelope = 0.0f;
        _maxBassLevel = 0.0f;
    }

    public void ResetBloom()
    {
        var defaultConfig = new PostProcessingConfig();
        _config.EnableBloom = defaultConfig.EnableBloom;
        _config.BloomThreshold = defaultConfig.BloomThreshold;
        _config.BloomIntensity = defaultConfig.BloomIntensity;
        _config.BloomRadius = defaultConfig.BloomRadius;
        _config.BloomAudioReactive = defaultConfig.BloomAudioReactive;
        _config.BloomAudioStrength = defaultConfig.BloomAudioStrength;
        _config.BloomAudioDecay = defaultConfig.BloomAudioDecay;
        _bloomAudioEnvelope = 0.0f;
        if (!(_config.EnableChromaticAberration && _config.ChromaticAudioReactive))
        {
            _maxBassLevel = 0.0f;
        }
    }

    public void ResetChromaticAberration()
    {
        var defaultConfig = new PostProcessingConfig();
        _config.EnableChromaticAberration = defaultConfig.EnableChromaticAberration;
        _config.ChromaticStrength = defaultConfig.ChromaticStrength;
        _config.ChromaticAudioReactive = defaultConfig.ChromaticAudioReactive;
        _config.ChromaticAudioStrength = defaultConfig.ChromaticAudioStrength;
        _config.ChromaticAudioDecay = defaultConfig.ChromaticAudioDecay;
        _chromaticAudioEnvelope = 0.0f;
        if (!(_config.EnableBloom && _config.BloomAudioReactive))
        {
            _maxBassLevel = 0.0f;
        }
    }

    public void ResetVignette()
    {
        var defaultConfig = new PostProcessingConfig();
        _config.EnableVignette = defaultConfig.EnableVignette;
        _config.VignetteStrength = defaultConfig.VignetteStrength;
        _config.VignetteSize = defaultConfig.VignetteSize;
    }

    public void ResetColorGrading()
    {
        var defaultConfig = new PostProcessingConfig();
        _config.EnableColorGrading = defaultConfig.EnableColorGrading;
        _config.Contrast = defaultConfig.Contrast;
        _config.Brightness = defaultConfig.Brightness;
        _config.Saturation = defaultConfig.Saturation;
        _config.ColorTint = defaultConfig.ColorTint;
    }

    public void ResetFilmGrain()
    {
        var defaultConfig = new PostProcessingConfig();
        _config.EnableFilmGrain = defaultConfig.EnableFilmGrain;
        _config.GrainStrength = defaultConfig.GrainStrength;
    }

    public void Dispose()
    {
        if (!_initialized) return;

        GL.DeleteFramebuffer(_mainFramebuffer);
        GL.DeleteFramebuffer(_bloomFramebuffer);
        GL.DeleteFramebuffer(_tempFramebuffer);
        GL.DeleteTexture(_mainColorTexture);
        GL.DeleteTexture(_bloomColorTexture);
        GL.DeleteTexture(_tempColorTexture);
        GL.DeleteProgram(_copyShaderProgram);
        GL.DeleteProgram(_postProcessShaderProgram);
        GL.DeleteProgram(_bloomExtractProgram);
        GL.DeleteProgram(_gaussianBlurProgram);
        GL.DeleteVertexArray(_quadVAO);
        GL.DeleteBuffer(_quadVBO);
    }
}
