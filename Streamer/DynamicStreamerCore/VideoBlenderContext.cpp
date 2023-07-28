#include "pch.h"
#include "VideoBlenderContext.h"


extern "C"
{
	DLL_EXPORT(VideoBlenderContext*) VideoBlenderContext_Create();

	DLL_EXPORT(void) VideoBlenderContext_Delete(VideoBlenderContext* handle);

	DLL_EXPORT(int) VideoBlenderContext_Init(VideoBlenderContext* handle, AVFrame* frame, int blendRgb);

	DLL_EXPORT(int) VideoBlenderContext_Add(VideoBlenderContext* handle, AVFrame* frame, int x, int y, int src_y_offset, int src_y_count);

	DLL_EXPORT(int) VideoBlenderContext_Get(VideoBlenderContext* handle, AVFrame* frame, int64_t pts, FrameProperties* PacketProperties);
}

static const enum AVPixelFormat alpha_pix_fmts[] = {
    AV_PIX_FMT_YUVA420P, AV_PIX_FMT_YUVA422P, AV_PIX_FMT_YUVA444P,
    AV_PIX_FMT_ARGB, AV_PIX_FMT_ABGR, AV_PIX_FMT_RGBA,
    AV_PIX_FMT_BGRA, AV_PIX_FMT_GBRAP, AV_PIX_FMT_NONE
};


#define R 0
#define G 1
#define B 2
#define A 3

enum { RED = 0, GREEN, BLUE, ALPHA };

int ff_fill_rgba_map(uint8_t* rgba_map, enum AVPixelFormat pix_fmt)
{
    switch (pix_fmt) {
    case AV_PIX_FMT_0RGB:
    case AV_PIX_FMT_ARGB:  rgba_map[ALPHA] = 0; rgba_map[RED] = 1; rgba_map[GREEN] = 2; rgba_map[BLUE] = 3; break;
    case AV_PIX_FMT_0BGR:
    case AV_PIX_FMT_ABGR:  rgba_map[ALPHA] = 0; rgba_map[BLUE] = 1; rgba_map[GREEN] = 2; rgba_map[RED] = 3; break;
    case AV_PIX_FMT_RGB48LE:
    case AV_PIX_FMT_RGB48BE:
    case AV_PIX_FMT_RGBA64BE:
    case AV_PIX_FMT_RGBA64LE:
    case AV_PIX_FMT_RGB0:
    case AV_PIX_FMT_RGBA:
    case AV_PIX_FMT_RGB24: rgba_map[RED] = 0; rgba_map[GREEN] = 1; rgba_map[BLUE] = 2; rgba_map[ALPHA] = 3; break;
    case AV_PIX_FMT_BGR48LE:
    case AV_PIX_FMT_BGR48BE:
    case AV_PIX_FMT_BGRA64BE:
    case AV_PIX_FMT_BGRA64LE:
    case AV_PIX_FMT_BGRA:
    case AV_PIX_FMT_BGR0:
    case AV_PIX_FMT_BGR24: rgba_map[BLUE] = 0; rgba_map[GREEN] = 1; rgba_map[RED] = 2; rgba_map[ALPHA] = 3; break;
    case AV_PIX_FMT_GBRP9LE:
    case AV_PIX_FMT_GBRP9BE:
    case AV_PIX_FMT_GBRP10LE:
    case AV_PIX_FMT_GBRP10BE:
    case AV_PIX_FMT_GBRP12LE:
    case AV_PIX_FMT_GBRP12BE:
    case AV_PIX_FMT_GBRP14LE:
    case AV_PIX_FMT_GBRP14BE:
    case AV_PIX_FMT_GBRP16LE:
    case AV_PIX_FMT_GBRP16BE:
    case AV_PIX_FMT_GBRAP:
    case AV_PIX_FMT_GBRAP10LE:
    case AV_PIX_FMT_GBRAP10BE:
    case AV_PIX_FMT_GBRAP12LE:
    case AV_PIX_FMT_GBRAP12BE:
    case AV_PIX_FMT_GBRAP16LE:
    case AV_PIX_FMT_GBRAP16BE:
    case AV_PIX_FMT_GBRP:  rgba_map[GREEN] = 0; rgba_map[BLUE] = 1; rgba_map[RED] = 2; rgba_map[ALPHA] = 3; break;
    default:                    
        return AVERROR(EINVAL);
    }
    return 0;
}

int ff_fmt_is_in(int fmt, const AVPixelFormat* fmts)
{
    const AVPixelFormat* p;

    for (p = fmts; *p != -1; p++) {
        if (fmt == *p)
            return 1;
    }
    return 0;
}

// divide by 255 and round to nearest
// apply a fast variant: (X+127)/255 = ((X+127)*257+257)>>16 = ((X+128)*257)>>16
#define FAST_DIV255(x) ((x) >> 8)
//((((x) + 128) * 257) >> 16)

// calculate the unpremultiplied alpha, applying the general equation:
// alpha = alpha_overlay / ( (alpha_main + alpha_overlay) - (alpha_main * alpha_overlay) )
// (((x) << 16) - ((x) << 9) + (x)) is a faster version of: 255 * 255 * x
// ((((x) + (y)) << 8) - ((x) + (y)) - (y) * (x)) is a faster version of: 255 * (x + y)
#define UNPREMULTIPLY_ALPHA(x, y) ((((x) << 16) - ((x) << 9) + (x)) / ((((x) + (y)) << 8) - ((x) + (y)) - (y) * (x)))


static void blend_image_packed_rgb(VideoBlenderContext* s, AVFrame* dst, const AVFrame* src,
    int main_has_alpha, int x, int y, int src_y_offset, int src_y_count, int is_straight)
{
    int i, imax, j, jmax;
    const int src_w = src->width;
    const int src_h = src->height;
    const int dst_w = dst->width;
    const int dst_h = dst->height;
    uint8_t alpha;          ///< the amount of overlay to blend on to main
    const int dr = s->main_rgba_map[R];
    const int dg = s->main_rgba_map[G];
    const int db = s->main_rgba_map[B];
    const int da = s->main_rgba_map[A];
    const int dstep = s->main_pix_step[0];
    const int sr = s->overlay_rgba_map[R];
    const int sg = s->overlay_rgba_map[G];
    const int sb = s->overlay_rgba_map[B];
    const int sa = s->overlay_rgba_map[A];
    const int sstep = s->overlay_pix_step[0];
    uint8_t* S, * sp, * d, * dp;

    i = FFMAX(-y, src_y_offset);
    sp = src->data[0] + i * src->linesize[0];
    dp = dst->data[0] + (y + i) * dst->linesize[0];

    for (imax = FFMIN(-y + dst_h, src_y_offset + src_y_count); i < imax; i++) {
        j = FFMAX(-x, 0);
        S = sp + j * sstep;
        d = dp + (x + j) * dstep;

        for (jmax = FFMIN(-x + dst_w, src_w); j < jmax; j++) {
            alpha = S[sa];

            // if the main channel has an alpha channel, alpha has to be calculated
            // to create an un-premultiplied (straight) alpha value
            if (main_has_alpha && alpha != 0 && alpha != 255) {
                uint8_t alpha_d = d[da];
                alpha = UNPREMULTIPLY_ALPHA(alpha, alpha_d);
            }

            switch (alpha) {
            case 0:
                break;
            case 255:
                d[dr] = S[sr];
                d[dg] = S[sg];
                d[db] = S[sb];
                break;
            default:
                // main_value = main_value * (1 - alpha) + overlay_value * alpha
                // since alpha is in the range 0-255, the result must divided by 255
                d[dr] = is_straight ? FAST_DIV255(d[dr] * (255 - alpha) + S[sr] * alpha) :
                    FFMIN(FAST_DIV255(d[dr] * (255 - alpha)) + S[sr], 255);
                d[dg] = is_straight ? FAST_DIV255(d[dg] * (255 - alpha) + S[sg] * alpha) :
                    FFMIN(FAST_DIV255(d[dg] * (255 - alpha)) + S[sg], 255);
                d[db] = is_straight ? FAST_DIV255(d[db] * (255 - alpha) + S[sb] * alpha) :
                    FFMIN(FAST_DIV255(d[db] * (255 - alpha)) + S[sb], 255);
            }
            if (main_has_alpha) {
                switch (alpha) {
                case 0:
                    break;
                case 255:
                    d[da] = S[sa];
                    break;
                default:
                    // apply alpha compositing: main_alpha += (1-main_alpha) * overlay_alpha
                    d[da] += FAST_DIV255((255 - d[da]) * S[sa]);
                }
            }
            d += dstep;
            S += sstep;
        }
        dp += dst->linesize[0];
        sp += src->linesize[0];
    }
}

static void blend_image_packed_rgb_not_straight_no_main_alpha(VideoBlenderContext* s, AVFrame* dst, const AVFrame* src, int x, int y, int src_y_offset, int src_y_count)
{
    int i, imax, j, jmax;
    const int src_w = src->width;
    const int src_h = src->height;
    const int dst_w = dst->width;
    const int dst_h = dst->height;
    uint8_t alpha;          ///< the amount of overlay to blend on to main
    const int dr = s->main_rgba_map[R];
    const int dg = s->main_rgba_map[G];
    const int db = s->main_rgba_map[B];
    const int da = s->main_rgba_map[A];
    const int dstep = s->main_pix_step[0];
    const int sr = s->overlay_rgba_map[R];
    const int sg = s->overlay_rgba_map[G];
    const int sb = s->overlay_rgba_map[B];
    const int sa = s->overlay_rgba_map[A];
    const int sstep = s->overlay_pix_step[0];
    uint8_t* S, * sp, * d, * dp;

    i = FFMAX(-y, src_y_offset);
    sp = src->data[0] + i * src->linesize[0];
    dp = dst->data[0] + (y + i) * dst->linesize[0];

    for (imax = FFMIN(-y + dst_h, src_y_offset + src_y_count); i < imax; i++) {
        j = FFMAX(-x, 0);
        S = sp + j * sstep;
        d = dp + (x + j) * dstep;

        for (jmax = FFMIN(-x + dst_w, src_w); j < jmax; j++)
        {
            alpha = S[sa];

            switch (alpha) {
            case 0:
                break;
            case 255:
                d[dr] = S[sr];
                d[dg] = S[sg];
                d[db] = S[sb];
                break;
            default:
                // main_value = main_value * (1 - alpha) + overlay_value * alpha
                // since alpha is in the range 0-255, the result must divided by 255
                d[dr] = FFMIN(FAST_DIV255(d[dr] * (255 - alpha)) + S[sr], 255);
                d[dg] = FFMIN(FAST_DIV255(d[dg] * (255 - alpha)) + S[sg], 255);
                d[db] = FFMIN(FAST_DIV255(d[db] * (255 - alpha)) + S[sb], 255);
            }
            d += dstep;
            S += sstep;
        }
        dp += dst->linesize[0];
        sp += src->linesize[0];
    }
}


static void blend_plane(
    AVFrame* dst, const AVFrame* src,
    int src_w, int src_h,
    int dst_w, int dst_h,
    int i, int hsub, int vsub,
    int x, int y, int src_y_offset, int src_y_count,
    int main_has_alpha,
    int dst_plane,
    int dst_offset,
    int dst_step,
    int straight,
    int yuv)
{
    int src_wp = AV_CEIL_RSHIFT(src_w, hsub);
    int src_hp = AV_CEIL_RSHIFT(src_y_offset + src_y_count, vsub);
    int dst_wp = AV_CEIL_RSHIFT(dst_w, hsub);
    int dst_hp = AV_CEIL_RSHIFT(dst_h, vsub);

    int src_y_offsetp = src_y_offset >> vsub;

    int yp = y >> vsub;
    int xp = x >> hsub;
    uint8_t* s, * sp, * d, * dp, * dap, * a, * da, * ap;
    int jmax, j, k, kmax;

    j = FFMAX(-yp, src_y_offsetp);
    sp = src->data[i] + j * src->linesize[i];
    dp = dst->data[dst_plane]
        + (yp + j) * dst->linesize[dst_plane]
        + dst_offset;
    ap = src->data[3] + (j << vsub) * src->linesize[3];
    dap = dst->data[3] + ((yp + j) << vsub) * dst->linesize[3];

    for (jmax = FFMIN(-yp + dst_hp, src_hp); j < jmax; j++) {
        k = FFMAX(-xp, 0);
        d = dp + (xp + k) * dst_step;
        s = sp + k;
        a = ap + (k << hsub);
        da = dap + ((xp + k) << hsub);

        for (kmax = FFMIN(-xp + dst_wp, src_wp); k < kmax; k++) {
            int alpha_v, alpha_h, alpha;

            // average alpha for color components, improve quality
            if (hsub && vsub && j + 1 < src_hp && k + 1 < src_wp) {
                alpha = (a[0] + a[src->linesize[3]] +
                    a[1] + a[src->linesize[3] + 1]) >> 2;
            }
            else if (hsub || vsub) {
                alpha_h = hsub && k + 1 < src_wp ?
                    (a[0] + a[1]) >> 1 : a[0];
                alpha_v = vsub && j + 1 < src_hp ?
                    (a[0] + a[src->linesize[3]]) >> 1 : a[0];
                alpha = (alpha_v + alpha_h) >> 1;
            }
            else
                alpha = a[0];
            // if the main channel has an alpha channel, alpha has to be calculated
            // to create an un-premultiplied (straight) alpha value
            if (main_has_alpha && alpha != 0 && alpha != 255) {
                // average alpha for color components, improve quality
                uint8_t alpha_d;
                if (hsub && vsub && j + 1 < src_hp && k + 1 < src_wp) {
                    alpha_d = (da[0] + da[dst->linesize[3]] +
                        da[1] + da[dst->linesize[3] + 1]) >> 2;
                }
                else if (hsub || vsub) {
                    alpha_h = hsub && k + 1 < src_wp ?
                        (da[0] + da[1]) >> 1 : da[0];
                    alpha_v = vsub && j + 1 < src_hp ?
                        (da[0] + da[dst->linesize[3]]) >> 1 : da[0];
                    alpha_d = (alpha_v + alpha_h) >> 1;
                }
                else
                    alpha_d = da[0];
                alpha = UNPREMULTIPLY_ALPHA(alpha, alpha_d);
            }
            if (straight) {
                // if (alpha == 255)
                //     *d = *s;
                // else 
                *d = FAST_DIV255(*d * (255 - alpha) + *s * alpha);
            }
            else {
                if (i && yuv)
                    *d = av_clip(FAST_DIV255((*d - 128) * (255 - alpha)) + *s - 128, -128, 128) + 128;
                else
                    *d = FFMIN(FAST_DIV255(*d * (255 - alpha)) + *s, 255);
            }
            s++;
            d += dst_step;
            da += 1 << hsub;
            a += 1 << hsub;
        }
        dp += dst->linesize[dst_plane];
        sp += src->linesize[i];
        ap += (1 << vsub) * src->linesize[3];
        dap += (1 << vsub) * dst->linesize[3];
    }
}

static  void blend_plane_yuv_optimized00(
    AVFrame* dst, const AVFrame* src,
    int src_w, int src_h,
    int dst_w, int dst_h,
    int i,
    int x, int y, int src_y_offset, int src_y_count,
    int dst_plane,
    int dst_offset,
    int dst_step)
{
    int src_wp = AV_CEIL_RSHIFT(src_w, 0);
    int src_hp = AV_CEIL_RSHIFT(src_y_offset + src_y_count, 0);
    int dst_wp = AV_CEIL_RSHIFT(dst_w, 0);
    int dst_hp = AV_CEIL_RSHIFT(dst_h, 0);
    int yp = y >> 0;
    int xp = x >> 0;
    uint8_t* s, * sp, * d, * dp, * dap, * a, * ap;
    int jmax, j, k, kmax;

    j = FFMAX(-yp, src_y_offset);
    sp = src->data[i] + j * src->linesize[i];
    dp = dst->data[dst_plane]
        + (yp + j) * dst->linesize[dst_plane]
        + dst_offset;
    ap = src->data[3] + (j << 0) * src->linesize[3];
    dap = dst->data[3] + ((yp + j) << 0) * dst->linesize[3];

    for (jmax = FFMIN(-yp + dst_hp, src_hp); j < jmax; j++) {
        k = FFMAX(-xp, 0);
        d = dp + (xp + k) * dst_step;
        s = sp + k;
        a = ap + (k << 0);

        for (kmax = FFMIN(-xp + dst_wp, src_wp); k < kmax; k++) 
        {
            int alpha = a[0];
            /*if (alpha == 255)
                *d = *s;
            else*/
            *d = FAST_DIV255(*d * (255 - alpha) + *s * alpha);
            s++;
            d += dst_step;
            a += 1;
        }
        dp += dst->linesize[dst_plane];
        sp += src->linesize[i];
        ap += (1 << 0) * src->linesize[3];
        dap += (1 << 0) * dst->linesize[3];
    }
}

static  void blend_plane_yuv_optimized11(
    AVFrame* dst, const AVFrame* src,
    int src_w, int src_h,
    int dst_w, int dst_h,
    int i,
    int x, int y, int src_y_offset, int src_y_count,
    int dst_plane,
    int dst_offset,
    int dst_step)
{
    int hsub = 1;
    int vsub = 1;
    int src_wp = AV_CEIL_RSHIFT(src_w, hsub);
    int src_hp = AV_CEIL_RSHIFT(src_y_offset + src_y_count, vsub);
    int dst_wp = AV_CEIL_RSHIFT(dst_w, hsub);
    int dst_hp = AV_CEIL_RSHIFT(dst_h, vsub);
    int yp = y >> vsub;
    int xp = x >> hsub;
    int src_y_offsetp = src_y_offset >> vsub;

    int src_hp_minus1 = src_hp - 1;
    int src_wp_minus1 = src_wp - 1;


    uint8_t* s, * sp, * d, * dp, * dap, * a, * da, * ap;
    int jmax, j, k, kmax;

    j = FFMAX(-yp, src_y_offsetp);
    sp = src->data[i] + j * src->linesize[i];
    dp = dst->data[dst_plane]
        + (yp + j) * dst->linesize[dst_plane]
        + dst_offset;
    ap = src->data[3] + (j << vsub) * src->linesize[3];
    dap = dst->data[3] + ((yp + j) << vsub) * dst->linesize[3];

    int alinesize = src->linesize[3];

    for (jmax = FFMIN(-yp + dst_hp, src_hp); j < jmax; j++) {
        k = FFMAX(-xp, 0);
        d = dp + (xp + k) * dst_step;
        s = sp + k;
        a = ap + (k << hsub);
        da = dap + ((xp + k) << hsub);

        for (kmax = FFMIN(-xp + dst_wp, src_wp); k < kmax; k++)
        {
            int alpha_v, alpha_h, alpha;

            // average alpha for color components, improve quality
            if (j < src_hp_minus1 && k < src_wp_minus1)
            {
                alpha = (a[0] + a[1] + a[alinesize] + a[alinesize + 1]) >> 2;
            }
            else
            {
                alpha_h = k < src_wp_minus1 ? (a[0] + a[1]) >> 1 : a[0];
                alpha_v = j < src_hp_minus1 ? (a[0] + a[alinesize]) >> 1 : a[0];
                alpha = (alpha_v + alpha_h) >> 1;
            }

            if (alpha == 255)
                *d = *s;
            else
                *d = FAST_DIV255(*d * (255 - alpha) + *s * alpha);
            s++;
            d += dst_step;
            a += 2;
        }
        dp += dst->linesize[dst_plane];
        sp += src->linesize[i];
        ap += (1 << vsub) * src->linesize[3];
        dap += (1 << vsub) * dst->linesize[3];
    }
}


static inline void alpha_composite(const AVFrame* src, const AVFrame* dst,
    int src_w, int src_h,
    int dst_w, int dst_h,
    int x, int y, int src_y_offset, int src_y_count)
{
    uint8_t alpha;          ///< the amount of overlay to blend on to main
    uint8_t* s, * sa, * d, * da;
    int i, imax, j, jmax;

    i = FFMAX(-y, src_y_offset);
    sa = src->data[3] + i * src->linesize[3];
    da = dst->data[3] + (y + i) * dst->linesize[3];

    for (imax = FFMIN(-y + dst_h, src_y_offset + src_y_count); i < imax; i++) {
        j = FFMAX(-x, 0);
        s = sa + j;
        d = da + x + j;

        for (jmax = FFMIN(-x + dst_w, src_w); j < jmax; j++) {
            alpha = *s;
            if (alpha != 0 && alpha != 255) {
                uint8_t alpha_d = *d;
                alpha = UNPREMULTIPLY_ALPHA(alpha, alpha_d);
            }
            switch (alpha) {
            case 0:
                break;
            case 255:
                *d = *s;
                break;
            default:
                // apply alpha compositing: main_alpha += (1-main_alpha) * overlay_alpha
                *d += FAST_DIV255((255 - *d) * *s);
            }
            d += 1;
            s += 1;
        }
        da += dst->linesize[3];
        sa += src->linesize[3];
    }
}

DLL_EXPORT(VideoBlenderContext*) VideoBlenderContext_Create()
{
    return new VideoBlenderContext();
}

DLL_EXPORT(void) VideoBlenderContext_Delete(VideoBlenderContext* handle)
{
    delete handle;
}

DLL_EXPORT(int) VideoBlenderContext_Init(VideoBlenderContext* handle, AVFrame* frame, int blendRgb)
{
    av_frame_make_writable(frame);
    handle->main_frame = frame;
    handle->blend_type = (BlendType)blendRgb;

    const AVPixFmtDescriptor* pix_desc = av_pix_fmt_desc_get((AVPixelFormat)frame->format);

    av_image_fill_max_pixsteps(handle->main_pix_step, NULL, pix_desc);

    //s->hsub = pix_desc->log2_chroma_w;
    //s->vsub = pix_desc->log2_chroma_h;

    handle->main_desc = pix_desc;
    handle->main_is_packed_rgb = ff_fill_rgba_map(handle->main_rgba_map, (AVPixelFormat)frame->format) >= 0;
    handle->main_has_alpha = ff_fmt_is_in((AVPixelFormat)frame->format, alpha_pix_fmts);
    return 0;
}

DLL_EXPORT(int) VideoBlenderContext_Add(VideoBlenderContext* ctx, AVFrame* src, int x, int y, int src_y_offset, int src_y_count)
{
    const AVPixFmtDescriptor* pix_desc = av_pix_fmt_desc_get((AVPixelFormat)src->format);
    av_image_fill_max_pixsteps(ctx->overlay_pix_step, NULL, pix_desc);
    ctx->overlay_is_packed_rgb = ff_fill_rgba_map(ctx->overlay_rgba_map, (AVPixelFormat)src->format) >= 0;

    AVFrame* dst = ctx->main_frame;

    if (ctx->blend_type == BlendType::Yuv420)
    {
        const int src_w = src->width;
        const int src_h = src->height;
        const int dst_w = dst->width;
        const int dst_h = dst->height;

        if (ctx->main_has_alpha)
        {
            blend_plane(dst, src, src_w, src_h, dst_w, dst_h, 0, 0, 0, x, y, src_y_offset, src_y_count, 1, ctx->main_desc->comp[0].plane, ctx->main_desc->comp[0].offset, ctx->main_desc->comp[0].step, 1, 1);
            blend_plane(dst, src, src_w, src_h, dst_w, dst_h, 1, 1, 1, x, y, src_y_offset, src_y_count, 1, ctx->main_desc->comp[1].plane, ctx->main_desc->comp[1].offset, ctx->main_desc->comp[1].step, 1, 1);
            blend_plane(dst, src, src_w, src_h, dst_w, dst_h, 2, 1, 1, x, y, src_y_offset, src_y_count, 1, ctx->main_desc->comp[2].plane, ctx->main_desc->comp[2].offset, ctx->main_desc->comp[2].step, 1, 1);

            alpha_composite(src, dst, src_w, src_h, dst_w, dst_h, x, y, src_y_offset, src_y_count);
        }
        else
        {
            blend_plane_yuv_optimized00(dst, src, src_w, src_h, dst_w, dst_h, 0, x, y, src_y_offset, src_y_count, ctx->main_desc->comp[0].plane, ctx->main_desc->comp[0].offset, ctx->main_desc->comp[0].step);
            blend_plane_yuv_optimized11(dst, src, src_w, src_h, dst_w, dst_h, 1, x, y, src_y_offset, src_y_count, ctx->main_desc->comp[1].plane, ctx->main_desc->comp[1].offset, ctx->main_desc->comp[1].step);
            blend_plane_yuv_optimized11(dst, src, src_w, src_h, dst_w, dst_h, 2, x, y, src_y_offset, src_y_count, ctx->main_desc->comp[2].plane, ctx->main_desc->comp[2].offset, ctx->main_desc->comp[2].step);
        }
    }
    else //BlendType::Rgb
    {
        if (ctx->main_has_alpha)
            blend_image_packed_rgb(ctx, dst, src, 1, x, y, src_y_offset, src_y_count, 0);
        else
            blend_image_packed_rgb_not_straight_no_main_alpha(ctx, dst, src, x, y, src_y_offset, src_y_count);
    }

    return 0;
}

DLL_EXPORT(int) VideoBlenderContext_Get(VideoBlenderContext* handle, AVFrame* frame, int64_t pts, FrameProperties* PacketProperties)
{
    av_frame_ref(frame, handle->main_frame);
    handle->main_frame = nullptr;
    frame->pts = pts;
    FrameProperties::FromAVFrame(PacketProperties, frame);
    return 0;
}
