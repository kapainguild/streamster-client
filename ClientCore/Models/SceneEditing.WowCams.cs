using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Streamster.ClientCore.Models
{
    public class WowCamsPage : IEditingPage, IDisposable
    {
        private SceneEditingModel _model;
        private ISceneItem _editedItem;

        public ObservableCollection<DeviceItemModel> Cameras { get; } = new ObservableCollection<DeviceItemModel>();

        public Property<DeviceItemModel> SelectedCamera { get; } = new Property<DeviceItemModel>();

        public Property<int> Tips { get; } = new Property<int>(25);

        public Property<DurationDesc> DurationSec { get; } = new Property<DurationDesc>();

        public List<int> TipsValues { get; } = new List<int>() { 1, 2, 5, 10, 25, 50, 100, 200, 500};

        public string LearnUrl { get; } = "https://docs.streamster.io/docs/add-ons/remote-camera-switch-8472/";

        public List<DurationDesc> DurationValues { get; } = new List<DurationDesc>() 
        { new DurationDesc(5, "5 seconds"), new DurationDesc(10, "10 seconds"), new DurationDesc(15, "15 seconds"),
            new DurationDesc(30, "30 seconds"),new DurationDesc(45, "45 seconds"),
            new DurationDesc(60, "1 minute"), new DurationDesc(120, "2 minutes"), new DurationDesc(300, "5 minutes"), new DurationDesc(600, "10 minutes")};

        public WowCamsPage(SceneEditingModel model, ISceneItem editedItem)
        {
            _model = model;
            _editedItem = editedItem;

            SelectedCamera.OnChange = (o, n) => SelectCamera();
            DurationSec.SilentValue = DurationValues.FirstOrDefault(s => s.Value == 30);

            UpdateFromModel();

            UpdateToModel();
        }

        internal static void GoToCreate(SceneEditingModel model)
        {
            var source = new SceneItemSource { WowCams = new SceneItemSourceWowCams() };

            var items = model.SceneState.Scene.Items;
            var camera = items.Values.OrderByDescending(s => s.ZOrder).FirstOrDefault(s => s.Source.Device != null);

            if (camera != null)
            {
                int zOrder = camera.ZOrder + 1;
                foreach (var item in items.Values)
                {
                    if (item.ZOrder >= zOrder)
                        item.ZOrder = item.ZOrder++;
                }
                model.AddSourceToScene(source, false, null, zOrder);
            }
            else
                model.AddSourceToScene(source, false);
        }

        private SceneItemSource GetModel(bool active = true)
        {
            var deviceId = SelectedCamera.Value?.Id;
            if (deviceId != null &&
                _model.SceneState.Device.VideoInputs.TryGetValue(deviceId, out var input))
            {
                return new SceneItemSource
                {
                    WowCams = new SceneItemSourceWowCams
                    {
                        DeviceName = new DeviceName { DeviceId = deviceId, Name = input.Name },
                        DurationSec = DurationSec.Value?.Value ?? 30,
                        Tips = Tips.Value,
                        Active = active,
                    }
                };
            }
            return null;
        }

        private void SelectCamera()
        {
            var model = GetModel();
            if (model != null)
            {
                if (_editedItem != null)
                    _editedItem.Source = model;
                else
                {
                    var items = _model.SceneState.Scene.Items;
                    var baseCamera = items.Values.OrderByDescending(s => s.ZOrder).FirstOrDefault(s => s.Source.Device != null);

                    if (baseCamera != null)
                    {
                        int zOrder = baseCamera.ZOrder + 1;
                        foreach (var item in items.Values)
                        {
                            if (item.ZOrder >= zOrder)
                                item.ZOrder = item.ZOrder++;
                        }
                        _model.AddSourceToScene(model, false, null, zOrder);
                    }
                    else
                        _model.AddSourceToScene(model, false);
                }
            }
        }

        private void UpdateFromModel()
        {
            UpdateCameras();

            if (_editedItem != null)
            {
                DurationSec.SilentValue = DurationValues.FirstOrDefault(s => s.Value == (_editedItem.Source.WowCams?.DurationSec ?? 30));
                Tips.SilentValue = _editedItem.Source.WowCams?.Tips ?? 40;

                var configuredDeviceId = _editedItem.Source.WowCams?.DeviceName?.DeviceId;
                if (configuredDeviceId == null)
                    SelectedCamera.SilentValue = null;
                else
                    SelectedCamera.SilentValue = Cameras.FirstOrDefault(s => s.Id == configuredDeviceId);
            }
        }

        private void UpdateCameras()
        {
            var usedCameraIds = _model.SceneState.Scene.Items.Values.
                            Where(s => s != _editedItem).
                            Select(s => s.Source?.Device?.DeviceName?.DeviceId ?? s.Source?.WowCams?.DeviceName?.DeviceId).
                            Where(s => s != null).ToList();

            ListHelper.UpdateCollection(_model.CoreData,
                _model.SceneState.Device.VideoInputs.Values.OrderBy(s => s.Name).ToList(),
                Cameras,
                s => s.Id,
                (s, id) => new DeviceItemModel(id,
                                s,
                                usedCameraIds.Contains(id) ? DeviceItemModelState.InUse : DeviceItemModelState.Normal,
                                null, null),
                (s) => s.Model);
        }

        public void UpdateContent(EditingUpdateType updateType)
        {
            if (updateType == EditingUpdateType.Content)
                UpdateFromModel();
        }

        public void UpdateToModel()
        {
            if (_editedItem != null)
                _editedItem.Source = GetModel();
        }

        public void Dispose()
        {
            if (_editedItem != null)
                _editedItem.Source = GetModel(false);
        }
    }

    public record DurationDesc(int Value, string DisplayName);
}
