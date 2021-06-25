#include "pch.h"
#include "EncoderContext.h"



extern "C"
{
	DLL_EXPORT(EncoderContext*) EncoderContext_Create();

	DLL_EXPORT(void) EncoderContext_Delete(EncoderContext* handle);

	DLL_EXPORT(int) EncoderContext_Open(EncoderContext* handle, char* name, char* options, EncoderSpec* codecProperties, EncoderBitrate* encoderBitrate, EncoderProperties* encoderProperties, CodecProperties* outCodecProperties);

	DLL_EXPORT(int) EncoderContext_Write(EncoderContext* handle, AVFrame* frame, int iFrame);

	DLL_EXPORT(int) EncoderContext_Read(EncoderContext* handle, AVPacket* packet, PacketProperties* packetProperties);

	DLL_EXPORT(void) EncoderContext_UpdateBitrate(EncoderContext* handle, EncoderBitrate* encoderBitrate);
}


DLL_EXPORT(EncoderContext*) EncoderContext_Create()
{
	return new EncoderContext();
}


DLL_EXPORT(void) EncoderContext_Delete(EncoderContext* handle)
{
	delete handle;
}

DLL_EXPORT(void) EncoderContext_UpdateBitrate(EncoderContext* handle, EncoderBitrate* encoderBitrate)
{
	if (handle->context)
	{
		if (encoderBitrate->bit_rate)
			handle->context->bit_rate = (int64_t)encoderBitrate->bit_rate * 1000;

		if (encoderBitrate->max_rate)
			handle->context->rc_max_rate = (int64_t)encoderBitrate->max_rate * 1000;

		if (encoderBitrate->buffer_size)
			handle->context->rc_buffer_size = (int64_t)encoderBitrate->buffer_size * 1000;
	}
}

DLL_EXPORT(int) EncoderContext_Open(EncoderContext* handle, char* name, char* options, EncoderSpec* encoderSpec, EncoderBitrate* encoderBitrate, EncoderProperties* encoderProperties, CodecProperties* outCodecProperties)
{
	int result = ErrorCodes::Ok;
	try
	{
		AVCodec* codec = avcodec_find_encoder_by_name(name);
		if (!codec)
			THROW("Codec not found");

		handle->context = avcodec_alloc_context3(codec);
		if (!handle->context)
			THROW("Could not allocate video codec context");

		Info("Open enc %s", name);

		if (encoderSpec->width)
		{
			handle->context->height = encoderSpec->height;
			handle->context->width = encoderSpec->width;
			handle->context->sample_aspect_ratio = encoderSpec->sample_aspect_ratio;
			handle->context->pix_fmt = encoderProperties->pix_fmt = codec->pix_fmts[0];
		}
		else
		{
			handle->context->sample_rate = encoderSpec->sample_rate;
			handle->context->channel_layout = encoderSpec->channel_layout;
			handle->context->channels = av_get_channel_layout_nb_channels(encoderSpec->channel_layout);
			handle->context->sample_fmt = encoderProperties->sample_fmt = codec->sample_fmts[0]; /* take first format from list of supported formats */
			int i = 1;
			while (codec->sample_fmts[i] != AV_SAMPLE_FMT_NONE) // try to find 16 bits sample format
			{
				int bytes = av_get_bytes_per_sample(codec->sample_fmts[i]);
				if (bytes == 2)
				{
					handle->context->sample_fmt = codec->sample_fmts[i];
					break;
				}
				i++;
			}
		}
		handle->context->time_base = encoderSpec->time_base;
		handle->context->flags |= AV_CODEC_FLAG_GLOBAL_HEADER;


		AVDictionary* dict = NULL;
		if (options)
			av_dict_parse_string(&dict, options, KEY_VALUE_SEPARATOR, PAIRS_SEPARATOR, 0);

		EncoderContext_UpdateBitrate(handle, encoderBitrate);

		CHECK(avcodec_open2(handle->context, codec, &dict));

		if (!encoderSpec->width && !(handle->context->codec->capabilities & AV_CODEC_CAP_VARIABLE_FRAME_SIZE))
			encoderProperties->required_frame_size = handle->context->frame_size;

		if (dict != NULL)
			THROW("Options are not accepted");

		CodecProperties::CopyParams(outCodecProperties, handle->context);
	}
	catch (const streamer_exception& e)
	{
		avcodec_free_context(&handle->context);
		result = LogAndReturn(e, "EncoderContext_Open");
	}
	return result;
}

DLL_EXPORT(int) EncoderContext_Write(EncoderContext* handle, AVFrame* frame, int iFrame)
{
	//if (frame->width)
	//{
	//	Info(">>>>>>>>>>>>>>  %dx%dx%d %lld", frame->width, frame->height, frame->format, frame->pts);
	//}
	if (iFrame)
		frame->pict_type = AV_PICTURE_TYPE_I;
	else
		frame->pict_type = AV_PICTURE_TYPE_NONE;
	return avcodec_send_frame(handle->context, frame);
}

DLL_EXPORT(int) EncoderContext_Read(EncoderContext* handle, AVPacket* packet, PacketProperties* packetProperties)
{
	AVPacket pkt;

	av_init_packet(&pkt);
	pkt.data = NULL;
	pkt.size = 0;

	int res = avcodec_receive_packet(handle->context, &pkt);
	if (res == 0)
	{
		av_packet_ref(packet, &pkt);
		av_packet_unref(&pkt);
		PacketProperties::FromAVPacket(packetProperties, packet);

		/*if (packet->duration == 0)
		{
			Info("Encoder out %d %d", packet->flags, packet->size);
		}*/

		/*if (packet->duration == 0)
		{
			Info(">>>>>>>>>>>>>>  %d %d %lld %lld", packet->size, packet->flags, packet->pts, packet->dts);
		}*/
	}

	return res;
}