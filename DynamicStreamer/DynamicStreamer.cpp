// DynamicStreamer.cpp : Implementation of CDynamicStreamer

#include "stdafx.h"
#include "DynamicStreamer.h"
#include "DynamicStreamerStatistics.h"

//#define TRACE_PACKETTIME
//#define TRACE_FPS
//#define TRACE_PIPELINE

//#define FFMPEG_LOG_LEVEL AV_LOG_DEBUG
//#define FFMPEG_LOG_LEVEL AV_LOG_VERBOSE
#define FFMPEG_LOG_LEVEL AV_LOG_WARNING



ULONGLONG CurrentTime()
{
	FILETIME time;
	GetSystemTimeAsFileTime(&time);
	return *(ULONGLONG*)&time;
}

ULONGLONG lastInTime1 = 0;
ULONGLONG lastInTime2 = 0;
ULONGLONG lastOutTime1 = 0;
ULONGLONG lastOutTime2 = 0;
#ifdef TRACE_FPS
void TraceOut(ULONGLONG& last, int stream, AVPacket* packet, char* msg)
{
	if (stream == 0)
	{
		ULONGLONG time2 = CurrentTime();
		ATLTRACE("%s  %llu  %llu     %lld\r\n", msg, time2 / 10000, (time2 - last)/10000, packet->pts);
		//printf("%s  %llu  %llu \r\n", msg, time2 / 10000, (time2 - last) / 10000);
		last = time2;
	}
}

#define TRACE_FPS_PACKET(lastTime, stream, packet, msg) TraceOut(lastTime, stream, packet, msg)

#else

#define TRACE_FPS_PACKET(lastTime, stream, packet, msg)

#endif


#ifdef TRACE_PACKETTIME

int tracePacketPeriod = 0;
DWORD procId = GetCurrentProcessId();

void TraceOut(AVPacket* packet, char* msg)
{
	ULONGLONG time2 = CurrentTime();
	ATLTRACE("%s  %llu  %llu %llu      %d\r\n", msg, time2 / 10000, (packet)->pts, (packet)->dts, procId);
	printf("%s  %llu  %llu       %d\r\n", msg, time2 / 10000, (packet)->pts, procId); 
}

void TraceTime(AVPacket* packet, char* msg)
{
	if (packet->stream_index == 0 && packet->dts != packet->pts)
		TraceOut(packet, msg);
}

void MarkTime(AVPacket* packet)
{
	if (packet->stream_index == 0) 
	{
		tracePacketPeriod++; 
		if (tracePacketPeriod > 300) 
		{
			packet->dts = packet->pts - 1;
			tracePacketPeriod = 0;
			TraceOut(packet, "MARK ");
		}
	}
}

#define TRACE_TIME(packet, str) TraceTime(packet, str)

#define MARK_TRACE_TIME(packet)  MarkTime(packet)

#else

#define TRACE_TIME(a, str)

#define MARK_TRACE_TIME(a)

#endif




#ifdef TRACE_PIPELINE

ULONGLONG alltime = 0;
int counter = 0;

void TraceOut(BaseTask* task, char* msg)
{
	ULONGLONG time = CurrentTime();
	counter++;
	alltime += time - task->StartTime;
	ATLTRACE("%d %d %s  %llu   ->   %llu  (ave: %llu)\r\n", task->PacketNumber, task->StreamIndex, msg, time / 10000, (time - task->StartTime) / 10000, alltime / (counter*10000));
}

#define TRACE_PIPELINE_TIME(task, msg) TraceOut(task, msg)

#else

#define TRACE_PIPELINE_TIME(task, msg) 

#endif // TRACE_PIPELINE






char* KEY_VALUE_SEPARATOR = "=";
char* PAIRS_SEPARATOR = ":";

char* NEW_KEY_VALUE_SEPARATOR = "^";
char* NEW_PAIRS_SEPARATOR = "`";


DWORD WINAPI OutputThread(LPVOID lpParam){ OutputDescriptor* desc = (OutputDescriptor*)lpParam; desc->parent->OutputThreadRoutine(desc); return 0; }
DWORD WINAPI InputThread(LPVOID lpParam){ CDynamicStreamer* desc = (CDynamicStreamer*)lpParam; desc->InputThreadRoutine(); return 0; }

void WINAPI TranscodingDecodeCallback(void* instance, void* item, int threadNo) { CDynamicStreamer* desc = (CDynamicStreamer*)instance; desc->TranscodingDecode(item, threadNo); }
void WINAPI TranscodingFilterCallback(void* instance, void* item, int threadNo) { CDynamicStreamer* desc = (CDynamicStreamer*)instance; desc->TranscodingFilter(item, threadNo); }
void WINAPI TranscodingEncodeCallback(void* instance, void* item, int threadNo) { CDynamicStreamer* desc = (CDynamicStreamer*)instance; desc->TranscodingEncode(item, threadNo); }
void WINAPI TranscodingCallbackCallback(void* instance, void* item, int threadNo) { CDynamicStreamer* desc = (CDynamicStreamer*)instance; desc->TranscodingCallback(item, threadNo); }

static long InstanceCounter = 0;

CStringA ConvertUnicodeToUTF8(const CStringW& uni)
{
	if (uni.IsEmpty()) return ""; // nothing to do
	CStringA utf8;
	int cc = 0;
	// get length (cc) of the new multibyte string excluding the \0 terminator first
	if ((cc = WideCharToMultiByte(CP_UTF8, 0, uni, -1, NULL, 0, 0, 0) - 1) > 0)
	{
		// convert
		char *buf = utf8.GetBuffer(cc + 1);
		if (buf)
		{
			WideCharToMultiByte(CP_UTF8, 0, uni, -1, buf, cc, 0, 0);
			buf[cc] = 0;
		}
		utf8.ReleaseBuffer();
	}
	return utf8;
}

static CDynamicStreamer* loggerInstance = NULL;

void logHandler(void*, int level, const char* msg, va_list params)
{
	if (loggerInstance && level <= FFMPEG_LOG_LEVEL)
		loggerInstance->log(TRUE, msg, params);
}


CDynamicStreamer::CDynamicStreamer()
{
	m_bMeasureReadTime = FALSE;
	m_ulStartReadOperationTime = 0;

	pDrcTarget = NULL;
	m_pCallback = NULL;
	thread = NULL;
	continue_input_thread = FALSE;

	pPendingInputParameters = NULL;
	pActiveInputParameters = NULL;

	pPendingEncoderParameters = NULL;
	pActiveEncoderParameters = NULL;

	pPendingFilterParameters = NULL;
	pActiveFilterParameters = NULL;

	pPendingDirectFrameParameters = NULL;
	pActiveDirectFrameParameters = NULL;

	pInputRuntime = NULL;
	pEncoderRuntime = NULL;
	encoderErrors = 0;
	bReinitEncoderAfterFail = FALSE;
	
	instanceId = InterlockedAdd(&InstanceCounter, 1);

	queueSize = 512;
	packetQueue.reserve(queueSize);
	for (int q = 0; q < queueSize; q++)
	{
		AVPacket p;
		av_init_packet(&p);
		p.data = NULL;
		p.size = 0;
		packetQueue.push_back(p);
	}

	outputUpdatePending = FALSE;
	inputVersion = 1;
	
	writerIdx = 0;
	queueEnd = packetQueue.size()-1;

	::InitializeConditionVariable(&queueCV);
	::InitializeCriticalSection(&allCS);

	ResetDrc();

	Measurer::SetTime(&statistics.overall.StartTime);
	Measurer::SetTime(&statistics.current.StartTime);

	loggerInstance = this;
	av_log_set_level(FFMPEG_LOG_LEVEL);
	av_log_set_callback(logHandler);

	pTranscodingDecoder = new ProcessorThread(TranscodingDecodeCallback, this, DECODERS_COUNT, 0, this, 4);
	pTranscodingEncoder = new ProcessorThread(TranscodingEncodeCallback, this, 1, 0, this, 4);
	pTranscodingFilter = new ProcessorThread(TranscodingFilterCallback, this, 1, 2, this, 8);
	pTranscodingCallback = new ProcessorThread(TranscodingCallbackCallback, this, 1, 0, this, 4);
}


void CDynamicStreamer::ResetDrc()
{
	log("Reset DRC");
	encoder_drc_ratio = 1.0;
	encoder_drc_buffer_measurements_counter = 0;
	encoder_drc_buffer_size = 0;
	encoder_drc_positive_counter = 0;
}

void CDynamicStreamer::set_error(OutputDescriptor* desc, const char* msg, int nerrorCode, const char* cline)
{
	char ffError[256];
	ffError[0] = 0;
	if (nerrorCode < 0)
		av_make_error_string(ffError, 256, nerrorCode);

	Statistics* target = desc ? &desc->statistics : &statistics;
	InterlockedAdd64(&target->current.Values[statisticTypeErrors], 1);
	
	::EnterCriticalSection(&allCS);
	InterlockedExchange(&target->lastError, nerrorCode == 0 ? 1 : nerrorCode);
	CStringA format;
	format.Format("%s|%s|%s|%d|%d", msg, ffError, cline, desc ? desc->id : -1, nerrorCode);
	target->lastErrorMessage = format;
	::LeaveCriticalSection(&allCS);

	CComBSTR bstr(format);
	if (m_pCallback)
		m_pCallback->NotifyError(nerrorCode, bstr, NULL);
}

void CDynamicStreamer::log(BOOL ffmpeg, const char* msg, va_list params)
{
	if (m_pCallback)
	{
		CStringA formatA;
		formatA.FormatV(msg, params);
		CComBSTR bstr(formatA);
		CComBSTR patter(msg);
		BSTR bpattern = NULL;
		if (ffmpeg)
			bpattern = patter;

		m_pCallback->NotifyError(0, bstr, bpattern);
	}
}


void CDynamicStreamer::log(const char* msg, ...)
{
	va_list arglist;
	
	va_start(arglist, msg);
	log(FALSE, msg, arglist);
	
	va_end(arglist);
}


void CDynamicStreamer::clear_error(OutputDescriptor* desc)
{
	Statistics* target = desc ? &desc->statistics : &statistics;
	InterlockedExchange(&target->lastError, 0);
}

BOOL CDynamicStreamer::init_filter_for_callback(OutputDescriptor* desc, FilteringContext* fctx, AVCodecContext *dec_ctx, const char *filter_spec)
{
	char args[512];
	const AVFilter *buffersrc = NULL;
	const AVFilter *buffersink = NULL;
	AVFilterInOut *outputs = avfilter_inout_alloc();
	AVFilterInOut *inputs = avfilter_inout_alloc();
	fctx->filter_graph = avfilter_graph_alloc();
	try
	{
		init_filter_options(fctx, dec_ctx);

		buffersrc = avfilter_get_by_name("buffer");
		buffersink = avfilter_get_by_name("buffersink");
		if (!buffersrc || !buffersink)
			THROW("filtering source or sink element not found");

		sprintf_s(args, sizeof(args),
			"video_size=%dx%d:pix_fmt=%d:time_base=%d/%d:pixel_aspect=%d/%d",
			dec_ctx->width,
			dec_ctx->height,
			dec_ctx->pix_fmt,
			dec_ctx->time_base.num,
			dec_ctx->time_base.den,
			dec_ctx->sample_aspect_ratio.num,
			dec_ctx->sample_aspect_ratio.den);

		CHECK(avfilter_graph_create_filter(&fctx->buffersrc_ctx, buffersrc, "in", args, NULL, fctx->filter_graph));
		CHECK(avfilter_graph_create_filter(&fctx->buffersink_ctx, buffersink, "out", NULL, NULL, fctx->filter_graph));
		AVPixelFormat pix_fmt = AV_PIX_FMT_BGR24;
		CHECK(av_opt_set_bin(fctx->buffersink_ctx, "pix_fmts", (uint8_t*)&pix_fmt, sizeof(pix_fmt), AV_OPT_SEARCH_CHILDREN));

		/* Endpoints for the filter graph. */
		outputs->name = av_strdup("in");
		outputs->filter_ctx = fctx->buffersrc_ctx;
		outputs->pad_idx = 0;
		outputs->next = NULL;

		inputs->name = av_strdup("out");
		inputs->filter_ctx = fctx->buffersink_ctx;
		inputs->pad_idx = 0;
		inputs->next = NULL;

		CHECK(avfilter_graph_parse_ptr(fctx->filter_graph, filter_spec, &inputs, &outputs, NULL));
		CHECK(avfilter_graph_config(fctx->filter_graph, NULL));
	}
	CATCH_USE
	{
		set_error(desc, e.what(), e.errorCode, e.line);
		avfilter_inout_free(&inputs);
		avfilter_inout_free(&outputs);
		return FALSE;
	}

	avfilter_inout_free(&inputs);
	avfilter_inout_free(&outputs);
	return TRUE;
}

void CDynamicStreamer::init_filter_options(FilteringContext* fctx, AVCodecContext* dec_ctx)
{
	int targetRange = (dec_ctx->color_range == AVCOL_RANGE_JPEG) ? 1 : 0;
	char args[512];
	sprintf_s(args, sizeof(args), "sws_flags=neighbor:sws_dither=none:dst_range=%d", targetRange);

	CHECK(av_opt_set(fctx->filter_graph, "scale_sws_opts", args, 0));
}

BOOL CDynamicStreamer::init_filter_for_directframe(FilteringContext* fctx, AVCodecContext *dec_ctx, AVCodecContext *enc_ctx)
{
	char args[512];
	const AVFilter *buffersrc = NULL;
	const AVFilter *buffersink = NULL;
	AVFilterInOut *outputs = avfilter_inout_alloc();
	AVFilterInOut *inputs = avfilter_inout_alloc();
	fctx->filter_graph = avfilter_graph_alloc();
	try
	{
		init_filter_options(fctx, dec_ctx);

		buffersrc = avfilter_get_by_name("buffer");
		buffersink = avfilter_get_by_name("buffersink");
		if (!buffersrc || !buffersink)
			THROW("filtering source or sink element not found");

		sprintf_s(args, sizeof(args),
			"video_size=%dx%d:pix_fmt=%d:time_base=%d/%d:pixel_aspect=%d/%d",
			pInputRuntime->Width,
			pInputRuntime->Height,
			enc_ctx->pix_fmt,
			dec_ctx->time_base.num,
			dec_ctx->time_base.den,
			dec_ctx->sample_aspect_ratio.num,
			dec_ctx->sample_aspect_ratio.den);

		CHECK(avfilter_graph_create_filter(&fctx->buffersrc_ctx, buffersrc, "in", args, NULL, fctx->filter_graph));
		CHECK(avfilter_graph_create_filter(&fctx->buffersink_ctx, buffersink, "out", NULL, NULL, fctx->filter_graph));
		AVPixelFormat pix_fmt = AV_PIX_FMT_BGR24;
		CHECK(av_opt_set_bin(fctx->buffersink_ctx, "pix_fmts", (uint8_t*)&pix_fmt, sizeof(pix_fmt), AV_OPT_SEARCH_CHILDREN));
		/* Endpoints for the filter graph. */
		outputs->name = av_strdup("in");
		outputs->filter_ctx = fctx->buffersrc_ctx;
		outputs->pad_idx = 0;
		outputs->next = NULL;

		inputs->name = av_strdup("out");
		inputs->filter_ctx = fctx->buffersink_ctx;
		inputs->pad_idx = 0;
		inputs->next = NULL;

		char filterSpec[512];
		int maxHeight = 720;
		if (dec_ctx->height > maxHeight)
		{
			// downscale & alignment
			int width = (dec_ctx->width * maxHeight) / dec_ctx->height;
			int widthAligned = (width / 32) * 32;
			sprintf_s(filterSpec, sizeof(filterSpec), "scale=w=%d:h=%d, fps=24", widthAligned, maxHeight);
		}
		else if (dec_ctx->width % 32 != 0)
		{
			// make alignment
			sprintf_s(filterSpec, sizeof(filterSpec), "scale=w=%d:h=%d, fps=24", (dec_ctx->width / 32) * 32, dec_ctx->height);
		}
		else
			sprintf_s(filterSpec, sizeof(filterSpec), "fps=24");

		CHECK(avfilter_graph_parse_ptr(fctx->filter_graph, filterSpec, &inputs, &outputs, NULL));
		CHECK(avfilter_graph_config(fctx->filter_graph, NULL));
	}
	CATCH_USE
	{
		set_error(NULL, e.what(), e.errorCode, e.line);
		avfilter_inout_free(&inputs);
		avfilter_inout_free(&outputs);
		return FALSE;
	}

	avfilter_inout_free(&inputs);
	avfilter_inout_free(&outputs);
	return TRUE;
}

void CDynamicStreamer::init_filter(FilteringContext* fctx, AVCodecContext* dec_ctx, AVCodecContext* enc_ctx, const char* filter_spec)
{
	char args[512];
	const AVFilter *buffersrc = NULL;
	const AVFilter *buffersink = NULL;
	AVFilterInOut *outputs = avfilter_inout_alloc();
	AVFilterInOut *inputs = avfilter_inout_alloc();
	fctx->filter_graph = avfilter_graph_alloc();
	try
	{
		if (dec_ctx->codec_type == AVMEDIA_TYPE_VIDEO)
		{
			init_filter_options(fctx, dec_ctx);

			buffersrc = avfilter_get_by_name("buffer");
			buffersink = avfilter_get_by_name("buffersink");
			if (!buffersrc || !buffersink)
				THROW("filtering source or sink element not found");

			int px = dec_ctx->pix_fmt;
			if (px == AV_PIX_FMT_YUVJ422P)
				px = AV_PIX_FMT_YUV422P;

			sprintf_s(args, sizeof(args),
				"video_size=%dx%d:pix_fmt=%d:time_base=%d/%d:pixel_aspect=%d/%d",
				dec_ctx->width,
				dec_ctx->height,
				px,
				dec_ctx->time_base.num,
				dec_ctx->time_base.den,
				dec_ctx->sample_aspect_ratio.num,
				dec_ctx->sample_aspect_ratio.den);

			CHECK(avfilter_graph_create_filter(&fctx->buffersrc_ctx, buffersrc, "in", args, NULL, fctx->filter_graph));
			CHECK(avfilter_graph_create_filter(&fctx->buffersink_ctx, buffersink, "out", NULL, NULL, fctx->filter_graph));
			CHECK(av_opt_set_bin(fctx->buffersink_ctx, "pix_fmts", (uint8_t*)&enc_ctx->pix_fmt/*comeback px*/, sizeof(enc_ctx->pix_fmt), AV_OPT_SEARCH_CHILDREN));
		}
		else if (dec_ctx->codec_type == AVMEDIA_TYPE_AUDIO)
		{
			buffersrc = avfilter_get_by_name("abuffer");
			buffersink = avfilter_get_by_name("abuffersink");
			if (!buffersrc || !buffersink)
				THROW("filtering source or sink element not found");

			sprintf_s(args, sizeof(args),
				"time_base=%d/%d:sample_rate=%d:sample_fmt=%s:channel_layout=0x%" PRIx64,
				dec_ctx->time_base.num,
				dec_ctx->time_base.den,
				dec_ctx->sample_rate,
				av_get_sample_fmt_name(dec_ctx->sample_fmt),
				dec_ctx->channel_layout);

			CHECK(avfilter_graph_create_filter(&fctx->buffersrc_ctx, buffersrc, "in", args, NULL, fctx->filter_graph));
			CHECK(avfilter_graph_create_filter(&fctx->buffersink_ctx, buffersink, "out", NULL, NULL, fctx->filter_graph));
			CHECK(av_opt_set_bin(fctx->buffersink_ctx, "sample_fmts", (uint8_t*)&enc_ctx->sample_fmt, sizeof(enc_ctx->sample_fmt), AV_OPT_SEARCH_CHILDREN));
			CHECK(av_opt_set_bin(fctx->buffersink_ctx, "channel_layouts", (uint8_t*)&enc_ctx->channel_layout, sizeof(enc_ctx->channel_layout), AV_OPT_SEARCH_CHILDREN));
			CHECK(av_opt_set_bin(fctx->buffersink_ctx, "sample_rates", (uint8_t*)&enc_ctx->sample_rate, sizeof(enc_ctx->sample_rate), AV_OPT_SEARCH_CHILDREN));
		}

		/* Endpoints for the filter graph. */
		outputs->name = av_strdup("in");
		outputs->filter_ctx = fctx->buffersrc_ctx;
		outputs->pad_idx = 0;
		outputs->next = NULL;

		inputs->name = av_strdup("out");
		inputs->filter_ctx = fctx->buffersink_ctx;
		inputs->pad_idx = 0;
		inputs->next = NULL;

		CHECK(avfilter_graph_parse_ptr(fctx->filter_graph, filter_spec, &inputs, &outputs, NULL));
		CHECK(avfilter_graph_config(fctx->filter_graph, NULL));

		if (enc_ctx->codec->type == AVMEDIA_TYPE_AUDIO && !(enc_ctx->codec->capabilities & AV_CODEC_CAP_VARIABLE_FRAME_SIZE))
			av_buffersink_set_frame_size(fctx->buffersink_ctx, enc_ctx->frame_size);
	}
	CATCH_USE
	{
		avfilter_inout_free(&inputs);
		avfilter_inout_free(&outputs);

		set_error(NULL, e.what(), e.errorCode, e.line);
		RETHROW;
	}

	avfilter_inout_free(&inputs);
	avfilter_inout_free(&outputs);
}

void CDynamicStreamer::open_encoder_context(CStringA& codecName, CStringA& options, AVCodecContext** c, AVCodecContext* input_decoder)
{
	AVCodec* codec = avcodec_find_encoder_by_name(codecName);
	if (!codec)
		THROW("Codec not found");

	*c = avcodec_alloc_context3(codec);
	if (!*c)
		THROW("Could not allocate video codec context");

	AVCodecContext* ctx = *c;

	log("Open enc %s", (LPCSTR)codecName);

	if (ctx->codec_type == AVMEDIA_TYPE_VIDEO)
	{
		AVCodecContext* dec_ctx = input_decoder;
		if (pInputRuntime->Width != 0)
		{
			ctx->height = pInputRuntime->Height;
			ctx->width = pInputRuntime->Width;
		}
		else
		{
			ctx->height = dec_ctx->height;
			ctx->width = dec_ctx->width;
		}
		ctx->sample_aspect_ratio = dec_ctx->sample_aspect_ratio;

		/*
		Chaturbate (and maybe stripchat) does not support 4:2:2 -> diabling optimization
		int pixelIndex = 0;
		while (codec->pix_fmts[pixelIndex] != AV_PIX_FMT_NONE && 
			codec->pix_fmts[pixelIndex] != dec_ctx->pix_fmt)
		{
			pixelIndex++;
		}

		if (codec->pix_fmts[pixelIndex] != AV_PIX_FMT_NONE)
		{
			ctx->pix_fmt = codec->pix_fmts[pixelIndex];
			log("Pixel format (%d) ready for filter bypassing", ctx->pix_fmt);
		}
		else*/
		ctx->pix_fmt = codec->pix_fmts[0];

		//ctx->colorspace = AVCOL_SPC_BT709;
		//ctx->time_base.num = 1;
		//ctx->time_base.den = 24;
		if (pInputRuntime->Fps == 0)
		{
			ctx->time_base = av_inv_q(dec_ctx->framerate);
		}
		else
		{
			ctx->time_base.num = 1;
			ctx->time_base.den = pInputRuntime->Fps;
		}
	}
	else
	{
		AVCodecContext* dec_ctx = input_decoder;
		ctx->sample_rate = dec_ctx->sample_rate;
		ctx->channel_layout = dec_ctx->channel_layout;
		ctx->channels = av_get_channel_layout_nb_channels(ctx->channel_layout);
		ctx->sample_fmt = codec->sample_fmts[0]; /* take first format from list of supported formats */
		int i = 1;
		while (codec->sample_fmts[i] != AV_SAMPLE_FMT_NONE) // try to find 16 bits sample format
		{
			int bytes = av_get_bytes_per_sample(codec->sample_fmts[i]);
			if (bytes == 2)
			{
				ctx->sample_fmt = codec->sample_fmts[i];
				break;
			}
			i++;
		}

		ctx->time_base.num = 1;
		ctx->time_base.den = ctx->sample_rate;
	}
	ctx->flags |= AV_CODEC_FLAG_GLOBAL_HEADER;


	AVDictionary* dict = NULL;
	if (options.GetLength() > 0)
		parse_dictionary(&dict, options);

	CHECK(avcodec_open2(*c, codec, &dict));

	if (dict != NULL)
		THROW("Options are not accepted");
}

HRESULT CDynamicStreamer::open_output(OutputDescriptor* desc)
{
	if (desc->pActualParameters == NULL)
		return E_FAIL;

	desc->baseTime = 0;
	try
	{
		if (desc->pCallback == NULL)
		{
			CHECK(avformat_alloc_output_context2(&desc->output_ctx, NULL, desc->type, desc->output));
			AVOutputFormat* ofmt = desc->output_ctx->oformat;
			for (int i = 0; i < desc->pActualParameters->stream_info.count; i++)
			{
				AVStream* out_stream = avformat_new_stream(desc->output_ctx, NULL);
				if (out_stream == NULL)
					THROW("Failed allocating output stream");

				CHECK(avcodec_parameters_copy(out_stream->codecpar, desc->pActualParameters->perStream[i].codecpar));
				out_stream->time_base = desc->pActualParameters->perStream[i].timeBase;

				/*CHECK(avcodec_copy_context(out_stream->codec, codecCtx));
				out_stream->time_base = codecCtx->time_base;
				out_stream->codec->framerate = in_stream->r_frame_rate;
				out_stream->avg_frame_rate = in_stream->r_frame_rate;
				out_stream->r_frame_rate = in_stream->r_frame_rate;*/

				/*if (encoder_ctx == NULL || encoder_ctx[i] == NULL)
				{
					if (codecCtx->extradata_size) {
						out_stream->codecpar->extradata = (uint8_t*)av_mallocz(codecCtx->extradata_size + AV_INPUT_BUFFER_PADDING_SIZE);
						if (out_stream->codecpar->extradata) {
							memcpy(out_stream->codecpar->extradata, codecCtx->extradata, codecCtx->extradata_size);
							out_stream->codecpar->extradata_size = codecCtx->extradata_size;
						}
					}
				}*/
			}

			AVDictionary* dict = NULL;
			if (desc->options)
				parse_dictionary(&dict, ConvertUnicodeToUTF8(CStringW(desc->options)));

			log("Opening out %s", (LPCSTR)desc->output);

			if (!(ofmt->flags & AVFMT_NOFILE))
				CHECK(avio_open2(&desc->output_ctx->pb, desc->output, AVIO_FLAG_WRITE, NULL, &dict));
		
			if (dict != NULL)
				THROW("Options are not accepted");

			avformat_write_header(desc->output_ctx, NULL);
		}
		else
		{
			desc->callback_decoder_ctx = new AVCodecContext*[desc->pActualParameters->stream_info.count];
			for (int q = 0; q < desc->pActualParameters->stream_info.count; q++)desc->callback_decoder_ctx[q] = NULL;

			int vid = desc->pActualParameters->stream_info.video_idx;
			OutputParametersPerStream* str = &desc->pActualParameters->perStream[vid];

			AVCodecParameters* codecpar = str->codecpar;
			AVCodec* codec = avcodec_find_decoder(codecpar->codec_id);
			if (codec == NULL)
				THROW("Codec for callback not found");
			AVCodecContext* dec = avcodec_alloc_context3(codec);

			dec->height = codecpar->height;
			dec->width = codecpar->width;
			
			int esz = 0;
			uint8_t* e = NULL;

			if (str->extradata_size) 
			{
				esz = str->extradata_size;
				e = str->extradata;
			}
			else if (codecpar->extradata_size)
			{
				esz = codecpar->extradata_size;
				e = codecpar->extradata;
			}

			if (esz)
			{
				dec->extradata = (uint8_t*)av_mallocz(esz + AV_INPUT_BUFFER_PADDING_SIZE);
				memcpy(dec->extradata, e, esz);
				dec->extradata_size = esz;
			}
			log("Opening out callback");
			CHECK(avcodec_open2(dec, codec, NULL));

			desc->callback_decoder_ctx[vid] = dec;
		}
	}
	CATCH_USE
	{
		desc->CloseOutput();
		set_error(desc, e.what(), e.errorCode, e.line);
		return E_FAIL;
	}
	desc->outputInitialized = TRUE;
	return S_OK;
}

void CDynamicStreamer::set_thread_name(int id)
{
	CStringA out;
	out.Format("DynamicStreamer%d (%d) ", instanceId, id);
	SetThreadName(-1, out);

}

int CDynamicStreamer::decode_packet_for_callback(AVPacket* packet, OutputDescriptor* desc)
{
	int stream = packet->stream_index;
	dec_func decoder = avcodec_decode_video2;
	AVCodecContext* decCtx = desc->callback_decoder_ctx[stream];
	int got_frame;

	int decode_ret = decoder(decCtx, desc->frame, &got_frame, packet);

	if (decode_ret < 0)
		set_error(desc, "Out. decoding error", decode_ret, LOCATION);
	else
		clear_error(desc);

	if (got_frame)
	{
		if (desc->callback_filter == NULL)
		{
			desc->callback_filter = new FilterRuntime(desc->pActualParameters->stream_info);
			if (!init_filter_for_callback(desc, &desc->callback_filter->filter_ctx[stream], decCtx, "null"))
			{
				delete desc->callback_filter;
				desc->callback_filter = NULL;
				return -1;
			}
		}
		desc->frame->pict_type = AV_PICTURE_TYPE_NONE;

		FilteringContext* fctx = &desc->callback_filter->filter_ctx[stream];

		int ret_flags = av_buffersrc_add_frame_flags(fctx->buffersrc_ctx, desc->frame, 0);
		if (ret_flags >= 0)
		{
			while (1)
			{
				int ret = av_buffersink_get_frame(fctx->buffersink_ctx, desc->filter_frame);
				if (ret < 0)
				{
					if (ret == AVERROR(EAGAIN) || ret == AVERROR_EOF) ret = 0;
					else set_error(desc, "Out. Filtering error", ret, LOCATION);
					av_frame_unref(desc->filter_frame);
					break;
				}
				desc->filter_frame->pict_type = AV_PICTURE_TYPE_NONE;

				int size = desc->filter_frame->width * desc->filter_frame->height * 3;
				byte* data = (byte*)desc->filter_frame->data[0];

				desc->pCallback->NotifyFrame(desc->filter_frame->width, desc->filter_frame->height, size, (__int64)(void*)data);

				av_frame_unref(desc->filter_frame);
			}
		}
		else set_error(desc, "Out. Filter Feeding error", ret_flags, LOCATION);
	}
	return decode_ret;

}


void CDynamicStreamer::Enque(AVPacket& pkt)
{
	::EnterCriticalSection(&allCS);

	av_packet_unref(&packetQueue[writerIdx]);
	av_copy_packet(&packetQueue[writerIdx], &pkt);

	writerIdx = (writerIdx + 1) % queueSize;

	

	if (writerIdx == queueEnd)
	{
		int newEnd = (queueEnd + queueSize / 8) % queueSize;

		for (int i = 0; i < outputs.size(); i++)
		{
			OutputDescriptor* desc = outputs[i];
			if (desc != NULL)
			{
				int dropCount = 0;
				if (newEnd > queueEnd)
				{
					if (desc->readerIdx >= queueEnd && desc->readerIdx < newEnd) dropCount = newEnd - desc->readerIdx;
				}
				else
				{
					if (desc->readerIdx < newEnd) dropCount = newEnd - desc->readerIdx;
					else if (desc->readerIdx >= queueEnd) dropCount = queueSize - desc->readerIdx + newEnd;
				}

				if (dropCount)
				{
					desc->readerIdx = newEnd;
					InterlockedAdd64(&desc->statistics.current.Values[statisticTypeDropped], dropCount);
				}
			}
		}

		queueEnd = newEnd;
	}

	WakeAllConditionVariable(&queueCV);

	::LeaveCriticalSection(&allCS);
}

void CDynamicStreamer::AddReaderToQueue(OutputDescriptor* desc)
{
	::EnterCriticalSection(&allCS);

	desc->readerIdx = (writerIdx + queueSize - 1) % queueSize;

	::LeaveCriticalSection(&allCS);
}

void CDynamicStreamer::Deque(AVPacket& pkt, OutputDescriptor* desc)
{
	::EnterCriticalSection(&allCS);

	while ((desc->readerIdx + 1) % queueSize == writerIdx && desc->continue_thread)
		SleepConditionVariableCS(&queueCV, &allCS, 1000);

	if (desc->continue_thread)
	{
		desc->readerIdx = (desc->readerIdx + 1) % queueSize;
		av_copy_packet(&pkt, &packetQueue[desc->readerIdx]);
	}
	::LeaveCriticalSection(&allCS);
}

BOOL CDynamicStreamer::NeedsReinitEncoderDrc(int& audioBitrate, int& videoBitrate)
{
	BOOL bResult = FALSE;

	// dynamic bitrate control
	if (pEncoderRuntime != NULL && pDrcTarget != NULL && pDrcTarget->pActualParameters != NULL) //drc target initialized
	{
		if (pDrcTarget->justReinitialized)
		{
			log("reset DRC after reinit");
			pDrcTarget->justReinitialized = FALSE;
			ResetDrc();

			audioBitrate = pActiveEncoderParameters->encoder_audioMaxBitrate;
			videoBitrate = pActiveEncoderParameters->encoder_videoMaxBitrate;
			return TRUE;
		}

		int readerIdx = pDrcTarget->readerIdx;
		int buffer = writerIdx > readerIdx ? writerIdx - readerIdx : writerIdx + queueSize - readerIdx;
		encoder_drc_buffer_measurements_counter++;

		// ATLTRACE("Buffer = %d\r\n", buffer);

		if (encoder_drc_buffer_measurements_counter > 80)
		{
			encoder_drc_buffer_measurements_counter = 0;

			int delta = buffer - encoder_drc_buffer_size;
			encoder_drc_buffer_size = buffer;
			double ratio = -1;

			if (delta > 3 && buffer > 45 || buffer > 90)
			{
				encoder_drc_positive_counter = 0;
				if (delta < 20 && buffer < 120 || delta < 0)
				{
					ratio = encoder_drc_ratio * .95;
					log("--- slow down %f (d%d, b%d)", ratio, delta, buffer);
				}
				else
				{
					ratio = encoder_drc_ratio * .85;
					log("------ fast down %f (d%d, b%d)", ratio, delta, buffer);
				}
			}

			if (delta < 5 || buffer < 15)
			{
				encoder_drc_positive_counter++;
				if (encoder_drc_positive_counter > 2)
				{
					encoder_drc_positive_counter = 0;

					if (encoder_drc_ratio < 1.0)
					{
						ratio = encoder_drc_ratio * 1.07;
						if (ratio > 1.0)
							ratio = 1.0;
						log("+++ up %f (d%d, b%d)", ratio, delta, buffer);

					}
				}
			}

			if (ratio > 0.15 && ratio <= 1.0)
			{
				encoder_drc_ratio = ratio;
				int videoBR = (int)(ratio * pActiveEncoderParameters->encoder_videoMaxBitrate);
				int audioBR = (int)(ratio * pActiveEncoderParameters->encoder_audioMaxBitrate);

				audioBitrate = audioBR;
				videoBitrate = videoBR;
				bResult = TRUE;
			}
		}
	}
	return bResult;
}

void CDynamicStreamer::NeedsReinit(BOOL& reinitInput, BOOL& reinitEncoder, BOOL& reinitFilter, BOOL& reinitDirectFrame)
{
	CriticalSectionLock lock(allCS);
	if (pPendingInputParameters)
	{
		if (pActiveInputParameters)
			delete pActiveInputParameters;
		pActiveInputParameters = pPendingInputParameters;
		pPendingInputParameters = NULL;
		reinitInput = TRUE;
	}

	if (pPendingEncoderParameters)
	{
		if (pActiveEncoderParameters)
			delete pActiveEncoderParameters;
		pActiveEncoderParameters = pPendingEncoderParameters;
		pPendingEncoderParameters = NULL;
		encoderErrors = 0;
		reinitEncoder = TRUE;
	}

	if (pPendingFilterParameters)
	{
		if (pActiveFilterParameters)
			delete pActiveFilterParameters;
		pActiveFilterParameters = pPendingFilterParameters;
		pPendingFilterParameters = NULL;
		reinitFilter = TRUE;
	}

	if (pPendingDirectFrameParameters)
	{
		if (pActiveDirectFrameParameters)
			delete pActiveDirectFrameParameters;
		pActiveDirectFrameParameters = pPendingDirectFrameParameters;
		pPendingDirectFrameParameters = NULL;
		reinitDirectFrame = TRUE;
	}

	if (outputUpdatePending)
	{
		outputUpdatePending = FALSE;
		UpdateOutputParameters(FALSE);
	}
}

void CDynamicStreamer::ReinitInput()
{
	log("Reinit input");
	StopPipeline();

	int fps = 0;
	int width = 0;
	int height = 0;
	if (pInputRuntime)
	{
		fps = pInputRuntime->Fps;
		width = pInputRuntime->Width;
		height = pInputRuntime->Height;
		log("Stopping input");
		delete pInputRuntime;
		log("Stopped input");
		pInputRuntime = NULL;
	}

	if (pActiveInputParameters)
	{
		if (!pActiveInputParameters->input_input.IsEmpty())
		{
			BOOL paramsChanged = fps != pActiveInputParameters->Fps || width != pActiveInputParameters->Width || height != pActiveInputParameters->Height;

			pInputRuntime = new InputRuntime();
			if (!InitInputRuntime())
			{
				delete pInputRuntime;
				pInputRuntime = NULL;
			}
			else
			{
				if (paramsChanged)
					inputVersion++;
				ReinitEncoder(NULL, NULL);
			}
		}
	}

	UpdateOutputParameters(FALSE);
}

void CDynamicStreamer::ReinitEncoder(int* audioBitrate, int* videoBitrate)
{
	log("Reinit enc");
	StopPipeline();
	bReinitEncoderAfterFail = FALSE;

	if (pEncoderRuntime)
	{
		delete pEncoderRuntime;
		pEncoderRuntime = NULL;
	}

	if (audioBitrate == NULL && videoBitrate == NULL)
	{
		ResetDrc();
	}

	if (pActiveEncoderParameters && pInputRuntime)
	{
		int a = pActiveEncoderParameters->encoder_audioMaxBitrate;
		int v = pActiveEncoderParameters->encoder_videoMaxBitrate;

		if (audioBitrate)
			a = *audioBitrate;

		if (videoBitrate)
			v = *videoBitrate;

		log("Reinit enc (%d, %d)", v, a);
		pEncoderRuntime = new EncoderRuntime(*pInputRuntime->stream_info);
		if (!InitEncoderRuntime(a, v))
		{
			encoderErrors = MAX_ENCODER_ERRORS;
			log("Reinit enc (fallback)");
			if (!InitEncoderRuntime(a, v))
			{
				delete pEncoderRuntime;
				pEncoderRuntime = NULL;
			}
		}
		
		InitFilter();
	}

	UpdateOutputParameters(TRUE);
}

static int check_interrupt(void* t)
{
	return t && static_cast<CDynamicStreamer*>(t)->is_read_timeout();
}


int CDynamicStreamer::is_read_timeout()
{
	if (m_bMeasureReadTime)
	{
		FILETIME time;
		GetSystemTimeAsFileTime(&time);
		ULONGLONG currentTime = *(ULONGLONG*)&time; 
		if (currentTime - m_ulStartReadOperationTime > 10*10000000) //10 sec
			return 1;
	}
	return 0;
}

void CDynamicStreamer::ResetStartOperationTime() 
{
	FILETIME time;
	GetSystemTimeAsFileTime(&time);
	m_ulStartReadOperationTime = *(ULONGLONG*)&time;
	m_bMeasureReadTime = TRUE;
}

BOOL CDynamicStreamer::InitInputRuntime()
{
	try
	{
		log("Input opening");

		pInputRuntime->Fps = pActiveInputParameters->Fps;
		pInputRuntime->Width = pActiveInputParameters->Width;
		pInputRuntime->Height = pActiveInputParameters->Height;

		AVInputFormat* format = av_find_input_format(pActiveInputParameters->input_type);
		if (format == NULL)
			THROW("Unable to find input format");

		AVDictionary* dict = NULL;
		parse_dictionary(&dict, pActiveInputParameters->input_options);
		AVDictionaryEntry* codec = av_dict_get(dict, "vcodec", NULL, 0);
		pInputRuntime->input_fmt_ctx = avformat_alloc_context();
		pInputRuntime->input_fmt_ctx->interrupt_callback.callback = &check_interrupt;
		pInputRuntime->input_fmt_ctx->interrupt_callback.opaque = this;

		if (codec != NULL)
		{
			AVCodec* found = avcodec_find_decoder_by_name(codec->value);
			if (found != NULL)
				pInputRuntime->input_fmt_ctx->video_codec_id = found->id;
			av_dict_set(&dict, "vcodec", NULL, 0);
		}

		CHECK(avformat_open_input(&pInputRuntime->input_fmt_ctx, pActiveInputParameters->input_input, format, &dict));

		log("Input opened");

		if (dict != NULL)
			THROW("input options are not accepted");

		pInputRuntime->input_fmt_ctx->max_analyze_duration = max_analyze_duration;
		CHECK(avformat_find_stream_info(pInputRuntime->input_fmt_ctx, NULL));

		if (!IsInputParametersInitialized())
		{
			if (max_analyze_duration < 1000000 * 10)
				max_analyze_duration *= 2;

			THROW("not all parameters initialized");
		}

		log("Input stream info found");

		int streams = pInputRuntime->input_fmt_ctx->nb_streams;
		if (streams > 2) streams = 2;
		pInputRuntime->stream_info = new StreamInfo(streams, -1, -1);

		pInputRuntime->input_time_base = new AVRational[streams];
		pInputRuntime->decoder_ctx = new AVCodecContext*[streams * DECODERS_COUNT];
		for (int q = 0; q < streams * DECODERS_COUNT; q++)
			pInputRuntime->decoder_ctx[q] = NULL;

		pInputRuntime->stream_info->video_idx = open_decoder_context(pInputRuntime, AVMEDIA_TYPE_VIDEO, streams);
		pInputRuntime->stream_info->audio_idx = open_decoder_context(pInputRuntime, AVMEDIA_TYPE_AUDIO, streams);

		if (pInputRuntime->stream_info->video_idx < 0 && pInputRuntime->stream_info->audio_idx < 0)
			THROW("Could not find audio or video stream in the input, aborting");
	}
	CATCH_USE
	{
		set_error(NULL, e.what(), e.errorCode, e.line);
		return FALSE;
	}

	return TRUE;
}

BOOL CDynamicStreamer::IsInputParametersInitialized()
{
	if (pInputRuntime->input_fmt_ctx->nb_streams != 2)
	{
		log("wrong number of streams (%d)", pInputRuntime->input_fmt_ctx->nb_streams);
		return FALSE;
	}

	if (pInputRuntime->input_fmt_ctx->streams == NULL || pInputRuntime->input_fmt_ctx->streams[0] == NULL || pInputRuntime->input_fmt_ctx->streams[1] == NULL ||
		pInputRuntime->input_fmt_ctx->streams[0]->codec == NULL || pInputRuntime->input_fmt_ctx->streams[1]->codec == NULL)
	{
		log("wrong streams");
		return FALSE;
	}
	if (pInputRuntime->input_fmt_ctx->streams[0]->codec->codec_type == AVMEDIA_TYPE_VIDEO)
	{
		if (pInputRuntime->input_fmt_ctx->streams[0]->codecpar->height == 0)
		{
			log("size not initialized 1");
			return FALSE;
		}
		if (pInputRuntime->input_fmt_ctx->streams[1]->codecpar->sample_rate <= 0)
		{
			log("rate not initialized 1");
			return FALSE;
		}
	}
	else
	{
		if (pInputRuntime->input_fmt_ctx->streams[1]->codecpar->height == 0)
		{
			log("size not initialized 2");
			return FALSE;
		}
		if (pInputRuntime->input_fmt_ctx->streams[0]->codecpar->sample_rate <= 0)
		{
			log("rate not initialized 2");
			return FALSE;
		}
	}
	return TRUE;
}

BOOL CDynamicStreamer::InitEncoderRuntime(int audioBitrate, int videoBitrate)
{
	try
	{
		for (int i = 0; i < pEncoderRuntime->stream_info.count; i++)
		{
			if (i == pEncoderRuntime->stream_info.video_idx)
			{
				CStringA codec = pActiveEncoderParameters->encoder_videoCodec;
				CStringA options = pActiveEncoderParameters->encoder_videoOptions;
				if (encoderErrors >= MAX_ENCODER_ERRORS && pActiveEncoderParameters->encoder_fallback_videoCodec.GetLength() > 0)
				{
					codec = pActiveEncoderParameters->encoder_fallback_videoCodec;
					options = pActiveEncoderParameters->encoder_fallback_videoOptions;
				}

				open_encoder_context(codec, 
					get_encoder_options_video(options, videoBitrate),
										&pEncoderRuntime->encoder_ctx[i],
										pInputRuntime->decoder_ctx[i]);
			}
			else if (i == pEncoderRuntime->stream_info.audio_idx)
			{
				open_encoder_context(pActiveEncoderParameters->encoder_audioCodec, 
									 get_encoder_options_audio(pActiveEncoderParameters->encoder_audioOptions, audioBitrate), 
									 &pEncoderRuntime->encoder_ctx[i],
									 pInputRuntime->decoder_ctx[i]);
			}
		}
	}
	CATCH_USE
	{
		set_error(NULL, e.what(), e.errorCode, e.line);
		return FALSE;
	}
	return TRUE;
}

void CDynamicStreamer::UpdateOutputParameters(BOOL bOnlyIfTransmissionNotStarted)
{
	CriticalSectionLock lock(allCS);

	if (pInputRuntime)
	{
		if (!pEncoderRuntime && (pPendingEncoderParameters || pActiveEncoderParameters))
		{
			// encoder is going to be reinitialized
			return;
		}

		if (bOnlyIfTransmissionNotStarted)
		{
			BOOL bStarted = FALSE;
			for (size_t i = 0; i < outputs.size(); i++)
			{
				OutputDescriptor* desc = outputs[i];
				if (desc != NULL && desc->pCallback == NULL && desc->pActualParameters)
				{
					bStarted = TRUE;
				}
			}

			if (!bStarted)
			{
				inputVersion++;
			}
			else
				return;
		}

		for (size_t i = 0; i < outputs.size(); i++)
		{
			OutputDescriptor* desc = outputs[i];
			if (desc != NULL)
			{
				if (desc->inputVersion != inputVersion)
				{
					if (desc->pPendingParameters)
						desc->pPendingParameters = NULL;

					UpdateOutputParameters(desc);
				}
			}
		}
	}
}

void CDynamicStreamer::UpdateDrcTarget()
{
	OutputDescriptor* result = NULL;

	CriticalSectionLock lock(allCS);
	for (size_t i = 0; i < outputs.size(); i++)
	{
		OutputDescriptor* desc = outputs[i];
		if (desc != NULL && desc->pCallback == NULL && desc->output.Find('\\', 0) < 0)
		{
			result = desc;
			break;
		}
	}
	ResetDrc();

	pDrcTarget = result;
}

void CDynamicStreamer::UpdateOutputParameters(OutputDescriptor* desc)
{
	log("UpdateOutputParameters(%d)", desc->id);
	desc->inputVersion = inputVersion;
	desc->pPendingParameters = new OutputParameters(*pInputRuntime->stream_info);
	if (desc->pCallback)
	{
		int vid = pInputRuntime->stream_info->video_idx;
		OutputParametersPerStream* str = &desc->pPendingParameters->perStream[vid];
		str->codecpar = avcodec_parameters_alloc();
		if (pEncoderRuntime)
		{
			AVCodecContext* ctx = pEncoderRuntime->encoder_ctx[vid];
			avcodec_parameters_from_context(str->codecpar, ctx);
			if (ctx->extradata_size) 
			{
				str->extradata = new uint8_t[ctx->extradata_size];// +AV_INPUT_BUFFER_PADDING_SIZE);
				memcpy(str->extradata, ctx->extradata, ctx->extradata_size);
				str->extradata_size = ctx->extradata_size;
			}
		}
		else
		{
			avcodec_parameters_from_context(str->codecpar, pInputRuntime->decoder_ctx[vid]);
		}
	}
	else
	{
		for (int q = 0; q < pInputRuntime->stream_info->count; q++)
		{
			OutputParametersPerStream* str = &desc->pPendingParameters->perStream[q];
			str->codecpar = avcodec_parameters_alloc();
			if (pEncoderRuntime)
			{
				avcodec_parameters_from_context(str->codecpar, pEncoderRuntime->encoder_ctx[q]);
				str->timeBase = pEncoderRuntime->encoder_ctx[q]->time_base;
				str->input_time_base = str->timeBase;
			}
			else
			{
				avcodec_parameters_from_context(str->codecpar, pInputRuntime->decoder_ctx[q]);
				str->timeBase = pInputRuntime->decoder_ctx[q]->time_base;
				str->input_time_base = pInputRuntime->input_time_base[q];
			}
		}
	}
}



void CDynamicStreamer::InputThreadRoutine()
{
	set_thread_name(-1);

	Measurer::SetTime(&statistics.overall.StartTime);
	Measurer::SetTime(&statistics.current.StartTime);

	AVPacket pkt;
	av_init_packet(&pkt);
	pkt.data = NULL;
	pkt.size = 0;

	int errorCounter = 0;

	while (continue_input_thread)
	{
		BOOL reinitInput = FALSE;
		BOOL reinitEncoder = FALSE;
		BOOL reinitFilter = FALSE;
		BOOL reinitDirectFrame = FALSE;

		NeedsReinit(reinitInput, reinitEncoder, reinitFilter, reinitDirectFrame);

		if (bReinitEncoderAfterFail)
		{
			encoderErrors++;
		}

		int* pAudio = NULL;
		int* pVideo = NULL;
		int audio;
		int video;

		if (!reinitInput && !reinitEncoder)
		{
			if (NeedsReinitEncoderDrc(audio, video))
			{
				reinitEncoder = TRUE;
				pAudio = &audio;
				pVideo = &video;
			}
		}


		if (reinitInput)
			ReinitInput();
		else if (reinitEncoder || bReinitEncoderAfterFail)
			ReinitEncoder(pAudio, pVideo);
		else if (reinitFilter)
			InitFilter();
		else if (reinitDirectFrame)
			ReinitDirectFrame();
		
		if (!pInputRuntime)
		{
			if (pActiveInputParameters != NULL && !pActiveInputParameters->input_input.IsEmpty())
			{
				ReinitInput();
				if (!pInputRuntime)
				{
					Sleep(50);
					continue;
				}
			}
			else
			{
				Sleep(50);
				continue;
			}
		}

		StartPipeline();

		ResetStartOperationTime();

		int ret = av_read_frame(pInputRuntime->input_fmt_ctx, &pkt);
		if (ret < 0 && is_read_timeout())
		{
			errorCounter = 10;
		}
		m_bMeasureReadTime = FALSE;

		//log("packet %d %lld", pkt.stream_index, pkt.dts);

		TRACE_TIME((&pkt), "INPUT");

		if (ret < 0)
		{
			//av_packet_unref(&pkt);
			set_error(NULL, "error reading frame", ret, LOCATION);

			errorCounter++;
			if (errorCounter > 10)
			{
				Sleep(50);
				ReinitInput();
				errorCounter = 0;
			}
			continue;
		}
		else 
			clear_error(NULL);


		int streamIndex = pkt.stream_index;

		// OBS Audio provides time with 1 - 1.5 seconds ahead, so we slightly adjust it if possible
		if (streamIndex == pInputRuntime->stream_info->video_idx)
			pInputRuntime->lastVideoPacket = pkt.dts;
		else
			pInputRuntime->lastAudioPacket = pkt.dts;

		if (pEncoderRuntime && pInputRuntime->lastVideoPacket && pInputRuntime->lastAudioPacket)
		{
			int secondBase = 10000000;

			AVRational videoBase = pInputRuntime->input_time_base[pInputRuntime->stream_info->video_idx];
			AVRational audioBase = pInputRuntime->input_time_base[pInputRuntime->stream_info->audio_idx];
			if (videoBase.den == audioBase.den && videoBase.num == audioBase.num && audioBase.den == secondBase && audioBase.num == 1)
			{
				//int64_t min = secondBase / 3;
				//int64_t max = secondBase * 2;
				int64_t delta = pInputRuntime->lastAudioPacket - pInputRuntime->lastVideoPacket;

				pInputRuntime->audioDelayStatisticsCounter++;
				pInputRuntime->audioDelayStatistics += delta;

				if (pInputRuntime->audioDelayStatisticsCounter > 45)
				{
					int64_t ave = pInputRuntime->audioDelayStatistics / pInputRuntime->audioDelayStatisticsCounter;
					if (ave > 0)
						pInputRuntime->audioShiftTarget = ave;
					else
						pInputRuntime->audioShiftTarget = 0;

					pInputRuntime->audioDelayStatisticsCounter = 0;
					pInputRuntime->audioDelayStatistics = 0;
				}

				if (streamIndex == pInputRuntime->stream_info->audio_idx)
				{
					int64_t step = secondBase / 300; // 3ms
					int64_t toTarget = pInputRuntime->audioShiftTarget - pInputRuntime->audioShift;
					if (abs(toTarget) > step)
					{
						if (toTarget > 0)
							pInputRuntime->audioShift += step;
						else 
							pInputRuntime->audioShift -= step;
					}

					//log("Audio shift is Set to %lld %lld %lld  %lld", delta, pInputRuntime->audioShift, pInputRuntime->audioShiftTarget, pkt.duration);

					pkt.dts -= pInputRuntime->audioShift;
					pkt.pts -= pInputRuntime->audioShift;
				}
			}
		}

		if (pkt.stream_index == pInputRuntime->stream_info->video_idx)
		{
			InterlockedAdd64(&statistics.current.Values[statisticTypeVideoFrames], 1);
			InterlockedAdd64(&statistics.current.Values[statisticTypeVideoBytes], pkt.size);
		}
		else if (pkt.stream_index == pInputRuntime->stream_info->audio_idx)
		{
			InterlockedAdd64(&statistics.current.Values[statisticTypeAudioFrames], 1);
			InterlockedAdd64(&statistics.current.Values[statisticTypeAudioBytes], pkt.size);
		}
		else
			continue;

		if (pActiveEncoderParameters == NULL)
		{
			Enque(pkt);

			TRACE_TIME((&pkt), "ENQUE");

			av_packet_unref(&pkt);
		}
		else
		{
			if (pEncoderRuntime != NULL)
			{
				AVCodecContext* decCtx = pInputRuntime->decoder_ctx[pkt.stream_index];
				av_packet_rescale_ts(&pkt, pInputRuntime->input_fmt_ctx->streams[pkt.stream_index]->time_base, decCtx->time_base);

				MARK_TRACE_TIME((&pkt));

				if (pkt.stream_index == 0)
				{
					//fix obs cam when deltas a like 0-0-2-0-2
					if (pInputRuntime->fixFpsLastPacketTime != 0 && pInputRuntime->fixFpsLastPacketTime <= pkt.pts)
					{
						int64_t delta = pkt.pts - pInputRuntime->fixFpsLastPacketTime;
						if (delta >= 2)
						{
							pkt.pts--;
							pkt.dts--;
						}
					}
					pInputRuntime->fixFpsLastPacketTime = pkt.pts;
				}
				

				/*AVPacket orig_pkt = pkt;
				do
				{
					Measurer processing(&statistics.current.Values[statisticTypeProcessingTime]);
					int decoded_size = transcode_packet(&pkt);

					if (decoded_size < 0) break;

					int decoded = FFMIN(decoded_size, pkt.size);
					pkt.data += decoded;
					pkt.size -= decoded;
				} while (pkt.size > 0);

				av_packet_unref(&orig_pkt);/**/

				DecoderTask* task = decoderPool.Rent();
				task->Packet = pkt;
				task->PacketNumber = packetSequence++;
				task->StreamIndex = pkt.stream_index;

				TRACE_FPS_PACKET(lastInTime1, pkt.stream_index, &pkt, "Receive");

#ifdef TRACE_PIPELINE
				task->StartTime = CurrentTime();
				ATLTRACE("[%d %d %d %d]\r\n", pTranscodingDecoder->GetQueueSize(), pTranscodingFilter->GetQueueSize(), pTranscodingEncoder->GetQueueSize(), pTranscodingCallback->GetQueueSize());
				TRACE_PIPELINE_TIME(task, "After read");
#endif

				pTranscodingDecoder->Enque(task);/**/
			}
			else
			{
				av_packet_unref(&pkt);
			}
		}
	}
}

void CDynamicStreamer::ReinitDirectFrame()
{
	StopPipeline();

	if (pEncoderRuntime && pActiveDirectFrameParameters)
	{
		pEncoderRuntime->pDirectFrameCallbackInterface = pActiveDirectFrameParameters->pCallback;
		pEncoderRuntime->DeleteDirectFrameFilter();

		if (pEncoderRuntime->pDirectFrameCallbackInterface)
		{
			pEncoderRuntime->pDirectFrameFilter = new FilterRuntime(pEncoderRuntime->stream_info);
			int stream = pEncoderRuntime->stream_info.video_idx;
			if (!init_filter_for_directframe(&pEncoderRuntime->pDirectFrameFilter->filter_ctx[stream], pInputRuntime->decoder_ctx[stream], pEncoderRuntime->encoder_ctx[stream]))
			{
				pEncoderRuntime->DeleteDirectFrameFilter();
			}
		}
	}
}

int CDynamicStreamer::open_decoder_from_encoder(AVCodecContext *enc_ctx, AVCodecContext** dec_ctx)
{
	AVCodec* decoder = avcodec_find_decoder(enc_ctx->codec_id);

	AVCodecContext* dec = avcodec_alloc_context3(decoder);

	/*AVCodecParameters* params = avcodec_parameters_alloc();

	avcodec_parameters_from_context(params, enc_ctx);
	avcodec_parameters_to_context(dec, params);

	//avcodec_parameters_free(&params);

	dec->profile = 100;
	dec->level = 41;
	dec->bits_per_raw_sample = 8;
	dec->field_order = AV_FIELD_PROGRESSIVE;
	dec->chroma_sample_location = AVCHROMA_LOC_LEFT;*/

	dec->height = enc_ctx->height;
	dec->width = enc_ctx->width;

/*	dec->height = enc_ctx->height;
	dec->width = enc_ctx->width;
	dec->sample_aspect_ratio = enc_ctx->sample_aspect_ratio;
	if (decoder->pix_fmts)
		dec->pix_fmt = decoder->pix_fmts[0];
	else
		dec->pix_fmt = enc_ctx->pix_fmt;
	dec->time_base = av_inv_q(video_dec_ctx->framerate);
	*/

	//avcodec_parameters_from_context(params, dec);
	//avcodec_parameters_free(&params);
	if (enc_ctx->extradata_size) {
		dec->extradata = (uint8_t*)av_mallocz(enc_ctx->extradata_size + AV_INPUT_BUFFER_PADDING_SIZE);
		if (dec->extradata) {
			memcpy(dec->extradata, enc_ctx->extradata, enc_ctx->extradata_size);
			dec->extradata_size = enc_ctx->extradata_size;
		}
	}

	int ret = avcodec_open2(dec, decoder, NULL);
	*dec_ctx = dec;
	return 0;
}

BOOL CDynamicStreamer::NeedsReinit(OutputDescriptor* desc)
{
	CriticalSectionLock lock(allCS);
	if (desc->pPendingParameters)
	{
		if (desc->pActualParameters)
			delete desc->pActualParameters;

		desc->pActualParameters = desc->pPendingParameters;
		desc->pPendingParameters = NULL;
		return TRUE;
	}
	return FALSE;
}


void CDynamicStreamer::OutputThreadRoutine(OutputDescriptor* desc)
{
	set_thread_name(desc->id);

	Measurer::SetTime(&desc->statistics.overall.StartTime);
	Measurer::SetTime(&desc->statistics.current.StartTime);

	int errorCounter = 0;

	while (desc->continue_thread)
	{
		if (NeedsReinit(desc) || !desc->outputInitialized)
		{
			desc->CloseOutput();
			if (open_output(desc) != S_OK)
			{
				int waitCount = 0;
				while (waitCount++ < 30 && desc->continue_thread)
					Sleep(100);
				continue;
			}
			desc->justReinitialized = TRUE;
			log("reinit required %d", desc->id);
			AddReaderToQueue(desc);
		}

		AVPacket packet;
		Deque(packet, desc);

		TRACE_TIME((&packet), "DEQUE");

		if (!desc->continue_thread)
			break;

		int stream = packet.stream_index;

		if (desc->pActualParameters && desc->pActualParameters->perStream[stream].codecpar)
		{
			Measurer measurer(&desc->statistics.current.Values[statisticTypeProcessingTime]);

			if (desc->pCallback == NULL)
				SendPacketToOutput(desc, packet);
			else
				SendPacketToCallback(desc, packet);
		}

		av_packet_unref(&packet);
	}
	if (desc->output_ctx != NULL)
		av_write_trailer(desc->output_ctx);
	desc->CloseOutput();
}

void CDynamicStreamer::SendPacketToOutput(OutputDescriptor* desc, AVPacket& packet)
{
	int stream = packet.stream_index;
	int size = packet.size;
	AVRational inputTB = desc->pActualParameters->perStream[stream].input_time_base;

	//log("SZ %d %d", size, stream);
	//log("BR %d %d %d %d %d", stream, size, packet.flags, packet.dts, packet.pts);

	av_packet_rescale_ts(&packet, inputTB, desc->output_ctx->streams[stream]->time_base);

	if (desc->baseTime == 0)
	{
		desc->baseTime = packet.dts;
	}

	packet.dts -= desc->baseTime;
	packet.pts -= desc->baseTime;

	ULONGLONG startTime = CurrentTime();
	
    TRACE_FPS_PACKET(lastOutTime1, stream, &packet, "Sending");

	//int res = av_write_frame(desc->output_ctx, &packet);
	int res = av_interleaved_write_frame(desc->output_ctx, &packet);

	TRACE_FPS_PACKET(lastOutTime2, stream, &packet, "Sent   ");


	ULONGLONG time = CurrentTime() - startTime;
	if (time > 3000000)
		log("long send %llu ms (%d)", time / 10000, desc->id);

	/*if (res >= 0)
	{
		desc->packetCounter++;
		if ((desc->packetCounter / 45) % 400 == 2)
			log("time %llu", time / 10000);
	}*/

	TRACE_TIME((&packet), "WROTE");

	if (res < 0)
	{
		if (res == -22 && desc->initialErrorCounter < 8)
		{
			desc->initialErrorCounter++;
			log("skip error %d %d %d", stream, packet.dts, packet.pts);
		}
		else
		{
			set_error(desc, "write frame error", res, LOCATION);

			desc->errorCounter += 5;
			if (desc->errorCounter > 50)
				desc->CloseOutput();
		}
	}
	else
	{
		if (desc->errorCounter > 0)
			desc->errorCounter--;
		if (stream == desc->pActualParameters->stream_info.video_idx)
		{
			InterlockedAdd64(&desc->statistics.current.Values[statisticTypeVideoFrames], 1);
			InterlockedAdd64(&desc->statistics.current.Values[statisticTypeVideoBytes], size);
		}
		else
		{
			InterlockedAdd64(&desc->statistics.current.Values[statisticTypeAudioFrames], 1);
			InterlockedAdd64(&desc->statistics.current.Values[statisticTypeAudioBytes], size);
		}
		clear_error(desc);
	}
}

void CDynamicStreamer::SendPacketToCallback(OutputDescriptor* desc, AVPacket& packet)
{
	AVPacket forDecode = packet;
	do
	{
		Measurer processing(&statistics.current.Values[statisticTypeProcessingTime]);

		int decoded_size = decode_packet_for_callback(&forDecode, desc);

		if (decoded_size < 0) break;

		int decoded = FFMIN(decoded_size, forDecode.size);
		forDecode.data += decoded;
		forDecode.size -= decoded;
	} while (forDecode.size > 0);

	InterlockedAdd64(&desc->statistics.current.Values[statisticTypeVideoFrames], 1);
	InterlockedAdd64(&desc->statistics.current.Values[statisticTypeVideoBytes], packet.size);
}

void CDynamicStreamer::get_stat(int id, Statistics& s, std::vector<CDynamicStreamerStatistics*>& v, LONG64 now)
{
	CComObject<CDynamicStreamerStatistics>* pCur = new CComObject<CDynamicStreamerStatistics>();
	pCur->id = id;
	pCur->overall = false;

	for (int i = 0; i < statisticTypeCount; i++)
		pCur->Item.Values[i] = InterlockedExchange64(&s.current.Values[i], 0);

	if (id == -1)
	{
		int sum = pTranscodingFilter->GetQueueSize() + pTranscodingEncoder->GetQueueSize() + pTranscodingDecoder->GetQueueSize() + pTranscodingCallback->GetQueueSize();
		pCur->Item.Values[statisticTypeProcessingTime] = sum;
	}
	
	pCur->Item.StartTime = now - s.current.StartTime;
	s.current.StartTime = now;

#ifdef TRACE_FPS
	log("FPS %lld %lld", pCur->Item.Values[0], pCur->Item.StartTime);
	log("BR %lld %lld", pCur->Item.Values[1]*8/1000, pCur->Item.StartTime);
#endif

	for (int i = 0; i < statisticTypeCount; i++)
		s.overall.Values[i] += pCur->Item.Values[i];

	CComObject<CDynamicStreamerStatistics>* pAll = new CComObject<CDynamicStreamerStatistics>();
	pAll->id = id;
	pAll->overall = true;
	pAll->error = s.lastError;
	pAll->errorMessage = s.lastErrorMessage;

	for (int i = 0; i < statisticTypeCount; i++)
		pAll->Item.Values[i] = s.overall.Values[i];

	pAll->Item.StartTime = now - s.overall.StartTime;

	v.push_back(pCur);
	v.push_back(pAll);
}

int CDynamicStreamer::open_decoder_context(InputRuntime* runtime, enum AVMediaType type, int streamsCount)
{
	int stream_idx = av_find_best_stream(runtime->input_fmt_ctx, type, -1, -1, NULL, 0);
	if (stream_idx < 0)
		return -1;
	
	AVStream *st = runtime->input_fmt_ctx->streams[stream_idx];
	runtime->input_time_base[stream_idx] = st->time_base;
	AVCodec *dec = avcodec_find_decoder(st->codecpar->codec_id);
	if (!dec) 
		THROW("Unable to find decoder");

	log("Input decoder (%s)", dec->name);

	for (int q = 0; q < DECODERS_COUNT; q++)
	{
		AVCodecContext* decctx = avcodec_alloc_context3(dec);
		runtime->decoder_ctx[q * streamsCount + stream_idx] = decctx;
		if (!decctx)
			THROW("Unable to create decoder context");

		CHECK(avcodec_parameters_to_context(decctx, st->codecpar));

		if (decctx->codec_type == AVMEDIA_TYPE_VIDEO)
		{
			if (st->codecpar->format == AV_PIX_FMT_NONE)
			{
				if (decctx->codec->id == AV_CODEC_ID_MJPEG)
					decctx->pix_fmt = AV_PIX_FMT_YUV422P;
				else
					decctx->pix_fmt = AV_PIX_FMT_YUYV422;
			}
			else if (decctx->pix_fmt == AV_PIX_FMT_YUVJ422P)
				decctx->pix_fmt = AV_PIX_FMT_YUV422P;

			AVRational framerate = av_guess_frame_rate(runtime->input_fmt_ctx, st, NULL);
			av_reduce(&decctx->framerate.num, &decctx->framerate.den, framerate.num, framerate.den, 100000);

			//hack
			if (pInputRuntime->Fps != 0)
				av_reduce(&decctx->framerate.num, &decctx->framerate.den, (int64_t)(pInputRuntime->Fps), 1, 100000);
		}

		AVDictionary* dict = nullptr;

		av_dict_set(&dict, "refcounted_frames", "1", 0);

		CHECK(avcodec_open2(decctx, dec, &dict));

		if (type == AVMEDIA_TYPE_AUDIO && !decctx->channel_layout)
			decctx->channel_layout = av_get_default_channel_layout(decctx->channels);
	}
	return stream_idx;
}

STDMETHODIMP CDynamicStreamer::GetStatistics(SAFEARRAY **pStats)
{
	std::vector<CDynamicStreamerStatistics*> v;

	LONGLONG now;
	Measurer::SetTime(&now);

	
	if (pTranscodingFilter->GetQueueSize() >= 4 ||
		pTranscodingEncoder->GetQueueSize() >= 4 ||
		pTranscodingDecoder->GetQueueSize() >= 4 ||
		pTranscodingCallback->GetQueueSize() >= 4)
	{
		log("Overload d%d, f%d, e%d, c%d", pTranscodingDecoder->GetQueueSize(), pTranscodingFilter->GetQueueSize(), pTranscodingEncoder->GetQueueSize(), pTranscodingCallback->GetQueueSize());
	}

	::EnterCriticalSection(&allCS);

	get_stat(-1, statistics, v, now);

	for (int i = 0; i < outputs.size(); i++)
	{
		OutputDescriptor* desc = outputs.at(i);
		if (desc != NULL)
			get_stat(i, desc->statistics, v, now);
	}

	::LeaveCriticalSection(&allCS);

	CComSafeArray<VARIANT> items(v.size());
	for (int i = 0; i < v.size(); i++)
	{
		CComVariant var(v.at(i));
		items.SetAt(i, var);
	}
	*pStats = items.Detach();
	return S_OK;
}

STDMETHODIMP CDynamicStreamer::SetInput(BSTR type, BSTR  input, BSTR options, int fps, int width, int height)
{
	CriticalSectionLock lock(allCS);

	if (pPendingInputParameters)
		delete pPendingInputParameters;

	pPendingInputParameters = new InputParameters();

	pPendingInputParameters->input_input = ConvertUnicodeToUTF8(CStringW(input));
	pPendingInputParameters->input_type = ConvertUnicodeToUTF8(CStringW(type));
	pPendingInputParameters->input_options = ConvertUnicodeToUTF8(CStringW(options));
	pPendingInputParameters->Fps = fps;
	pPendingInputParameters->Width = width;
	pPendingInputParameters->Height = height;


	StartInputThread();
	return S_OK;
}

STDMETHODIMP CDynamicStreamer::SetCallback(IDynamicStreamerCallback* pCallback)
{
	if (m_pCallback)
		m_pCallback->Release();
	if (pCallback)
		pCallback->AddRef();
	m_pCallback = pCallback;
	return S_OK;
}





STDMETHODIMP CDynamicStreamer::SetFilter(BSTR videoFilter)
{
	CriticalSectionLock lock(allCS);

	if (pPendingFilterParameters)
		delete pPendingFilterParameters;

	pPendingFilterParameters = new FilterParameters();

	pPendingFilterParameters->video_filter = ConvertUnicodeToUTF8(CStringW(videoFilter));
	return S_OK;
}

STDMETHODIMP CDynamicStreamer::GetSupportedCodecs(int* pFlags)
{
	pFlags = 0;
	return S_OK;
}

STDMETHODIMP CDynamicStreamer::SetEncoder(BSTR videoCodec, BSTR videoOptions, BSTR fallbackvideoCodec, BSTR fallbackVideoOptions, int videoMaxBitrate, BSTR audioCodec, BSTR audioOptions, int audioMaxBitrate)
{
	CriticalSectionLock lock(allCS);

	if (pPendingEncoderParameters)
		delete pPendingEncoderParameters;

	pPendingEncoderParameters = new EncoderParameters();
	pPendingEncoderParameters->encoder_videoCodec = ConvertUnicodeToUTF8(CStringW(videoCodec));
	pPendingEncoderParameters->encoder_audioCodec = ConvertUnicodeToUTF8(CStringW(audioCodec));
	pPendingEncoderParameters->encoder_videoOptions = ConvertUnicodeToUTF8(CStringW(videoOptions));
	pPendingEncoderParameters->encoder_audioOptions = ConvertUnicodeToUTF8(CStringW(audioOptions));
	pPendingEncoderParameters->encoder_videoMaxBitrate = videoMaxBitrate;
	pPendingEncoderParameters->encoder_audioMaxBitrate = audioMaxBitrate;

	pPendingEncoderParameters->encoder_fallback_videoCodec = ConvertUnicodeToUTF8(CStringW(fallbackvideoCodec));
	pPendingEncoderParameters->encoder_fallback_videoOptions = ConvertUnicodeToUTF8(CStringW(fallbackVideoOptions));

	return S_OK;
}

STDMETHODIMP CDynamicStreamer::SetDirectFrameCallback(IDynamicStreamerDecoderCallback* pCallback)
{
	::EnterCriticalSection(&allCS);
	if (pPendingDirectFrameParameters)
		delete pPendingDirectFrameParameters;

	pPendingDirectFrameParameters = new DirectFrameParamiters();
	pPendingDirectFrameParameters->pCallback = pCallback;
	::LeaveCriticalSection(&allCS);
	return S_OK;
}

STDMETHODIMP CDynamicStreamer::AddOutput(BSTR type, BSTR output, BSTR options, IDynamicStreamerDecoderCallback* pCallback, int *pId)
{
	::EnterCriticalSection(&allCS);

	OutputDescriptor* desc = new OutputDescriptor();
	*pId = -1;
	for (size_t i = 0; i < outputs.size(); i++)
	{
		if (outputs[i] == NULL)
		{
			outputs[i] = desc;
			*pId = i;
			break;
		}
	}
	if (*pId == -1)
	{
		*pId = outputs.size();
		outputs.push_back(desc);
	}

	desc->options = ConvertUnicodeToUTF8(CStringW(options));
	desc->type = ConvertUnicodeToUTF8(CStringW(type));
	desc->output = ConvertUnicodeToUTF8(CStringW(output));
	desc->parent = this;
	desc->id = *pId;

	if (pCallback)
		pCallback->AddRef();
	desc->pCallback = pCallback;

    desc->thread = ::CreateThread(NULL, 0, OutputThread, desc, 0, NULL);

	outputUpdatePending = TRUE;

	UpdateDrcTarget();
	::LeaveCriticalSection(&allCS);

	return S_OK;
}

STDMETHODIMP CDynamicStreamer::RemoveOutput(int id)
{
	::EnterCriticalSection(&allCS);
	OutputDescriptor* desc = outputs[id];
	desc->continue_thread = FALSE;
	outputs[id] = NULL;
	UpdateDrcTarget();
	::LeaveCriticalSection(&allCS);

	if (::WaitForSingleObject(desc->thread, 5000) == WAIT_TIMEOUT)
	{
		::TerminateThread(desc->thread, 0);
	}
	::CloseHandle(desc->thread);
	delete desc;
	
	return S_OK;
}


void CDynamicStreamer::InitFilter()
{
	StopPipeline();
	if (pEncoderRuntime != NULL)
	{
		pEncoderRuntime->ResetFilter();

		try
		{
			for (int i = 0; i < pInputRuntime->stream_info->count; i++)
			{
				if (i == pInputRuntime->stream_info->video_idx)
				{
					CStringA filter = "null";
					if (pActiveFilterParameters)
						filter = pActiveFilterParameters->video_filter;

					init_filter(&pEncoderRuntime->pFilterRuntime->filter_ctx[i], pInputRuntime->decoder_ctx[i], pEncoderRuntime->encoder_ctx[i], filter);//"zoompan=z='2.5':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d=1");// , "null"
				}
				else if (i == pInputRuntime->stream_info->audio_idx)
				{
					init_filter(&pEncoderRuntime->pFilterRuntime->filter_ctx[i], pInputRuntime->decoder_ctx[i], pEncoderRuntime->encoder_ctx[i], "anull");
				}
			}
		}
		CATCH_STD
		{
			delete pEncoderRuntime;
			pEncoderRuntime = NULL;
		}
	}

	ReinitDirectFrame();
}

int CDynamicStreamer::parse_dictionary(AVDictionary **pm, const char *str)
{
	int result = 0;
	if (strchr(str, NEW_KEY_VALUE_SEPARATOR[0]))
	{
		result = av_dict_parse_string(pm, str, NEW_KEY_VALUE_SEPARATOR, NEW_PAIRS_SEPARATOR, 0);
	}
	else
	{
		result = av_dict_parse_string(pm, str, KEY_VALUE_SEPARATOR, PAIRS_SEPARATOR, 0);
	}
	return result;
}

CStringA CDynamicStreamer::get_encoder_options_audio(CStringA& videoOptions, int videoBitrate)
{
	char args[512];
	sprintf_s(args, sizeof(args),
		"b%s%dk%s%s",
		NEW_KEY_VALUE_SEPARATOR,
		videoBitrate,
		NEW_PAIRS_SEPARATOR,
		(const char*)videoOptions);

	return CStringA(args);
}

CStringA CDynamicStreamer::get_encoder_options_video(CStringA& videoOptions, int videoBitrate)
{
	char result[1024];

	sprintf_s(result, sizeof(result),
		"bufsize%s%dk%smaxrate%s%dk%sminrate%s%dk%sb%s%dk%s%s",
		NEW_KEY_VALUE_SEPARATOR,
		(int)(videoBitrate*1.2),
		NEW_PAIRS_SEPARATOR,
		NEW_KEY_VALUE_SEPARATOR,
		videoBitrate,
		NEW_PAIRS_SEPARATOR,
		NEW_KEY_VALUE_SEPARATOR,
		videoBitrate,
		NEW_PAIRS_SEPARATOR,
		NEW_KEY_VALUE_SEPARATOR,
		videoBitrate,
		NEW_PAIRS_SEPARATOR,
		(const char*)videoOptions);

	return CStringA(result);
}

void CDynamicStreamer::StartInputThread()
{
	continue_input_thread = TRUE;
	if (thread == NULL)
	{
		thread = ::CreateThread(NULL, 0, InputThread, this, 0, NULL);
	}
}

void CDynamicStreamer::FinalRelease()
{
	if (loggerInstance == this)
		loggerInstance = NULL;

	for (int i = 0; i < outputs.size(); i++)
	{
		if (outputs[i])
			RemoveOutput(i);
	}

	StopInputThread();

	StopPipeline();

	delete pTranscodingDecoder;
	delete pTranscodingEncoder;
	delete pTranscodingFilter;
	delete pTranscodingCallback;

	::DeleteCriticalSection(&allCS);

	for (int q = 0; q < packetQueue.size(); q++)
		av_packet_unref(&packetQueue[q]);

	if (m_pCallback)
		m_pCallback->Release();

	if (pActiveInputParameters)
		delete pActiveInputParameters;
	if (pActiveEncoderParameters)
		delete pActiveEncoderParameters;
	if (pActiveFilterParameters)
		delete pActiveFilterParameters;
	if (pPendingFilterParameters)
		delete pPendingFilterParameters;
	if (pPendingInputParameters)
		delete pPendingInputParameters;
	if (pPendingEncoderParameters)
		delete pPendingEncoderParameters;
	if (pPendingDirectFrameParameters)
		delete pPendingDirectFrameParameters;
	if (pActiveDirectFrameParameters)
		delete pActiveDirectFrameParameters;

	if (pInputRuntime)
		delete pInputRuntime;

	if (pEncoderRuntime)
		delete pEncoderRuntime;

	m_ftm.Release();
}

void CDynamicStreamer::StopInputThread()
{
	continue_input_thread = FALSE;
	if (thread != NULL)
	{
		if (::WaitForSingleObject(thread, 5000) == WAIT_TIMEOUT)
		{
			log("Input thread is to be terminated");
			::TerminateThread(thread, 0);
		}
		::CloseHandle(thread);
		thread = NULL;
	}
}

void CDynamicStreamer::StopPipeline()
{
	if (bPipelineStarted)
	{
		// order is important
		if (!pTranscodingDecoder->StopThread())
			log("pTranscodingDecoder was terminated");
		if (!pTranscodingFilter->StopThread())
			log("pTranscodingFilter was terminated");
		if (!pTranscodingEncoder->StopThread())
			log("pTranscodingEncoder was terminated");
		if (!pTranscodingCallback->StopThread())
			log("pTranscodingCallback was terminated");
		bPipelineStarted = FALSE;
	}
}

void CDynamicStreamer::StartPipeline()
{
	if (!bPipelineStarted && pEncoderRuntime)
	{
		packetSequence = 0;
		pTranscodingCallback->StartThread();
		pTranscodingEncoder->StartThread();
		pTranscodingFilter->StartThread();
		pTranscodingDecoder->StartThread();
		bPipelineStarted = TRUE;
	}
}

int CDynamicStreamer::transcode_packet(AVPacket* packet)
{
	int idx = packet->stream_index;

	dec_func decoder = (idx == pInputRuntime->stream_info->video_idx) ? avcodec_decode_video2 : avcodec_decode_audio4;
	AVCodecContext* decCtx = pInputRuntime->decoder_ctx[idx];

	int got_frame = 0;
	AVFrame* frame = pEncoderRuntime->frame;
	int decode_ret = decoder(decCtx, frame, &got_frame, packet);

	if (decode_ret < 0)
		set_error(NULL, "decoding error", decode_ret, LOCATION);

	if (got_frame)
	{
		frame->pts = frame->best_effort_timestamp;
		frame->pict_type = AV_PICTURE_TYPE_NONE;
		int ret_flags = av_buffersrc_add_frame_flags(pEncoderRuntime->pFilterRuntime->filter_ctx[idx].buffersrc_ctx, frame, 0);
		//Sleep(10);
		if (ret_flags >= 0)
		{
			while (1)
			{
				AVFrame* filter_frame = pEncoderRuntime->filter_frame;
				int ret = av_buffersink_get_frame(pEncoderRuntime->pFilterRuntime->filter_ctx[idx].buffersink_ctx, filter_frame);
				if (ret < 0)
				{
					if (ret == AVERROR(EAGAIN) || ret == AVERROR_EOF)
						ret = 0;
					else
						set_error(NULL, "Filtering error", ret, LOCATION);
					av_frame_unref(filter_frame);
					break;
				}
				filter_frame->pict_type = AV_PICTURE_TYPE_NONE;
				AVPacket pkt2;
				av_init_packet(&pkt2);
				pkt2.data = NULL;
				pkt2.size = 0;
				enc_func encoder = (idx == pInputRuntime->stream_info->video_idx) ? avcodec_encode_video2 : avcodec_encode_audio2;
				int got_output = 0;
				int ret2 = encoder(pEncoderRuntime->encoder_ctx[idx], &pkt2, filter_frame, &got_output);
				if (ret2 < 0)
				{
					av_frame_unref(filter_frame);
					Sleep(5);
					encoderErrors++;
					ReinitEncoder(NULL, NULL);
					set_error(NULL, "encoding error", ret2, LOCATION);
				}
				else
					av_frame_unref(filter_frame);

				if (got_output)
				{
					pkt2.stream_index = idx;

#ifdef TRACE_PACKETTIME
					if (idx == 0)
					{
						pkt2.pts = packet->pts;
						pkt2.dts = packet->dts;
						TRACE_TIME((&pkt2), "TRANS");
					}
#endif
					Enque(pkt2);

					clear_error(NULL);
				}
				av_packet_unref(&pkt2);
			}
		}
		else set_error(NULL, "Filter Feeding error", ret_flags, LOCATION);
	}
	return decode_ret;
}


void CDynamicStreamer::TranscodingDecode(void* item, int threadNo)
{
	DecoderTask* task = (DecoderTask*)item;
	AVPacket* pPacket = &task->Packet;
	int idx = pPacket->stream_index;
	TRACE_PIPELINE_TIME(task, "Before decode");
	do
	{
		dec_func decoder = (idx == pInputRuntime->stream_info->video_idx) ? avcodec_decode_video2 : avcodec_decode_audio4;
		AVCodecContext* decCtx = pInputRuntime->decoder_ctx[pInputRuntime->stream_info->count * threadNo + idx];

		int got_frame = 0;
		
		FrameTask* next = filterPool.Rent();

		int decode_ret = decoder(decCtx, next->Frame, &got_frame, pPacket);

		if (idx == pInputRuntime->stream_info->video_idx && next->Frame->format == AV_PIX_FMT_YUVJ422P)
			next->Frame->format = AV_PIX_FMT_YUV422P;

		if (decode_ret < 0)
			set_error(NULL, "decoding error", decode_ret, LOCATION);

		if (got_frame)
		{
			next->CopyFrom(task);
			av_packet_ref(&next->Packet, pPacket);

			TRACE_PIPELINE_TIME(task, "After decode");

			pTranscodingFilter->EnqueWithSorting(next);
		}
		else
		{
			filterPool.Release(next);
		}

		if (decode_ret < 0) break;

		int decoded = FFMIN(decode_ret, pPacket->size);
		pPacket->data += decoded;
		pPacket->size -= decoded;
	} while (pPacket->size > 0);
	
	av_packet_unref(&task->Packet);
	decoderPool.Release(task);
}

void CDynamicStreamer::TranscodingFilter(void* item, int threadNo)
{
	FrameTask* task = (FrameTask*)item;
	AVFrame* frame = task->Frame;
	int idx = task->StreamIndex;

	TRACE_PIPELINE_TIME(task, "Before Filter");
	//Sleep(15);

	frame->pts = frame->best_effort_timestamp;
	frame->pict_type = AV_PICTURE_TYPE_NONE;
	int ret_flags = av_buffersrc_add_frame_flags(pEncoderRuntime->pFilterRuntime->filter_ctx[idx].buffersrc_ctx, frame, 0);

	TRACE_PIPELINE_TIME(task, "Middle Filter");
	if (ret_flags >= 0)
	{
		while (1)
		{
			FrameTask* next = encoderPool.Rent();
			int ret = av_buffersink_get_frame(pEncoderRuntime->pFilterRuntime->filter_ctx[idx].buffersink_ctx, next->Frame);
			if (ret < 0)
			{
				if (ret == AVERROR(EAGAIN) || ret == AVERROR_EOF)
					ret = 0;
				else
					set_error(NULL, "Filtering error", ret, LOCATION);

				av_frame_unref(next->Frame);
				encoderPool.Release(next);
				break;
			}
			next->Frame->pict_type = AV_PICTURE_TYPE_NONE;

			if (idx == pInputRuntime->stream_info->video_idx && pEncoderRuntime->pDirectFrameFilter)
			{
				auto cf = callbackPool.Rent();
				cf->CopyFrom(task);
				av_frame_ref(cf->Frame, next->Frame);
				av_packet_ref(&cf->Packet, &task->Packet);

				pTranscodingCallback->Enque(cf);
			}

			next->CopyFrom(task);

			TRACE_PIPELINE_TIME(task, "After filter");

			pTranscodingEncoder->Enque(next);
		}
	}
	else
	{
		set_error(NULL, "Filter Feeding error", ret_flags, LOCATION);
	}
    av_packet_unref(&task->Packet);
	filterPool.Release(task);
}

void CDynamicStreamer::TranscodingCallback(void* item, int threadNo)
{
	FrameTask* task = (FrameTask*)item;
	AVFrame* frame = task->Frame;
	int idx = task->StreamIndex;

	frame->pts = frame->best_effort_timestamp;
	frame->pict_type = AV_PICTURE_TYPE_NONE;
	int ret_flags = av_buffersrc_add_frame_flags(pEncoderRuntime->pDirectFrameFilter->filter_ctx[idx].buffersrc_ctx, frame, 0);
	av_packet_unref(&task->Packet);
	//av_frame_unref(task->Frame);
	if (ret_flags >= 0)
	{
		while (1)
		{
			FrameTask* next = callbackTempPool.Rent();
			int ret = av_buffersink_get_frame(pEncoderRuntime->pDirectFrameFilter->filter_ctx[idx].buffersink_ctx, next->Frame);
			if (ret < 0)
			{
				if (ret == AVERROR(EAGAIN) || ret == AVERROR_EOF)
					ret = 0;
				else
					set_error(NULL, "Filtering error", ret, LOCATION);

				av_frame_unref(next->Frame);
				callbackTempPool.Release(next);
				break;
			}

			int size = next->Frame->width * next->Frame->height * 3;
			byte* data = (byte*)next->Frame->data[0];

			pEncoderRuntime->pDirectFrameCallbackInterface->NotifyFrame(next->Frame->width, next->Frame->height, size, (__int64)(void*)data);
			av_frame_unref(next->Frame);
			callbackTempPool.Release(next);
		}
	}
	else
	{
		set_error(NULL, "Filter Feeding error", ret_flags, LOCATION);
	}
	callbackPool.Release(task); 
}

void CDynamicStreamer::TranscodingEncode(void* item, int threadNo)
{
	FrameTask* task = (FrameTask*)item;
	int idx = task->StreamIndex;

	TRACE_PIPELINE_TIME(task, "Before encode");

	AVPacket pkt2;
	av_init_packet(&pkt2);
	pkt2.data = NULL;
	pkt2.size = 0;
	enc_func encoder = (idx == pInputRuntime->stream_info->video_idx) ? avcodec_encode_video2 : avcodec_encode_audio2;
	int got_output = 0;
	int ret2 = encoder(pEncoderRuntime->encoder_ctx[idx], &pkt2, task->Frame, &got_output);
	av_frame_unref(task->Frame);

	if (ret2 < 0)
	{
		bReinitEncoderAfterFail = TRUE;
		set_error(NULL, "encoding error", ret2, LOCATION);
	}

	if (got_output)
	{
		pkt2.stream_index = idx;

		TRACE_PIPELINE_TIME(task, "After encode");
		TRACE_FPS_PACKET(lastInTime2, idx, &pkt2, "Encoded");

		Enque(pkt2);

		clear_error(NULL);
	}
	av_packet_unref(&pkt2);
	av_packet_unref(&task->Packet);

	encoderPool.Release(task);
}