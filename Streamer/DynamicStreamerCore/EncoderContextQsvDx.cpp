#include "pch.h"

#include "QSV\QSV_Encoder.h"
#include "QSV\QSV_Encoder_Internal.h"

#include "EncoderContext.h"
#include "EncoderContextQsvDx.h"



extern "C"
{
	DLL_EXPORT(EncoderContextQsvDx*) EncoderContextQsvDx_Create();

	DLL_EXPORT(void) EncoderContextQsvDx_Delete(EncoderContextQsvDx* handle);

	DLL_EXPORT(int) EncoderContextQsvDx_Open(EncoderContextQsvDx* handle, char* options, 
		EncoderSpec* codecProperties, EncoderBitrate* encoderBitrate, 
		void* device, void* deviceCtx, CodecProperties* outCodecProperties);

	DLL_EXPORT(int) EncoderContextQsvDx_Write(EncoderContextQsvDx* handle, uintptr_t sharedHandle, int64_t pts, int iFrame);

	DLL_EXPORT(int) EncoderContextQsvDx_Read(EncoderContextQsvDx* handle, AVPacket* packet, PacketProperties* packetProperties);

	DLL_EXPORT(void) EncoderContextQsvDx_UpdateBitrate(EncoderContextQsvDx* handle, EncoderBitrate* encoderBitrate);
}


DLL_EXPORT(EncoderContextQsvDx*) EncoderContextQsvDx_Create()
{
	return new EncoderContextQsvDx();
}


DLL_EXPORT(void) EncoderContextQsvDx_Delete(EncoderContextQsvDx* handle)
{
	delete handle;
}

DLL_EXPORT(void) EncoderContextQsvDx_UpdateBitrate(EncoderContextQsvDx* handle, EncoderBitrate* encoderBitrate)
{
	auto p = &handle->setup;

	if (p->nTargetBitRate != encoderBitrate->bit_rate || p->nMaxBitRate != encoderBitrate->max_rate)
	{
		p->nTargetBitRate = encoderBitrate->bit_rate;
		p->nMaxBitRate = encoderBitrate->max_rate;

		handle->updateBitratePending = true;
	}
}

DLL_EXPORT(int) EncoderContextQsvDx_Open(EncoderContextQsvDx* handle, char* options,
	EncoderSpec* encoderSpec, EncoderBitrate* encoderBitrate,
	void* device, void* deviceCtx, CodecProperties* c)
{

	if (!handle->platform_logged)
	{
		handle->platform_logged = true;
		enum qsv_cpu_platform qsv_platform = qsv_get_cpu_platform();
		Info("Intel QSV platform %d", qsv_platform);
	}
	handle->device = device;
	handle->deviceCtx = deviceCtx;

	auto p = &handle->setup;
	memset(p, 0, sizeof(handle->setup));

	switch (encoderSpec->Quality)
	{
	case EncoderQuality::Speed:
		p->nTargetUsage = MFX_TARGETUSAGE_BEST_SPEED;
		break;
	case EncoderQuality::BalancedQuality:
		p->nTargetUsage = MFX_TARGETUSAGE_3;
		break;
	case EncoderQuality::Quality:
		p->nTargetUsage = MFX_TARGETUSAGE_BEST_QUALITY;
		break;
	default:
		p->nTargetUsage = MFX_TARGETUSAGE_BALANCED;
		break;
	}
	p->nWidth = encoderSpec->width;
	p->nHeight = encoderSpec->height;
	p->nAsyncDepth = 3; // latency, "ultra-low"
	p->nFpsNum = encoderSpec->time_base.den;
	p->nFpsDen = encoderSpec->time_base.num;
	p->nTargetBitRate = encoderBitrate->bit_rate;
	p->nMaxBitRate = encoderBitrate->max_rate;
	p->nCodecProfile = MFX_PROFILE_AVC_MAIN;
	p->nRateControl = MFX_RATECONTROL_CBR;
	p->nAccuracy = 1000;
	p->nConvergence = 1;
	p->nQPI = 23;
	p->nQPP = 23;
	p->nQPB = 23;
	p->nLADEPTH = 0; //latency, "ultra-low"
	p->nKeyIntSec = 2;
	p->nbFrames = 0;
	p->nICQQuality = 23;
	p->bMBBRC = false;
	p->bCQM = false;

	if (p->nLADEPTH > 0) {
		if (p->nLADEPTH > 100) p->nLADEPTH = 100; 
		else if (p->nLADEPTH < 10) p->nLADEPTH = 10;
	}

	qsv_t* q = qsv_encoder_open(p, device, deviceCtx);

	if (q)
	{
		handle->handle = q;

		c->bit_rate = ((int64_t)encoderBitrate->bit_rate) * 1000;
		c->codec_id = AV_CODEC_ID_H264;
		c->codec_type = AVMEDIA_TYPE_VIDEO;

		c->color_primaries = 2; // something taken from ffmpeg-qsv
		c->color_space = 2;// something taken from ffmpeg-qsv
		c->color_trc = 2;// something taken from ffmpeg-qsv

		c->format = AV_PIX_FMT_NV12;
		c->height = encoderSpec->height;
		c->width = encoderSpec->width;
		c->sample_aspect_ratio = encoderSpec->sample_aspect_ratio;

		uint8_t* pSPS, * pPPS;
		uint16_t nSPS, nPPS;
		qsv_encoder_headers(q, &pSPS, &pPPS, &nSPS, &nPPS);

		memcpy(c->extradata, pSPS, nSPS);
		memcpy(c->extradata + nSPS, pPPS, nPPS);

		c->extradata_size = nSPS + nPPS;

		return ErrorCodes::Ok;
	}
	return ErrorCodes::InternalErrorUnknown1;
}

DLL_EXPORT(int) EncoderContextQsvDx_Write(EncoderContextQsvDx* handle, uintptr_t sharedHandle, int64_t pts, int iFrame)
{
	auto p = &handle->setup;
	AVRational local;
	local.num = p->nFpsDen; // reverse
	local.den = p->nFpsNum;

	AVRational qsv;
	qsv.num = 1;
	qsv.den = 90000;

	int64_t qsv_pts = av_rescale_q(pts, local, qsv); 

	mfxBitstream* pStream = nullptr;

	uint64_t key_unused = 1;
	auto sts = qsv_encoder_encode_tex(handle->handle, qsv_pts, (uint32_t)sharedHandle, key_unused, &key_unused, &pStream);

	if (sts == MFX_ERR_NONE)
	{
		av_init_packet(&handle->pkt);
		handle->pkt.data = NULL;
		handle->pkt.size = 0;

		if (pStream && pStream->DataLength)
		{
			uint8_t* buffer_data = (uint8_t*)av_malloc(pStream->DataLength);
			memcpy(buffer_data, &pStream->Data[pStream->DataOffset], pStream->DataLength);
			av_packet_from_data(&handle->pkt, buffer_data, pStream->DataLength);

			handle->pkt.pts = handle->pkt.dts = av_rescale_q(pStream->TimeStamp, qsv, local);

			if (pStream->FrameType & MFX_FRAMETYPE_I)
				handle->pkt.flags |= AV_PKT_FLAG_KEY;
		}
		else
			Info("No packet read (Problem if many such messages)");
	}
	else if (sts == MFX_ERR_MORE_DATA)
	{
		Info("More data required for QsvDx");
		sts = MFX_ERR_NONE;
	}
	return sts;
}

DLL_EXPORT(int) EncoderContextQsvDx_Read(EncoderContextQsvDx* handle, AVPacket* packet, PacketProperties* packetProperties)
{
	if (handle->pkt.data)
	{
		av_packet_ref(packet, &handle->pkt);
		av_packet_unref(&handle->pkt);
		PacketProperties::FromAVPacket(packetProperties, packet);

		if (handle->updateBitratePending)
		{
			handle->updateBitratePending = false;
			// next code works only for AsyncDepth = 1
			/*while (true)
			{
				uint64_t key_unused = 1;
				mfxBitstream* pStream = nullptr;
				qsv_encoder_encode_tex(handle->handle, 0, (uint32_t)0, key_unused, &key_unused, &pStream);

				if (pStream && pStream->DataLength)
					break;
				else
					Sleep(10);
			}*/
			auto update = qsv_encoder_update(handle->handle, &handle->setup);
			if (update != MFX_ERR_NONE)
			{
				Warning("qsv_encoder_update returned %d. hard reconfig.", update);

				auto reconfig = qsv_encoder_reconfig(handle->handle, &handle->setup, handle->device, handle->deviceCtx);

				if (reconfig != MFX_ERR_NONE)
					Warning("qsv_encoder_reconfig returned %d.", reconfig);
			}
		}

		return 0;
	}
	else
		return -11; // try later
}
