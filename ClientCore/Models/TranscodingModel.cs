﻿using DeltaModel;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Services;
using Streamster.ClientCore.Support;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Streamster.ClientCore.Models
{
    public class TranscodingModel
    {
        private readonly ConnectionService _connectionService;
        private readonly IAppResources _appResources;
        private string _transId;

        public bool TranscodingEnabled { get; private set; }

        public Property<ITranscoder> Transcoder { get; } = new Property<ITranscoder>();

        public CoreData CoreData { get; private set; }

        public StreamSettingsModel StreamSettings { get; }

        public Property<TranscodingMessageType> Message { get; } = new Property<TranscodingMessageType>();
        public Property<bool> NoneTranscodedChangeDisabled { get; } = new Property<bool>();
        public Property<bool> TranscodedChangeDisabled { get; } = new Property<bool>();

        public ObservableCollection<TranscodingChannelModel> NoneTranscoded { get; } = new ObservableCollection<TranscodingChannelModel>();
        public ObservableCollection<TranscodingChannelModel> Transcoded { get; } = new ObservableCollection<TranscodingChannelModel>();

        public Resolution[] Resolutions { get; private set; }
        public int[] FpsList { get; private set; }

        public string MaxResolution { get; private set; }
        public string MaxFps { get; private set; }

        public TrascodingComboboxValue[] OriginalFpss { get; set; }
        public TrascodingComboboxValue[] OriginalResolutions { get; set; }

        public Property<TrascodingComboboxValue> OriginalFpsCurrent { get; } = new Property<TrascodingComboboxValue>();
        public Property<TrascodingComboboxValue> OriginalResolutionCurrent { get; } = new Property<TrascodingComboboxValue>();

        public Property<string> TranscodedDescription { get; } = new Property<string>();

        public Property<string> OriginalDescription { get; } = new Property<string>();

        public string TariffUrl { get; private set; }

        public TranscodingModel(CoreData coreData, StreamSettingsModel streamSettings, ConnectionService connectionService, IAppResources appResources)
        {
            CoreData = coreData;
            StreamSettings = streamSettings;
            _connectionService = connectionService;
            _appResources = appResources;
        }

        public void Start()
        {
            TariffUrl = _connectionService.UserName == null ? _appResources.AppData.PricingUrlForNotRegistered : _appResources.AppData.PricingUrl;

            var settings = CoreData.Root.Settings;
            var claims = _connectionService.Claims;

            if (CoreData.Root.Transcoders.Count == 0)
            {
                var trans = CoreData.Create<ITranscoder>();
                trans.Bitrate = Math.Min(2500, settings.Bitrate);
                trans.Fps = Math.Min(30, settings.Fps);
                trans.Resolution = new Resolution(1280, 720);
                if (settings.Resolution?.Width < trans.Resolution?.Width)
                    trans.Resolution = settings.Resolution;

                CoreData.Root.Transcoders[IdGenerator.New()] = trans;
            }

            TranscodingEnabled = claims.Transcoders > 0;
            _transId = CoreData.Root.Transcoders.First().Key;
            var transcoder = CoreData.Root.Transcoders[_transId]; ;
            Transcoder.Value = transcoder;

            OriginalFpss = StreamSettings.FpsList.Select(s => new TrascodingComboboxValue { Name = s.ToString(), Value = s, Good = s <= claims.TranscoderInputLimit.Fps }).ToArray();
            OriginalResolutions = StreamSettings.Resolutions.Select(s => new TrascodingComboboxValue { Name = s.ToString(), Value = s, Good = s.Height <= claims.TranscoderInputLimit.Height }).ToArray();

            Resolutions = StreamSettings.Resolutions.Where(s => s.Height <= claims.TranscoderOutputLimit.Height).ToArray();
            FpsList = StreamSettings.FpsList.Where(s => s <= claims.TranscoderOutputLimit.Fps).ToArray();

            if (claims.TranscoderOutputLimit.Fps < transcoder.Fps)
                transcoder.Fps = claims.TranscoderOutputLimit.Fps;
            if (claims.TranscoderOutputLimit.Height < transcoder.Resolution.Height)
                transcoder.Resolution = Resolutions[0];

            var maxInputResolution = OriginalResolutions.FirstOrDefault(s => s.Good)?.Value as Resolution;

            var maxInputResolutionString = maxInputResolution != null ? $"{maxInputResolution.Width}x{maxInputResolution.Height}" : "1920:1080";

            MaxResolution = $"Original stream's resolution exceeds maximum allowed {maxInputResolutionString}. TRANSCODING WILL NOT WORK.";
            MaxFps = $"Original stream's FPS (frames per second) exceeds maximum allowed {claims.TranscoderInputLimit.Fps}. TRANSCODING WILL NOT WORK.";

            OriginalFpsCurrent.OnChange = (a, b) =>
            {
                if (b?.Value is int newValue)
                    settings.Fps = newValue;
            };

            OriginalResolutionCurrent.OnChange = (a, b) =>
            {
                if (b?.Value is Resolution newValue)
                    settings.Resolution = newValue;
            };

            CoreData.Subscriptions.SubscribeForAnyProperty<IChannel>((s, c, p, v) => Refresh());
            CoreData.Subscriptions.SubscribeForAnyProperty<ITranscoder>((s, c, p, v) => Refresh());
            CoreData.Subscriptions.SubscribeForAnyProperty<ISettings>((s, c, p, v) => Refresh());

            Refresh();
        }

        private void Refresh()
        {
            _transId = CoreData.Root.Transcoders.First().Key;
            var transcoder = CoreData.Root.Transcoders[_transId];
            if (transcoder != Transcoder.Value)
                Transcoder.Value = transcoder;

            var settings = CoreData.Root.Settings;
            var claims = _connectionService.Claims;
            var list = GetAllChannels().ToList();

            ListHelper.UpdateCollectionNoId(list.Where(l => IsTranscoded(l)).ToList(), Transcoded, (a, b) => a == b.Source, (t) => CreateModel(t));
            ListHelper.UpdateCollectionNoId(list.Where(l => !IsTranscoded(l)).ToList(), NoneTranscoded, (a, b) => a == b.Source, (t) => CreateModel(t));

            NoneTranscodedChangeDisabled.Value = settings.StreamingToCloudStarted || settings.IsRecordingRequested;
            TranscodedChangeDisabled.Value = settings.StreamingToCloudStarted && list.Where(l => IsTranscoded(l)).Any(s => s.IsOn);

            OriginalFpsCurrent.SilentValue = OriginalFpss.FirstOrDefault(s => (int)s.Value == settings.Fps);
            OriginalResolutionCurrent.SilentValue = OriginalResolutions.FirstOrDefault(s => ((Resolution)s.Value).Equals(settings.Resolution));

            TranscodedDescription.Value = $"{transcoder.Resolution.Width}x{transcoder.Resolution.Height}x{transcoder.Fps}fps @ {transcoder.Bitrate}kbps";
            OriginalDescription.Value = $"{settings.Resolution.Width}x{settings.Resolution.Height}x{settings.Fps}fps @ {settings.Bitrate}kbps";

            TranscodingMessageType message = TranscodingMessageType.None;
            if (!TranscodingEnabled)
                message = TranscodingMessageType.TranscodingDisabled;
            else if (settings.Resolution.Height > claims.TranscoderInputLimit.Height)
                message = TranscodingMessageType.HighInputResolution;
            else if (settings.Fps > claims.TranscoderInputLimit.Fps)
                message = TranscodingMessageType.HighInputFps;
            else if (settings.Resolution?.Width < transcoder.Resolution?.Width ||
                                    settings.Fps < transcoder.Fps ||
                                    settings.Bitrate < transcoder.Bitrate)
                message = TranscodingMessageType.IncreasedQuality;

            Message.Value = message;
        }

        private IEnumerable<IChannel> GetAllChannels() => CoreData.Root.Channels.Values.Concat(new[] { CoreData.Root.Settings.ChannelBeingCreated }).Where(s => s != null);

        private TranscodingChannelModel CreateModel(IChannel c)
        {
            var channelName = c.Name;
            if (channelName == null)
            {
                if (!string.IsNullOrEmpty(c.TargetId) && CoreData.Root.Targets.TryGetValue(c.TargetId, out var target))
                    channelName = target.Name;
                else
                    channelName = "Custom";
            }

            return new TranscodingChannelModel
            {
                Id = CoreData.GetId(c),
                Name = channelName,
                Source = c,
                Move = () => ToggleChannel(c)
            };
        }

        private void ToggleChannel(IChannel c)
        {
            SetTranscoding(c, !IsTranscoded(c));
        }

        public bool IsTranscoded(IChannel channel)
        {
            if (_transId == null || !TranscodingEnabled)
                return false;

            return channel.TranscoderId == _transId;
        }

        internal void SetTranscoding(IChannel source, bool val)
        {
            if (TranscodingEnabled)
                source.TranscoderId = val ? _transId : null;
        }
    }

    public enum TranscodingMessageType
    {
        None,
        IncreasedQuality,
        TranscodingDisabled,
        HighInputFps,
        HighInputResolution,
    }

    public class TrascodingComboboxValue
    {
        public string Name { get; set; }

        public bool Good { get; set; }

        public object Value { get; set; }
    }

    public class TranscodingChannelModel
    {
        public IChannel Source { get; set; }

        public string Name { get; set; }

        public Action Move { get; set; }

        public string Id { get; internal set; }
    }
}
