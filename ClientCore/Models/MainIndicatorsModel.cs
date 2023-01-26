using Streamster.ClientCore.Cross;
using Streamster.ClientData;
using Streamster.ClientData.Model;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Streamster.ClientCore.Models
{
    public class MainIndicatorsModel
    {
        private readonly CoreData _coreData;
        private readonly StreamSettingsModel _streamSettings;

        public ObservableCollection<DeviceIndicatorsModel> Devices { get; } = new ObservableCollection<DeviceIndicatorsModel>();

        public MainIndicatorsModel(CoreData coreData,
            StreamSettingsModel streamSettings,
            ICpuService cpuService // for precreation
            )
        {
            _coreData = coreData;
            _streamSettings = streamSettings;

            _coreData.Subscriptions.SubscribeForProperties<IIndicatorCpu>(s => s.Load, (o, c, p) => Refresh(o, (d, i) => d.Cpu.ChartModel.AddValue(i.Load, 100)));
            _coreData.Subscriptions.SubscribeForAnyProperty<IIndicatorCpu>((o, c, p, _) => Refresh(o, RefreshCpu));

            _coreData.Subscriptions.SubscribeForProperties<IIndicatorCloudOut>(s => s.Bitrate, (o, c, p) => Refresh(o, (d, i) => d.CloudOut.ChartModel.AddValue(i.Bitrate / 1000.0, _coreData.Settings.Bitrate / 1000.0)));
            _coreData.Subscriptions.SubscribeForAnyProperty<IIndicatorCloudOut>((o, c, p, _) => Refresh(o, RefreshCloudOut));


            _coreData.Subscriptions.SubscribeForProperties<IIndicatorCloudIn>(s => s.Bitrate, (o, c, p) => Refresh(o, (d, i) => d.CloudIn.ChartModel.AddValue(i.Bitrate / 1000.0, _coreData.Settings.Bitrate / 1000.0)));
            _coreData.Subscriptions.SubscribeForAnyProperty<IIndicatorCloudIn>((o, c, p, _) => Refresh(o, RefreshCloudIn));

            _coreData.Subscriptions.SubscribeForProperties<IIndicatorEncoder>(s => s.State, (o, c, p) => Refresh(o, RefreshEncoderState));
            _coreData.Subscriptions.SubscribeForProperties<IIndicatorEncoder>(s => s.Data, (o, c, p) => Refresh(o, RefreshEncoderData));

            _coreData.Subscriptions.SubscribeForProperties<IChannel>(s => s.Bitrate, (o, c, p) => RefreshChannel(o));
            _coreData.Subscriptions.SubscribeForProperties<IChannel>(s => s.State, (o, c, p) => RefreshChannel(o));

            _coreData.Subscriptions.SubscribeForProperties<IDevice>(s => s.State, (o, c, p) => RefreshDevicesStates(o));

            _coreData.Subscriptions.SubscribeForProperties<IIndicatorVpn>(s => s.Sent, (o, c, p) => Refresh(o, (m, i) => m.Vpn.ChartModel.AddValue(i.Sent / 1000.0, 10)));
            _coreData.Subscriptions.SubscribeForProperties<IIndicatorVpn>(s => s.Received, (o, c, p) => Refresh(o, (m, i) => m.Vpn.Received.AddValue(i.Received / 1000.0, 10)));
            _coreData.Subscriptions.SubscribeForProperties<IIndicatorVpn>(s => s.State, (o, c, p) => Refresh(o, RefreshVpnState));
        }

        private void RefreshVpnState(DeviceIndicatorsModel local, IIndicatorVpn data)
        {
            Reset(local.Vpn, data);

            var vpn = local.Vpn;
            vpn.State.Value = data.State;

            vpn.DetailedDescription.Value = vpn.State.Value switch
            {
                IndicatorState.Ok => "VPN established and works",
                IndicatorState.Warning => "Connecting...",
                IndicatorState.Error => "Failed. VPN is reconnecting...",
                _ => "?"
            };

            vpn.Value.Value = vpn.State.Value switch
            {
                IndicatorState.Ok => "ok",
                IndicatorState.Warning => "...",
                IndicatorState.Error => "E",
                _ => "?"
            };
        }

        private void RefreshDevicesStates(IDevice o)
        {
            var id = _coreData.GetId(o);

            var dev = Devices.FirstOrDefault(s => s.DeviceId == id);
            if (dev != null)
            {
                if (o.State != DeviceState.Online)
                    dev.Offline.Value = DeviceIndicatorsState.Offline;
                else if (o.Type == ClientConstants.WebClientId)
                    dev.Offline.Value = DeviceIndicatorsState.Online;
                else
                    dev.Offline.Value = DeviceIndicatorsState.Indicators;
            }

            UpdateDeviceNames();
        }

        private void RefreshChannel(IChannel channel)
        {
            foreach(var dev in Devices)
            {
                if (dev.CloudOut.State.Value == IndicatorState.Disabled)
                {
                    if (dev.Restream.State.Value != IndicatorState.Disabled)
                        dev.Restream.State.Value = IndicatorState.Disabled;
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
                        if (dev.Restream.State.Value != IndicatorState.Disabled)
                            dev.Restream.State.Value = IndicatorState.Disabled;
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

        private void RefreshCloudOut(DeviceIndicatorsModel localDevice, IIndicatorCloudOut cloudOut)
        {
            var state = cloudOut.State;

            var bitrate = state == IndicatorState.Disabled ? 0 : cloudOut.Bitrate;
            _streamSettings.SetActualBitrate(bitrate, state, localDevice);

            var r = localDevice.CloudOut;
            r.State.Value = state;

            if (state == IndicatorState.Ok || state == IndicatorState.Warning || state == IndicatorState.Warning2)
            {
                r.Value.Value = $"{bitrate / 1000}";
                r.SmallValue.Value = $".{(bitrate % 1000) / 100}";
            }
            else
            {
                r.Value.Value = $"E";
                r.SmallValue.Value = null;
            }

            r.DetailedDescription.Value = state switch
            {
                IndicatorState.Ok => "Stream to cloud is Ok",
                IndicatorState.Warning => "Bitrate is lower then requested",
                IndicatorState.Warning2 => "Bitrate is VERY low",
                IndicatorState.Error => "Stream to cloud is UNSTABLE",
                IndicatorState.Error2 => "Stream to cloud FAILED",
                _ => "?"
            };
        }

        private void RefreshCloudIn(DeviceIndicatorsModel localDevice, IIndicatorCloudIn cloudIn)
        {
            var state = cloudIn.State;
            var bitrate = state == IndicatorState.Disabled ? 0 : cloudIn.Bitrate;

            var r = localDevice.CloudIn;
            r.State.Value = state;

            if (state == IndicatorState.Ok || state == IndicatorState.Warning)
            {
                r.Value.Value = $"{bitrate / 1000}";
                r.SmallValue.Value = $".{(bitrate % 1000) / 100}";
            }
            else
            {
                r.Value.Value = $"E";
                r.SmallValue.Value = null;
            }

            r.DetailedDescription.Value = state switch
            {
                IndicatorState.Ok => "Stream from cloud is Ok",
                IndicatorState.Warning => "Bitrate from cloud is lower than expected",
                IndicatorState.Error => "Stream from cloud is failed",
                _ => "?"
            };
        }

        private void RefreshEncoderData(DeviceIndicatorsModel device, IIndicatorEncoder input)
        {
            if (input?.Data != null)
            {
                device.Encoder.ChartModel.AddValue(input.Data.Q, 12);
                device.Encoder.OutputFps.AddValue(input.Data.O, _coreData.Settings.Fps);
            }

            RefreshEncoderState(device, input);
        }

        private void RefreshEncoderState(DeviceIndicatorsModel device, IIndicatorEncoder input)
        {
            var encoder = device.Encoder;
            encoder.State.Value = input.State;
            encoder.Value.Value = encoder.State.Value switch
            {
                IndicatorState.Ok => "ok",
                IndicatorState.Warning => "W",
                IndicatorState.Warning2 => "W",
                IndicatorState.Error => "E",
                IndicatorState.Error2 => "E",
                _ => "?"
            };

            encoder.DetailedDescription.Value = encoder.State.Value switch
            {
                IndicatorState.Ok => "Inputs and Encoder work Ok",
                IndicatorState.Warning => "FPS is low. Encoder may be overloaded.",
                IndicatorState.Error => "Encoder is overloaded",
                IndicatorState.Error2 => "One or more video/audio sources failed",
                _ => "?"
            };
        }

        private void RefreshCpu(DeviceIndicatorsModel device, IIndicatorCpu input)
        {
            var cpu = device.Cpu;
            var load = input.Load;
            cpu.Value.Value = load.ToString();
            cpu.State.Value = input.State;

            cpu.DetailedDescription.Value = cpu.State.Value switch
            {
                IndicatorState.Ok => $"CPU load {load}% is normal",
                IndicatorState.Warning => $"CPU load {load}% is ABOVE normal",
                IndicatorState.Error => $"CPU load {load}% is OVERLOADED",
                _ => "?"
            };
            device.Cpu.Processes.Value = input.Top;
        }

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
                else if (device.DeviceId == _coreData.ThisDeviceId)
                    device.Name.Value = "local";
                else if (_coreData.Root.Devices.TryGetValue(device.DeviceId, out var d))
                    device.Name.Value = StreamingSourcesModel.GetShortName(d);
                else
                    device.Name.Value = "???";
            }
        }

        private void Reset(IndicatorModelBase localIndicator, IIndicatorBase input)
        {
            if (input.State == IndicatorState.Disabled)
                localIndicator.Reset();
        }
    }

}
