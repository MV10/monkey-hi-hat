#version 460

// Adapted from foodini's "font renderer" at https://www.shadertoy.com/view/cdtBWl
// And my own simple font outlining demo at https://www.shadertoy.com/view/mlyczD

in vec2 fragCoord;
out vec4 fragColor;
uniform vec2 resolution;

// The completed visualization content behind the text.
uniform sampler2D base_image;

// Requires a font texture which matches the antialised X-channel dimensions, content,
// and layout used by the Shadertoy font texture. See Volt's Laboratory for a copy.
// Untested, but this might also work: https://evanw.github.io/font-texture-generator/
// although it would lack the SDF data in the W-channel used to create an outline.
uniform sampler2D font;

// Characters in a flat row,col layout per text dimensions above. Values 32 (space)
// through 126 (tilde) match the ASCII table. The rest is arbitrary. Refer to the
// constants defined in the Shadertoy version for details on the others. A value
// of 0 indicates end of line.
uniform isampler2D text;

// The (columns,rows) dimensions of the data in the fixed-size text array uniform. Not
// all array elements must be used. Character 0 is treated as end of line.
uniform ivec2 dimensions;

// Normalized coordinates of where printing should begin, with the standard layout of
// the lower left corner being (0,0). Printing is top-down, text[n,0] is the first
// row, text[n,1] is below that, etc., however character-drawing is bottom-up, which
// means printing from the top edge should be 1.0 minus row_size.
uniform vec2 start_position;

// Normalized value which determines character width and height (monospaced).
uniform float char_size;

// For time-based crossfading of data like Spotify playlist track info.
uniform float fade_level = 1.0;

// Optional, heavier borders can look better at lower resolutions
uniform float outline_weight = 0.55;

const float half_char_width = 1.0 / 32.0;
const float char_width = 1.0 / 16.0;

vec2 uv;
vec2 position;

void print_char(int c, out vec4 symbol, out vec4 border) 
{
    vec2 font_uv_offset = (uv - position) / char_size;
    
    symbol = vec4(0.0);
    border = vec4(0.0);

    if(font_uv_offset.x < -1.0 ||
       font_uv_offset.x >  1.0 ||
       font_uv_offset.y < -1.0 ||
       font_uv_offset.y >  1.0) {
        return;
    }
    
    float row = float(15 - c / 16);
    float col = float(c % 16);
    
    vec2 font_uv = vec2(half_char_width + char_width * col, half_char_width + char_width * row); 
    font_uv += font_uv_offset * half_char_width;

    vec4 texel = texture(font, font_uv);

    // add this to the base_image color
    symbol = vec4(texel.x);

    // subtract this from the base_image color
    border = vec4(step(texel.w, outline_weight) * step(texel.x, 0.5));

    return;
}

void main()
{
    // in ShaderToy:
    // vec2 uv = 2.0*fragCoord/iResolution.xy-1.0;
    // uv.y *= iResolution.y/iResolution.x;
    uv = 2.0 * fragCoord - 1.0;
    uv.y *= resolution.y / resolution.x;

    position = start_position;

    fragColor = texture(base_image, fragCoord);

    vec4 symbol = vec4(0.0);
    vec4 border = vec4(0.0);

    for(int y = 0; y < dimensions.y; y++)
    {
        for(int x = 0; x < dimensions.x; x++)
        {
            int code = int(texelFetch(text, ivec2(x, y), 0).r);

            if(code == 0) break;

            print_char(code, symbol, border);
            fragColor += symbol - border;
            position.x += char_size;
        }

        position.x = start_position.x;
        position.y -= char_size * 1.75;
    }
}
