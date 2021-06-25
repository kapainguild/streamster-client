#pragma once


class FilterContext
{
public:
	AVFilterContext* buffersink_ctx = nullptr;
	std::vector<AVFilterContext*> buffersrc_ctx;
	AVFilterGraph* filter_graph = nullptr;
	bool destructed = false;


	FilterContext()
	{
	}

	~FilterContext()
	{
		destructed = true;
		buffersink_ctx = nullptr;
		avfilter_graph_free(&filter_graph);
	}
};

struct FilterInputSpec
{
	//video
	AVPixelFormat pix_fmt;
	int width;
	int height;
	AVRational sample_aspect_ratio;
	AVColorRange color_range;
	int BestQuality;

	//audio
	int sample_rate;
	AVSampleFormat sample_fmt;
	uint64_t channel_layout;

	//common
	AVRational time_base;
};


struct FilterOutputSpec
{
	//video
	AVPixelFormat pix_fmt;

	//audio
	int sample_rate;
	AVSampleFormat sample_fmt;
	uint64_t channel_layout;
	int required_frame_size;
};


