#include "pch.h"
#include "FilterContext.h"


#include "pch.h"
#include "InputContext.h"
#include "FilterContext.h"

extern "C"
{
	DLL_EXPORT(FilterContext*) FilterContext_Create();

	DLL_EXPORT(void) FilterContext_Delete(FilterContext* handle);

	DLL_EXPORT(int) FilterContext_Open(FilterContext* handle, FilterInputSpec* inputSpec, int inputSpecCount, FilterOutputSpec* outputSpec, char* filterSpec);

	DLL_EXPORT(int) FilterContext_Write(FilterContext* handle, AVFrame* hFrame, int inputNo);

	DLL_EXPORT(int) FilterContext_Read(FilterContext* handle, AVFrame* frame, FrameProperties* PacketProperties);
}


DLL_EXPORT(FilterContext*) FilterContext_Create()
{
	return new FilterContext();
}


DLL_EXPORT(void) FilterContext_Delete(FilterContext* handle)
{
	delete handle;
}

DLL_EXPORT(int) FilterContext_Open(FilterContext* handle, FilterInputSpec* inputSpecs, int inputSpecCount, FilterOutputSpec* outputSpec, char* filterSpec)
{
	int result = ErrorCodes::Ok;

	char args[512];
	char inputName[512];
	
	AVFilterInOut* outputs = avfilter_inout_alloc();
	std::vector<AVFilterInOut*> inputs;
	handle->filter_graph = avfilter_graph_alloc();

	int video = inputSpecs[0].width > 0;

	const AVFilter* buffersrc = nullptr; 
	const AVFilter* buffersink = nullptr;

	try
	{
		if (video)
		{
			buffersrc = avfilter_get_by_name("buffer");
			buffersink = avfilter_get_by_name("buffersink");

			if (!buffersrc || !buffersink)
				THROW("filtering source or sink element not found");

			for (int q = 0; q < inputSpecCount; q++)
			{
				auto inputSpec = &inputSpecs[q];

				int targetRange = (inputSpec->color_range == AVCOL_RANGE_JPEG) ? 1 : 0;
				if (inputSpec->BestQuality)
				{
					sprintf_s(args, sizeof(args), "sws_flags=fast_bilinear:sws_dither=none:dst_range=%d", targetRange);
				}
				else
				{
					sprintf_s(args, sizeof(args), "sws_flags=neighbor:sws_dither=none:dst_range=%d", targetRange);
				}
				
				CHECK(av_opt_set(handle->filter_graph, "scale_sws_opts", args, 0));
				

				int px = inputSpec->pix_fmt;
				if (px == AV_PIX_FMT_YUVJ422P)
					px = AV_PIX_FMT_YUV422P;

				sprintf_s(inputName, sizeof(inputName), "in%d", q);
				sprintf_s(args, sizeof(args),
					"video_size=%dx%d:pix_fmt=%d:time_base=%d/%d:pixel_aspect=%d/%d",
					inputSpec->width,
					inputSpec->height,
					px,
					inputSpec->time_base.num,
					inputSpec->time_base.den,
					inputSpec->sample_aspect_ratio.num,
					inputSpec->sample_aspect_ratio.den);

				AVFilterContext* inputCtx = nullptr;
				CHECK(avfilter_graph_create_filter(&inputCtx, buffersrc, inputName, args, NULL, handle->filter_graph));
				handle->buffersrc_ctx.push_back(inputCtx);
			}

			CHECK(avfilter_graph_create_filter(&handle->buffersink_ctx, buffersink, "out", NULL, NULL, handle->filter_graph));
			CHECK(av_opt_set_bin(handle->buffersink_ctx, "pix_fmts", (uint8_t*)&outputSpec->pix_fmt/*comeback px*/, sizeof(outputSpec->pix_fmt), AV_OPT_SEARCH_CHILDREN));

			if (!handle->buffersink_ctx || handle->filter_graph->nb_filters == 0)
			{
				int qq = 0;
			}
		}
		else
		{
			buffersrc = avfilter_get_by_name("abuffer");
			buffersink = avfilter_get_by_name("abuffersink");

			if (!buffersrc || !buffersink)
				THROW("filtering source or sink element not found");

			for (int q = 0; q < inputSpecCount; q++)
			{
				auto inputSpec = &inputSpecs[q];

				sprintf_s(inputName, sizeof(inputName), "in%d", q);
				sprintf_s(args, sizeof(args),
					"time_base=%d/%d:sample_rate=%d:sample_fmt=%s:channel_layout=0x%" PRIx64,
					inputSpec->time_base.num,
					inputSpec->time_base.den,
					inputSpec->sample_rate,
					av_get_sample_fmt_name(inputSpec->sample_fmt),
					inputSpec->channel_layout);

				AVFilterContext* inputCtx = nullptr;
				CHECK(avfilter_graph_create_filter(&inputCtx, buffersrc, inputName, args, NULL, handle->filter_graph));
				handle->buffersrc_ctx.push_back(inputCtx);
			}

			CHECK(avfilter_graph_create_filter(&handle->buffersink_ctx, buffersink, "out", NULL, NULL, handle->filter_graph));
			CHECK(av_opt_set_bin(handle->buffersink_ctx, "sample_fmts", (uint8_t*)&outputSpec->sample_fmt, sizeof(outputSpec->sample_fmt), AV_OPT_SEARCH_CHILDREN));
			CHECK(av_opt_set_bin(handle->buffersink_ctx, "channel_layouts", (uint8_t*)&outputSpec->channel_layout, sizeof(outputSpec->channel_layout), AV_OPT_SEARCH_CHILDREN));
			CHECK(av_opt_set_bin(handle->buffersink_ctx, "sample_rates", (uint8_t*)&outputSpec->sample_rate, sizeof(outputSpec->sample_rate), AV_OPT_SEARCH_CHILDREN));

			if (!handle->buffersink_ctx)
			{
				int qq = 0;
			}
		}

		AVFilterInOut* lastInput = nullptr;
		for (int q = inputSpecCount - 1; q >= 0; q--)
		{
			auto input = avfilter_inout_alloc();
			input->name = av_strdup(handle->buffersrc_ctx[q]->name);
			input->filter_ctx = handle->buffersrc_ctx[q];
			input->pad_idx = 0;
			input->next = lastInput;
			lastInput = input;
			inputs.push_back(input);
		}

		outputs->name = av_strdup("out");
		outputs->filter_ctx = handle->buffersink_ctx;
		outputs->pad_idx = 0;
		outputs->next = NULL;

		CHECK(avfilter_graph_parse(handle->filter_graph, filterSpec, outputs, lastInput, nullptr));
		CHECK(avfilter_graph_config(handle->filter_graph, NULL));

		if (outputSpec->required_frame_size)
			av_buffersink_set_frame_size(handle->buffersink_ctx, outputSpec->required_frame_size);
	}
	catch (const streamer_exception& e)
	{
		result = LogAndReturn(e, "FilterContext_Open");
	}
	/*for (int q = 0; q < inputs.size(); q++)
		avfilter_inout_free(&inputs[q]);
	avfilter_inout_free(&outputs);*/
	return result;
}

DLL_EXPORT(int) FilterContext_Write(FilterContext* handle, AVFrame* frame, int inputNo)
{
	if (frame)
	{
		if (frame->format == AV_PIX_FMT_YUVJ422P)
			frame->format = AV_PIX_FMT_YUV422P;
		int res = av_buffersrc_add_frame_flags(handle->buffersrc_ctx[inputNo], frame, 0);
		return res;
	}
	else 
		return av_buffersrc_add_frame_flags(handle->buffersrc_ctx[inputNo], nullptr, 0);
}

DLL_EXPORT(int) FilterContext_Read(FilterContext* handle, AVFrame* frame, FrameProperties* frameProperties)
{
	int res = av_buffersink_get_frame(handle->buffersink_ctx, frame);
	if (res == 0)
		FrameProperties::FromAVFrame(frameProperties, frame);
	return res;
}