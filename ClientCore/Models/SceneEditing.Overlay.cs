using DeltaModel;
using Streamster.ClientData.Model;
using System;
using System.Diagnostics;
using System.Linq;

namespace Streamster.ClientCore.Models
{
    public class OverlayPage : IEditingPage, IDisposable
    {
        private readonly SceneEditingModel _model;
        private readonly ISceneItem _editedItem;
        private Subscription _subscription;

        public Property<OverlayDesc[]> Overlays { get; } = new Property<OverlayDesc[]>();

        public Property<OverlayDesc> CurrentOverlay { get; } = new Property<OverlayDesc>();

        public Property<bool> NoOverlays { get; } = new Property<bool>();

        public Action GoToOverlayEditing { get; }

        public OverlayPage(SceneEditingModel model, ISceneItem editedItem) 
        {
            _model = model;
            _editedItem = editedItem;

            GoToOverlayEditing = () =>
            {
                var startInfo = new ProcessStartInfo("https://app.streamster.io/overlays") { UseShellExecute = true };
                Process.Start(startInfo);
            };

            CurrentOverlay.OnChange = (o, n) => OverlaySelected(n);

            _subscription = _model.CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.Overlays, (a, b, c) => RefreshFromModel());
            RefreshFromModel();
        }

        public void UpdateContent(EditingUpdateType updateType)
        {
            if (updateType == EditingUpdateType.Content)
                RefreshFromModel();
        }
        
        private void RefreshFromModel()
        {
            var overlays = _model.CoreData.Root.Settings.Overlays;
            Overlays.Value = _model.CoreData.Root.Settings.Overlays;
            NoOverlays.Value = overlays == null || overlays.Length == 0;
            if (_editedItem != null)
            {
                CurrentOverlay.Value = _model.CoreData.Root.Settings.Overlays?.FirstOrDefault(s => s.Id == _editedItem.Source.Overlay.Id);
            }
        }

        private void OverlaySelected(OverlayDesc n)
        {
            if (n != null)
            {
                var source = new SceneItemSource { Overlay = new SceneItemSourceOverlay { Id = n.Id } };
                if (_editedItem != null)
                    _editedItem.Source = source;
                else
                    _model.AddSourceToScene(source, true);
            }
        }

        public static string GetName(SceneItemSourceOverlay overlay, CoreData coreData)
        {
            var desc = coreData.Root.Settings.Overlays?.FirstOrDefault(s => s.Id == overlay.Id);
            if (!string.IsNullOrEmpty(desc?.Name))
                return desc?.Name;
            return "??overlay??";
        }

        public void Dispose()
        {
            if (_subscription != null)
            {
                _subscription.Unsubscribe();
                _subscription = null;
            }
        }
    }

}
