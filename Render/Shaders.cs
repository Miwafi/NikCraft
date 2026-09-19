namespace NikCraft.Render;

/// <summary>All GLSL sources live here so the build output stays a single self-contained executable.</summary>
internal static class Shaders
{
    public const string ChunkVertex = @"#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec2 aUv;
layout (location = 2) in float aLight;

uniform mat4 uViewProjection;
uniform vec3 uChunkOffset;

out vec2 vUv;
out float vLight;
out float vViewDepth;

void main()
{
    vec4 worldPosition = vec4(aPosition + uChunkOffset, 1.0);
    vec4 clip = uViewProjection * worldPosition;

    gl_Position = clip;
    vUv = aUv;
    vLight = aLight;
    vViewDepth = clip.w;
}
";

    public const string ChunkFragment = @"#version 330 core
in vec2 vUv;
in float vLight;
in float vViewDepth;

uniform sampler2D uAtlas;
uniform vec3 uFogColor;
uniform float uFogStart;
uniform float uFogEnd;
uniform float uAlphaCutoff;
uniform float uAmbient;

out vec4 FragColor;

void main()
{
    vec4 texel = texture(uAtlas, vUv);

    if (uAlphaCutoff > 0.0 && texel.a < uAlphaCutoff)
    {
        discard;
    }

    vec3 color = texel.rgb * (vLight * uAmbient);

    float fogFactor = clamp((vViewDepth - uFogStart) / max(uFogEnd - uFogStart, 0.001), 0.0, 1.0);
    color = mix(color, uFogColor, fogFactor);

    FragColor = vec4(color, texel.a);
}
";

    public const string ParticleVertex = @"#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec2 aUv;
layout (location = 2) in vec4 aColor;

uniform mat4 uViewProjection;

out vec2 vUv;
out vec4 vColor;
out float vViewDepth;

void main()
{
    vec4 clip = uViewProjection * vec4(aPosition, 1.0);

    gl_Position = clip;
    vUv = aUv;
    vColor = aColor;
    vViewDepth = clip.w;
}
";

    public const string ParticleFragment = @"#version 330 core
in vec2 vUv;
in vec4 vColor;
in float vViewDepth;

uniform sampler2D uAtlas;
uniform vec3 uFogColor;
uniform float uFogStart;
uniform float uFogEnd;

out vec4 FragColor;

void main()
{
    vec4 texel = texture(uAtlas, vUv);

    if (texel.a < 0.35)
    {
        discard;
    }

    vec3 color = texel.rgb * vColor.rgb;

    float fogFactor = clamp((vViewDepth - uFogStart) / max(uFogEnd - uFogStart, 0.001), 0.0, 1.0);
    color = mix(color, uFogColor, fogFactor);

    FragColor = vec4(color, texel.a * vColor.a);
}
";

    public const string SkyVertex = @"#version 330 core
layout (location = 0) in vec2 aPosition;

out vec2 vNdc;

void main()
{
    vNdc = aPosition;
    gl_Position = vec4(aPosition, 1.0, 1.0);
}
";

    public const string SkyFragment = @"#version 330 core
in vec2 vNdc;

uniform mat4 uInverseViewProjection;
uniform vec3 uCameraPosition;
uniform vec3 uSkyTopColor;
uniform vec3 uSkyHorizonColor;
uniform vec3 uSunDirection;
uniform vec3 uSunColor;

out vec4 FragColor;

void main()
{
    vec4 farPoint = uInverseViewProjection * vec4(vNdc, 1.0, 1.0);
    vec3 direction = normalize(farPoint.xyz / farPoint.w - uCameraPosition);

    float height = clamp(direction.y * 2.1 + 0.06, 0.0, 1.0);
    vec3 color = mix(uSkyHorizonColor, uSkyTopColor, height);

    float sunAmount = max(dot(direction, normalize(uSunDirection)), 0.0);
    color += uSunColor * pow(sunAmount, 350.0) * 2.2;
    color += uSunColor * pow(sunAmount, 10.0) * 0.16;

    if (direction.y < 0.0)
    {
        float below = clamp(-direction.y * 3.5, 0.0, 1.0);
        color = mix(color, uSkyHorizonColor * 0.72, below);
    }

    FragColor = vec4(color, 1.0);
}
";

    public const string UiVertex = @"#version 330 core
layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec2 aUv;
layout (location = 2) in vec4 aColor;

uniform mat4 uProjection;

out vec2 vUv;
out vec4 vColor;

void main()
{
    gl_Position = uProjection * vec4(aPosition, 0.0, 1.0);
    vUv = aUv;
    vColor = aColor;
}
";

    public const string UiFragment = @"#version 330 core
in vec2 vUv;
in vec4 vColor;

uniform sampler2D uTexture;
uniform int uUseTexture;

out vec4 FragColor;

void main()
{
    vec4 color = vColor;

    if (uUseTexture == 1)
    {
        color *= texture(uTexture, vUv);
    }

    FragColor = color;
}
";

    public const string LineVertex = @"#version 330 core
layout (location = 0) in vec3 aPosition;

uniform mat4 uViewProjection;
uniform vec3 uOrigin;
uniform float uScale;

void main()
{
    vec3 position = uOrigin + aPosition * uScale;
    gl_Position = uViewProjection * vec4(position, 1.0);
}
";

    public const string LineFragment = @"#version 330 core
uniform vec4 uColor;

out vec4 FragColor;

void main()
{
    FragColor = uColor;
}
";
}
