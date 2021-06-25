#pragma once

enum BlendType
{
    Yuv420 = 0,
    Rgb = 1
};


class VideoBlenderContext
{
public:
    uint8_t main_is_packed_rgb;
    uint8_t main_rgba_map[4];
    uint8_t main_has_alpha;
    uint8_t overlay_is_packed_rgb;
    uint8_t overlay_rgba_map[4];

    int main_pix_step[4];       ///< steps per pixel for each plane of the main output
    int overlay_pix_step[4];    ///< steps per pixel for each plane of the overlay
    const AVPixFmtDescriptor* main_desc; ///< format descriptor for main input

    AVFrame* main_frame;
    BlendType blend_type;
};

