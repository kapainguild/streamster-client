#pragma once




class InputContext
{
public:
	AVFormatContext* context;
	AVRational desired_time_base;


	InputContext()
	{
		context = avformat_alloc_context();
		desired_time_base.num = 1;
		desired_time_base.den = 0;
	}

	~InputContext()
	{
		if (context)
		{
			av_opt_free(context->pb);
			avio_context_free(&context->pb);
			avformat_close_input(&context);
		}

		// or avformat_free_context?
	}
};

struct InputStreamProperties
{
	CodecProperties CodecProps;
};



