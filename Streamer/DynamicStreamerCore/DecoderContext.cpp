#include "pch.h"
#include "DecoderContext.h"
#include "InputContext.h"

extern "C"
{
	DLL_EXPORT(DecoderContext*) DecoderContext_Create();

	DLL_EXPORT(void) DecoderContext_Delete(DecoderContext* handle);

	DLL_EXPORT(int) DecoderContext_Open(DecoderContext* handle, CodecProperties* codecProperties, DecoderProperties* decoderProperties, CodecProperties* outCodecProperties);

	DLL_EXPORT(int) DecoderContext_Write(DecoderContext* handle, AVPacket* hPacket);

	DLL_EXPORT(int) DecoderContext_Read(DecoderContext* handle, AVFrame* frame, FrameProperties* PacketProperties);
}


DLL_EXPORT(DecoderContext*) DecoderContext_Create() 
{
	return new DecoderContext();
}


DLL_EXPORT(void) DecoderContext_Delete(DecoderContext* handle)
{
	delete handle;
}

DLL_EXPORT(int) DecoderContext_Open(DecoderContext* handle, CodecProperties* codecProperties, DecoderProperties* decoderProperties, CodecProperties* outCodecProperties)
{
	int result = ErrorCodes::Ok;

	AVCodecParameters* params = avcodec_parameters_alloc();
	try
	{
		CodecProperties::CopyParams(params, codecProperties);

		AVCodec* dec = avcodec_find_decoder(params->codec_id);
		if (!dec)
			THROW("Unable to find decoder");

		handle->context = avcodec_alloc_context3(dec);

		CHECK(avcodec_parameters_to_context(handle->context, params));

		if (params->codec_type == AVMEDIA_TYPE_VIDEO)
		{
			if (params->format == AV_PIX_FMT_NONE)
			{
				if (params->codec_id == AV_CODEC_ID_MJPEG)
					handle->context->pix_fmt = AV_PIX_FMT_YUV422P;
				else
					handle->context->pix_fmt = AV_PIX_FMT_YUYV422;
			}
			else if (handle->context->pix_fmt == AV_PIX_FMT_YUVJ422P)
				handle->context->pix_fmt = AV_PIX_FMT_YUV422P;
		}

		AVDictionary* dict = nullptr;

		av_dict_set(&dict, "refcounted_frames", "1", 0);

		CHECK(avcodec_open2(handle->context, dec, &dict));

		if (params->codec_type == AVMEDIA_TYPE_AUDIO && !handle->context->channel_layout)
			handle->context->channel_layout = av_get_default_channel_layout(handle->context->channels);

		decoderProperties->pix_fmt = handle->context->pix_fmt;
		decoderProperties->sample_fmt = handle->context->sample_fmt;
		decoderProperties->channel_layout = handle->context->channel_layout;

		CodecProperties::CopyParams(outCodecProperties, handle->context);
	}
	catch (const streamer_exception& e)
	{
		avcodec_free_context(&handle->context);
		result = LogAndReturn(e, "DecoderContext_Open");
	}

	avcodec_parameters_free(&params);
	return result;
}

DLL_EXPORT(int) DecoderContext_Write(DecoderContext* handle, AVPacket* hPacket)
{
	return avcodec_send_packet(handle->context, hPacket);
}

DLL_EXPORT(int) DecoderContext_Read(DecoderContext* handle, AVFrame* frame, FrameProperties* frameProperties)
{
	int res = avcodec_receive_frame(handle->context, frame);
	if (res == 0)
		FrameProperties::FromAVFrame(frameProperties, frame);
	return res;
}