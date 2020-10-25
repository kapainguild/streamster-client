using Streamster.ClientCore.Cross;
using Streamster.ClientData.Model;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Streamster.ClientCore.Models
{
    public class MainIndicatorsModel
    {
        private readonly CoreData _coreData;
        private readonly MainStreamerModel _mainStreamerModel;

        public ObservableCollection<DeviceIndicatorsModel> Devices { get; } = new ObservableCollection<DeviceIndicatorsModel>();

        public MainIndicatorsModel(CoreData coreData,
            MainStreamerModel mainStreamerModel,
            ICpuService cpuService // for precreation
            )
        {
            _coreData = coreData;
            _mainStreamerModel = mainStreamerModel;
            _coreData.Subscriptions.SubscribeForProperties<IIndicatorCpu>(s => s.Enabled, (o, c, p) => Refresh(o, (d, i) => Reset(d.Cpu, i)));
            _coreData.Subscriptions.SubscribeForProperties<IIndicatorCpu>(s => s.Load, (o, c, p) => Refresh(o, RefreshCpuLoad));
            _coreData.Subscriptions.SubscribeForProperties<IIndicatorCpu>(s => s.Top, (o, c, p) => Refresh(o, RefreshCpuTop));

            _coreData.Subscriptions.SubscribeForProperties<IIndicatorCloudOut>(s => s.Enabled, (o, c, p) =>
            
                Refresh(o, (d, i) =>
                {
                    Reset(d.CloudOut, i);
                    if (!i.Enabled)
                        _mainStreamerModel.SetActualBitrate(0, IndicatorState.Unknown);
                    })
                );

            _coreData.Subscriptions.SubscribeForProperties<IIndicatorCloudOut>(s => s.Bitrate, (o, c, p) => Refresh(o, RefreshCloudOutBitrate));
            _coreData.Subscriptions.SubscribeForProperties<IIndicatorCloudOut>(s => s.Errors, (o, c, p) => Refresh(o, RefreshCloudOut));

            _coreData.Subscriptions.SubscribeForProperties<IIndicatorCloudIn>(s => s.Enabled, (o, c, p) => Refresh(o, (d, i) => Reset(d.CloudIn, i)));
            _coreData.Subscriptions.SubscribeForProperties<IIndicatorCloudIn>(s => s.Bitrate, (o, c, p) => Refresh(o, RefreshCloudInBitrate));
            _coreData.Subscriptions.SubscribeForProperties<IIndicatorCloudIn>(s => s.Errors, (o, c, p) => Refresh(o, RefreshCloudIn));

            _coreData.Subscriptions.SubscribeForProperties<IIndicatorEncoder>(s => s.Enabled, (o, c, p) => Refresh(o, (d, i) => { Reset(d.Encoder, i); Reset(d.Input, i); }));
            _coreData.Subscriptions.SubscribeForProperties<IIndicatorEncoder>(s => s.InputFps, (o, c, p) => Refresh(o, RefreshInputFps));
            _coreData.Subscriptions.SubscribeForProperties<IIndicatorEncoder>(s => s.InputErrors, (o, c, p) => Refresh(o, RefreshInput));
            _coreData.Subscriptions.SubscribeForProperties<IIndicatorEncoder>(s => s.InputTargetFps, (o, c, p) => Refresh(o, RefreshInput));
            _coreData.Subscriptions.SubscribeForProperties<IIndicatorEncoder>(s => s.QueueSize, (o, c, p) => Refresh(o, RefreshEncoder));
            _coreData.Subscriptions.SubscribeForProperties<IChannel>(s => s.Bitrate, (o, c, p) => RefreshChannel(o));
            _coreData.Subscriptions.SubscribeForProperties<IChannel>(s => s.State, (o, c, p) => RefreshChannel(o));

            _coreData.Subscriptions.SubscribeForProperties<IDevice>(s => s.State, (o, c, p) => RefreshDevicesStates(o));

        }

        private void RefreshDevicesStates(IDevice o)
        {
            var id = _coreData.GetId(o);

            var dev = Devices.FirstOrDefault(s => s.DeviceId == id);
            if (dev != null)
                dev.Offline.Value = o.State != DeviceState.Online;

            UpdateDeviceNames();
        }

        private void RefreshChannel(IChannel channel)
        {
            foreach(var dev in Devices)
            {
                if (dev.CloudOut.State.Value == IndicatorState.Unknown)
                {
                    if (dev.Restream.State.Value != IndicatorState.Unknown)
                        dev.Restream.State.Value = IndicatorState.Unknown;
                }
                else
                {
                    var channels = _coreData.Root.Channels.Values.Where(s => s.State != ChannelState.Idle).Select(s => new IndicatorModelRestreamChannel
                    {
                        Name = GetChannelName(s),
                        State = s.State,
                        Bitrate = s.Bitrate
                    }).ToArray();

                    var restream = dev.Restream;
                    int allCount = channels.Length;

                    if (allCount == 0)
                    {
                        if (dev.Restream.State.Value != IndicatorState.Unknown)
                            dev.Restream.State.Value = IndicatorState.Unknown;
                    }
                    else 
                    { 
                        restream.Channels.Value = channels;
                        int badCount = channels.Count(s => s.State != ChannelState.RunningOk);

                        if (badCount == 0)
                        {
                            restream.State.Value = IndicatorState.Ok;
                            restream.DetailedDescription.Value = "Restreaming works fine";
                        }
                        else
                        {
                            restream.State.Value = IndicatorState.Error;
                            restream.DetailedDescription.Value = $"{badCount} restreaming target(s) failed";
                        }

                        restream.Value.Value = $"{allCount - badCount}/{allCount}";
                        restream.ChartModel.AddValue(channels.Sum(s => s.Bitrate) / 1000.0, 4.0);
                    }
                }

            }
        }

        private string GetChannelName(IChannel s)
        {
            if (s.Name != null)
                return s.Name;

            string targetId = s.TargetId;
            if (targetId == null)
                return "Custom";

            if (_coreData.Root.Targets.TryGetValue(targetId, out var target))
                return target.Name;

            return "?";
        }

        private void RefreshCloudOutBitrate(DeviceIndicatorsModel localDevice, IIndicatorCloudOut cloudOut)
        {
            var ave = localDevice.CloudOut.AverageBitrate.AddValue(cloudOut.Bitrate);
            var configured = _coreData.Settings.Bitrate;
            localDevice.CloudOut.ChartModel.AddValue(ave / 1000.0, configured / 1000.0);
            
            RefreshCloudOut(localDevice, cloudOut);
        }

        private void RefreshCloudInBitrate(DeviceIndicatorsModel localDevice, IIndicatorCloudIn cloudIn)
        {
            var ave = localDevice.CloudIn.AverageBitrate.AddValue(cloudIn.Bitrate);
            var configured = _coreData.Settings.Bitrate;
            localDevice.CloudIn.ChartModel.AddValue(ave / 1000.0, configured / 1000.0); 

            RefreshCloudIn(localDevice, cloudIn);
        }

        private void RefreshCloudOut(DeviceIndicatorsModel localDevice, IIndicatorCloudOut cloudOut)
        {
            var r = localDevice.CloudOut;
            if (r.AverageBitrate.TryGetAverage(out var bitrate))
            {
                var configured = _coreData.Settings.Bitrate;
                r.AverageBitrate.TryGetLast(out var lastBitrate);

                if (cloudOut.Errors > 0)
                {
                    r.State.Value = IndicatorState.Error;
                    if (lastBitrate > 0)
                        r.DetailedDescription.Value = "Stream to cloud is UNSTABLE";
                    else
                        r.DetailedDescription.Value = "Stream to cloud FAILED";
                    r.Value.Value = $"E";
                    r.SmallValue.Value = null;
                }
                else
                {
                    r.Value.Value = $"{bitrate / 1000}";
                    r.SmallValue.Value = $".{(bitrate % 1000) / 100}";
                    var bitratePercent = (bitrate * 100) / _coreData.Settings.Bitrate;

                    if (bitratePercent < 60)
                    {
                        r.DetailedDescription.Value = "Bitrate is VERY low";
                        r.State.Value = IndicatorState.Error;
                    }
                    else if (bitratePercent < 80)
                    {
                        r.State.Value = IndicatorState.Warning;
                        r.DetailedDescription.Value = "Bitrate is lower then requested";
                    }
                    else
                    {
                        r.DetailedDescription.Value = "Stream to cloud is Ok";
                        r.State.Value = IndicatorState.Ok;
                    }
                }
                _mainStreamerModel.SetActualBitrate(bitrate, r.State.Value);
            }
        }

        private void RefreshCloudIn(DeviceIndicatorsModel localDevice, IIndicatorCloudIn cloudIn)
        {
            var r = localDevice.CloudIn;
            if (r.AverageBitrate.TryGetAverage(out var bitrate))
            {
                var baseBitrate = _coreData.Settings.Bitrate;
                var sender = _coreData.Root.Devices.Values.FirstOrDefault(s => s.KPIs?.CloudOut?.Bitrate > 0);
                if (sender != null)
                    baseBitrate = sender.KPIs.CloudOut.Bitrate;

                r.ChartModel.AddValue(bitrate / 1000.0, 0.0); // TODO: change to average

                if (cloudIn.Errors > 0)
                {
                    r.State.Value = IndicatorState.Error;
                    if (bitrate > 0)
                        r.DetailedDescription.Value = "Stream from cloud is UNSTABLE";
                    else
                        r.DetailedDescription.Value = "Stream from cloud FAILED";
                    r.Value.Value = $"E";
                    r.SmallValue.Value = null;
                }
                else
                {
                    r.Value.Value = $"{bitrate / 1000}";
                    r.SmallValue.Value = $".{(bitrate % 1000) / 100}";
                    var bitratePercent = (bitrate * 100) / baseBitrate;

                    if (bitratePercent < 60)
                    {
                        r.DetailedDescription.Value = "Bitrate is VERY low";
                        r.State.Value = IndicatorState.Error;
                    }
                    else if (bitratePercent < 80)
                    {
                        r.State.Value = IndicatorState.Warning;
                        r.DetailedDescription.Value = "Bitrate is lower then expected";
                    }
                    else
                    {
                        r.DetailedDescription.Value = "Stream from cloud is Ok";
                        r.State.Value = IndicatorState.Ok;
                    }
                }
            }
        }

        private void RefreshInputFps(DeviceIndicatorsModel device, IIndicatorEncoder input)
        {
            device.Input.ChartModel.AddValue(input.InputFps, 0);
            RefreshInput(device, input);
        }

        private void RefreshInput(DeviceIndicatorsModel device, IIndicatorEncoder input)
        {
            var model = device.Input;

            if (input.InputErrors > 0)
            {
                if (input.InputFps > 0)
                    model.DetailedDescription.Value = "Video or audio source UNSTABLE";
                else
                    model.DetailedDescription.Value = "Video or audio source FAILED";

                model.State.Value = IndicatorState.Error;
                model.Value.Value = "E";
            }
            else if (input.InputFps == 0)
            {
                model.DetailedDescription.Value = "Camera does not provide frames";
                model.State.Value = IndicatorState.Error;
                model.Value.Value = "E";
            }
            else
            {
                var baseFps = input.InputTargetFps;
                if (baseFps > 0 && (input.InputFps * 100 / baseFps) < 80)
                {
                    model.DetailedDescription.Value = "Camera does not provide requested FPS";
                    model.Value.Value = "W";
                    model.State.Value = IndicatorState.Warning;
                }
                else
                {
                    model.DetailedDescription.Value = "Camera and audio work fine";
                    model.State.Value = IndicatorState.Ok;
                    model.Value.Value = "ok";
                }
            }
        }


        private void RefreshEncoder(DeviceIndicatorsModel device, IIndicatorEncoder input)
        {
            var encoder = device.Encoder;

            var queueSize = input.QueueSize;
            if (queueSize > 8)
            {
                encoder.State.Value = IndicatorState.Error;
                encoder.DetailedDescription.Value = "Encoder is overloaded";
                encoder.Value.Value = "E";
            }
            else if (queueSize > 4)
            {
                encoder.State.Value = IndicatorState.Warning;
                encoder.DetailedDescription.Value = "Encoder is under high load";
                encoder.Value.Value = "W";
            }
            else
            {
                encoder.State.Value = IndicatorState.Ok;
                encoder.DetailedDescription.Value = "Encoder works fine";
                encoder.Value.Value = "ok";
            }

            encoder.ChartModel.AddValue(queueSize, 12);
        }

        private void RefreshCpuLoad(DeviceIndicatorsModel device, IIndicatorCpu input)
        {
            var cpu = device.Cpu;
            var cpuAverage = cpu.AverageCpu.AddValue(input.Load);
            cpu.Value.Value = cpuAverage.ToString();
            cpu.ChartModel.AddValue(cpuAverage, 100);

            if (cpuAverage < 75)
            {
                cpu.State.Value = IndicatorState.Ok;
                cpu.DetailedDescription.Value = $"CPU load {cpuAverage}% is normal";
            }
            else if (cpuAverage < 90)
            {
                cpu.State.Value = IndicatorState.Warning;
                cpu.DetailedDescription.Value = $"CPU load {cpuAverage}% is ABOVE normal";
            }
            else
            {
                cpu.State.Value = IndicatorState.Error;
                cpu.DetailedDescription.Value = $"CPU load {cpuAverage}% is OVERLOADED";
            }
        }

        private void RefreshCpuTop(DeviceIndicatorsModel device, IIndicatorCpu input) => device.Cpu.Processes.Value = input.Top;

        private void Refresh<T>(T changedKpi, Action<DeviceIndicatorsModel, T> updater) where T : IIndicatorBase
        {
            var kpis = _coreData.GetParent<IDeviceIndicators>(changedKpi);
            var device = _coreData.GetParent<IDevice>(kpis);
            var id = _coreData.GetId(device);

            var localDevice = Devices.FirstOrDefault(s => s.DeviceId == id);
            if (localDevice == null)
            {
                localDevice = new DeviceIndicatorsModel { DeviceId = id };
                if (id != _coreData.ThisDeviceId)
                    Devices.Insert(0, localDevice);
                else
                    Devices.Add(localDevice);

                UpdateDeviceNames();
            }

            updater(localDevice, changedKpi);
        }

        private void UpdateDeviceNames()
        {
            foreach (var device in Devices)
            {
                if (Devices.Count <= 1)
                    device.Name.Value = null;
                else
                    device.Name.Value = device.DeviceId == _coreData.ThisDeviceId ? "local" : "remote";
            }
        }

        private void Reset(IndicatorModelBase localIndicator, IIndicatorBase input)
        {
            if (!input.Enabled)
                localIndicator.Reset();
        }
    }

}
