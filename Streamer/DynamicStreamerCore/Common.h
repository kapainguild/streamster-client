#pragma once


struct PacketProperties
{
	int64_t Pts;
	int64_t Dts;
	void* DataPtr;
	int Size;
	int StreamIndex;
	int Flags;
	
	static void FromAVPacket(PacketProperties* props, AVPacket* packet)
	{
		props->Dts = packet->dts;
		props->Pts = packet->pts;
		props->Size = packet->size;
		props->StreamIndex = packet->stream_index;
		props->Flags = packet->flags;
		props->DataPtr = packet->data;
	}
};

struct FramePlaneDesc
{
	void* DataPtr;
	int Stride;
	int StrideCount;
};

struct FrameProperties
{
	int Width;
	int Height;
	int Samples;
	int Format;
	int64_t Pts;
	void* DataPtr0;
	void* DataPtr1;
	void* DataPtr2;

	static void FromAVFrame(FrameProperties* props, AVFrame* frame)
	{
		props->Width = frame->width;
		props->Height = frame->height;
		props->Samples = frame->nb_samples;
		props->Pts = frame->pts;
		props->Format = frame->format;
		props->DataPtr0 = frame->data[0];
		props->DataPtr1 = frame->data[1];
		props->DataPtr2 = frame->data[2];
	}
};

struct CodecProperties
{
	AVMediaType codec_type;
	int codec_id;
	int codec_tag;

	int extradata_size;
	int format;
	int64_t bit_rate;
	int bits_per_coded_sample;
	int bits_per_raw_sample;
	int profile;
	int level;
	int width;
	int height;
	AVRational sample_aspect_ratio;

	int field_order;

	int color_range;
	int color_primaries;
	int color_trc;
	int color_space;
	int chroma_location;
	int video_delay;

	uint64_t channel_layout;
	int channels;
	int sample_rate;
	int block_align;
	int frame_size;
	int initial_padding;
	int trailing_padding;
	int seek_preroll;


	uint8_t extradata[1024];

	static void CopyParams(CodecProperties* i, AVCodecContext* ctx)
	{
		AVCodecParameters* params = avcodec_parameters_alloc();
		try
		{
			avcodec_parameters_from_context(params, ctx);
			CodecProperties::CopyParams(i, params);
			avcodec_parameters_free(&params);
		}
		catch (const streamer_exception&)
		{
			avcodec_parameters_free(&params);
			throw;
		}
	}

	static void CopyParams(CodecProperties* i, AVCodecParameters* cp)
	{
		i->codec_type = cp->codec_type;
		i->codec_id = cp->codec_id;
		i->codec_tag = cp->codec_tag;
		i->extradata_size = cp->extradata_size;

		i->format = cp->format;
		i->bit_rate = cp->bit_rate;
		i->bits_per_coded_sample = cp->bits_per_coded_sample;
		i->bits_per_raw_sample = cp->bits_per_raw_sample;
		i->profile = cp->profile;
		i->level = cp->level;
		i->width = cp->width;
		i->height = cp->height;
		i->sample_aspect_ratio = cp->sample_aspect_ratio;

		i->field_order = cp->field_order;

		i->color_range = cp->color_range;
		i->color_primaries = cp->color_primaries;
		i->color_trc = cp->color_trc;
		i->color_space = cp->color_space;
		i->chroma_location = cp->chroma_location;
		i->video_delay = cp->video_delay;

		i->channel_layout = cp->channel_layout;
		i->channels = cp->channels;
		i->sample_rate = cp->sample_rate;
		i->block_align = cp->block_align;
		i->frame_size = cp->frame_size;
		i->initial_padding = cp->initial_padding;
		i->trailing_padding = cp->trailing_padding;
		i->seek_preroll = cp->seek_preroll;

		if (cp->extradata_size >= sizeof(CodecProperties::extradata))
			THROW("extradata is too big");

		memcpy(i->extradata, cp->extradata, cp->extradata_size);
	}

	static void CopyParams(AVCodecParameters* i, CodecProperties* cp)
	{
		i->codec_type = cp->codec_type;
		i->codec_id = (AVCodecID)cp->codec_id;
		i->codec_tag = cp->codec_tag;
		i->extradata_size = cp->extradata_size;

		i->format = cp->format;
		i->bit_rate = cp->bit_rate;
		i->bits_per_coded_sample = cp->bits_per_coded_sample;
		i->bits_per_raw_sample = cp->bits_per_raw_sample;
		i->profile = cp->profile;
		i->level = cp->level;
		i->width = cp->width;
		i->height = cp->height;
		i->sample_aspect_ratio = cp->sample_aspect_ratio;

		i->field_order = (AVFieldOrder)cp->field_order;

		i->color_range = (AVColorRange)cp->color_range;
		i->color_primaries = (AVColorPrimaries)cp->color_primaries;
		i->color_trc = (AVColorTransferCharacteristic)cp->color_trc;
		i->color_space = (AVColorSpace)cp->color_space;
		i->chroma_location = (AVChromaLocation)cp->chroma_location;
		i->video_delay = cp->video_delay;

		i->channel_layout = cp->channel_layout;
		i->channels = cp->channels;
		i->sample_rate = cp->sample_rate;
		i->block_align = cp->block_align;
		i->frame_size = cp->frame_size;
		i->initial_padding = cp->initial_padding;
		i->trailing_padding = cp->trailing_padding;
		i->seek_preroll = cp->seek_preroll;

		if (cp->extradata_size)
		{
			i->extradata = (uint8_t*)av_mallocz(((size_t)cp->extradata_size) + ((size_t)AV_INPUT_BUFFER_PADDING_SIZE));
			memcpy(i->extradata, cp->extradata, cp->extradata_size);
		}
	}
};

