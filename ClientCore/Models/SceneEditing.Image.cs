using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Models
{
    public class ImagePage : IEditingPage
    {
        private SceneEditingModel _model;
        private ISceneItem _editedItem;

        public bool Editing => _editedItem != null;

        public Property<ImageModel> Current { get; } = new Property<ImageModel>();

        public Property<List<ImageModel>> Recent { get; } = new Property<List<ImageModel>>();

        public Action<string, byte[]> AddFile { get; }

        public Property<string> Message { get; } = new Property<string>();

        public ImagePage(SceneEditingModel model, ISceneItem editedItem)
        {
            _model = model;
            _editedItem = editedItem;

            AddFile = (s, b) => _ = AddResource(s, b);

            RefreshFromModel();
        }

        public void UpdateContent(EditingUpdateType updateType)
        {
            if (updateType == EditingUpdateType.Content)
                RefreshFromModel();
        }

        private void RefreshFromModel()
        {
            IResource ignore = null;
            if (Editing)
            {
                ImageModel model = null;
                var id = _editedItem.Source.Image.ResourceId;

                if (id != null && _model.CoreData.Root.Resources.TryGetValue(id, out var resource))
                {
                    var data = _model.ResourceService.GetResource(id);
                    if (data != null)
                    {
                        model = new ImageModel(resource.Info.Name, data, null);
                        ignore = resource;
                    }
                }
                Current.Value = model;
            }

            Recent.Value = _model.CoreData.Root.Resources.Values.
                                    OrderByDescending(s => s.LastUse).
                                    Where(s => s != ignore && (s.Info.Type == ResourceType.ImagePng || s.Info.Type == ResourceType.ImageJpeg)).
                                    Select(s => new { M = s, Data = _model.ResourceService.GetResource(_model.CoreData.GetId(s)) }).
                                    Where(s => s.Data != null).
                                    Take(5).
                                    Select(s => new ImageModel(s.M.Info.Name, s.Data, () => DoSelect(_model.CoreData.GetId(s.M)))).
                                    ToList();
        }


        private async Task AddResource(string fileName, byte[] data)
        {
            if (data == null)
            {
                Message.Value = "Bad image format";
                await Task.Delay(3500);
                Message.Value = null;
            }
            else
            {
                var id = _model.ResourceService.AddResource(fileName, data, ResourceType.ImageJpeg);
                UpdateOrCreateItem(id, data);
            }
        }

        private void DoSelect(string id)
        {
            if (_model.CoreData.Root.Resources.TryGetValue(id, out var resource))
                resource.LastUse = DateTime.UtcNow;

            UpdateOrCreateItem(id, _model.ResourceService.GetResource(id));
        }

        private void UpdateOrCreateItem(string id, byte[] data)
        {
            var source = new SceneItemSource { Image = new SceneItemSourceImage { ResourceId = id } };
            if (Editing)
                _editedItem.Source = source;
            else
            {
                var size = _model.ImageHelper.GetSize(data);

                SceneRect rect = null;
                if (size.width > 0 && size.height > 0)
                {
                    var ratio = (double)size.width / (double)size.height;

                    var baseExtent = 0.5;
                    var baseRatio = 16.0 / 9.0;

                    if (ratio > baseRatio)
                        rect = new SceneRect { W = baseExtent, H = baseExtent * baseRatio / ratio };
                    else
                        rect = new SceneRect { W = baseExtent * ratio / baseRatio, H = baseExtent};
                }

                _model.AddSourceToScene(source, false, rect);
            }

        }

        internal static string GetName(SceneItemSourceImage image, CoreData coreData)
        {
            if (coreData.Root.Resources.TryGetValue(image.ResourceId, out var img))
                return img.Info.Name;

            return "?????";
        }
    }

    public record ImageModel(string Name, byte[] Data, Action Select);
}
