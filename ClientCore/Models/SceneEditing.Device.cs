using Serilog;
using Streamster.ClientData.Model;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Streamster.ClientCore.Models
{
    public record DeviceItemModel(string Id, IInputDevice Model, DeviceItemModelState State, Action Select, Property<bool> IsSelected);

    public enum DeviceItemModelState { Normal, InUse, Current }

    public class DevicePage : IEditingPage
    {
        public ObservableCollection<DeviceItemModel> Cameras { get; } = new ObservableCollection<DeviceItemModel>();
        public Property<DeviceItemModel> SelectedCamera = new Property<DeviceItemModel>();
        private readonly SceneEditingModel _model;
        private readonly ISceneItem _editedItem;

        public DevicePage(SceneEditingModel model, ISceneItem editedItem)
        {
            _model = model;
            _editedItem = editedItem;
            UpdateCameras();
        }

        public void UpdateContent(EditingUpdateType updateType)
        {
            UpdateCameras();
        }

        private void UpdateCameras()
        {
            var usedCameraIds = _model.SceneState.Scene.Items.Values.Where(s => s != _editedItem).Select(s => s.Source?.Device?.DeviceName?.DeviceId).Where(s => s != null).ToList();

            var selectedId = _editedItem?.Source.Device.DeviceName.DeviceId;

            ListHelper.UpdateCollection(_model.CoreData, 
                _model.SceneState.Device.VideoInputs.Values.OrderBy(s => s.Name).ToList(), 
                Cameras, 
                s => s.Id,
                (s, id) => new DeviceItemModel(id,
                                s,
                                id == selectedId ? DeviceItemModelState.Current : (usedCameraIds.Contains(id) ? DeviceItemModelState.InUse : DeviceItemModelState.Normal),
                                () => Select(id), new Property<bool>()),
                d => d.Model);

            if (_editedItem != null)
            {
                Cameras.ToList().ForEach(s => s.IsSelected.Value = _model.CoreData.GetId(s.Model) == selectedId);
            }
        }

        private void Select(string key)
        {
            if (_model.SceneState.Device.VideoInputs.TryGetValue(key, out var input))
            {
                var source = new SceneItemSource
                {
                    Device = new SceneItemSourceDevice { DeviceName = new DeviceName { DeviceId = key, Name = input.Name } }
                };

                if (_editedItem != null)
                    _editedItem.Source = source;
                else 
                    _model.AddSourceToScene(source);
            }
            else
                Log.Warning($"Unable to find selected video input {key}");
        }
    }



}
