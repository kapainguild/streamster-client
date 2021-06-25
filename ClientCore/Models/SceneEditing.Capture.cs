using DynamicStreamer.Screen;
using Streamster.ClientData.Model;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Models
{
    public class CapturePage : IEditingPage
    {
        protected SceneEditingModel _model;
        private readonly Func<IDevice, CaptureSource[]> _sourceGetter;
        private readonly Func<ISceneItem, SceneItemSourceCapture> _getConfig;
        private readonly Action<SceneItemSource, SceneItemSourceCapture> _setConfig;
        private readonly ISceneItem _editing;

        public ObservableCollection<CaptureItemModel> Items { get; } = new ObservableCollection<CaptureItemModel>();

        public bool IsEditing => _editing != null;

        public Property<bool> CaptureCursor { get; } = new Property<bool>(true);

        public bool CursorSupported { get; }

        public CapturePage(SceneEditingModel model, ISceneItem editing, Func<IDevice, CaptureSource[]> sourceGetter, Func<ISceneItem, SceneItemSourceCapture> getConfig,
            Action<SceneItemSource, SceneItemSourceCapture> setConfig)
        {
            _model = model;
            _sourceGetter = sourceGetter;
            _getConfig = getConfig;
            _setConfig = setConfig;
            _editing = editing;

            CursorSupported = model.SceneState.Device.ApiContract >= ScreenCaptureManager.CursorSupported;

            CaptureCursor.OnChange = (o, n) => UpdateCursor();
            UpdateFromModel();
        }

        public void UpdateContent(EditingUpdateType updateType)
        {
            UpdateFromModel();
        }

        protected virtual void UpdateFromModel()
        {
            int position = 0;
            var sources = _sourceGetter(_model.SceneState.Device);
            foreach (var model in sources)
            {
                var vm = position < Items.Count ? Items[position] : default;
                if (vm == null || !vm.Source.Equals(model))
                {
                    var oldAnotherPosition = Items.Skip(position).FirstOrDefault(s => s.Source.Equals(model));
                    if (oldAnotherPosition != null)
                    {
                        Items.Remove(oldAnotherPosition);
                        Items.Insert(position, oldAnotherPosition);
                    }
                    else
                    {
                        Items.Insert(position, new CaptureItemModel { Source = model, Select = () => DoSelect(model) });
                    }
                }
                position++;
            }

            while (Items.Count > sources.Length)
                Items.RemoveAt(Items.Count - 1);

            if (_editing != null)
            {
                var config = _getConfig(_editing);

                CaptureCursor.SilentValue = config.CaptureCursor;

                var selected = Items.FirstOrDefault(s => s.Source.Name == config.Source.Name && s.Source.CaptureId == config.Source.CaptureId);
                if (selected == null)
                    selected = Items.FirstOrDefault(s => s.Source.Name == config.Source.Name);

                if (selected == null)
                {
                    selected = new CaptureItemModel { Source = config.Source, Select = () => DoSelect(config.Source) };
                    selected.NotFound.Value = true;
                    Items.Insert(0, selected);
                }

                Items.ToList().ForEach(s => s.IsSelected.Value = s == selected);
            }
        }

        protected void DoSelect(CaptureSource model)
        {
            var source = new SceneItemSource();
            _setConfig(source, new SceneItemSourceCapture { Source = model, CaptureCursor = CaptureCursor.Value });
            if (_editing != null)
                _editing.Source = source;
            else
                _model.AddSourceToScene(source, false, GetRect(model));
        }

        private SceneRect GetRect(CaptureSource model)
        {
            SceneRect rect = null;
            if (model.W > 0 && model.H > 0)
            {
                var ratio = (double)model.W / (double)model.H;

                var baseExtent = 0.5;
                var baseRatio = 16.0 / 9.0;

                if (ratio > baseRatio)
                    rect = new SceneRect { W = baseExtent, H = baseExtent * baseRatio / ratio };
                else
                    rect = new SceneRect { W = baseExtent * ratio / baseRatio, H = baseExtent };
            }
            return rect;
        }

        private void UpdateCursor()
        {
            if (_editing != null)
            {
                var selected = Items.FirstOrDefault(s => s.IsSelected.Value);
                if (selected != null)
                    DoSelect(selected.Source);
            }
        }
    }

    public class CapturePageDisplay : CapturePage
    {
        public Property<bool> DisplayItems { get; } = new Property<bool>();

        public CapturePageDisplay(SceneEditingModel model, ISceneItem editing) : 
            base(model, editing,
                d => d.Displays, 
                i => i.Source.CaptureDisplay, 
                (s, c) => s.CaptureDisplay = c)
        {
        }

        protected override void UpdateFromModel()
        {
            base.UpdateFromModel();

            DisplayItems.Value = !(Items.Count == 0 ||
                Items.Count == 1 && Items[0].IsSelected.Value && !Items[0].NotFound.Value);
        }

        public static void GoToCreate(SceneEditingModel model)
        {
            if (!CapturePageNotSupported.IsCaptureSupported(model))
                model.GoToPage(new CapturePageNotSupported());
            else
            {
                var items = model.SceneState.Device.Displays;
                if (items != null && items.Length > 0)
                {
                    var first = model.SceneState.Device.Displays[0];

                    var source = new SceneItemSource
                    {
                        CaptureDisplay = new SceneItemSourceCapture
                        {
                            CaptureCursor = true,
                            Source = first
                        }
                    };

                    model.AddSourceToScene(source);
                }
                else
                    model.SelectAddLayer();
            }
        }
    }

    public class CapturePageNotSupported : IEditingPage
    {
        public void UpdateContent(EditingUpdateType updateType) { }

        public static bool IsCaptureSupported(SceneEditingModel model) => model.SceneState.Device.ApiContract >= ScreenCaptureManager.CreateFromHandleSupported;
    }

    public class CapturePageWindow : CapturePage
    {
        public bool SelectSupported { get; }

        public Action Select { get; }

        public CapturePageWindow(SceneEditingModel model, ISceneItem editing) :
            base(model, editing, 
                d => d.Windows, 
                i => i.Source.CaptureWindow, 
                (s, c) => s.CaptureWindow = c)
        {
            SelectSupported = model.SceneState.IsLocal;

            Select = () => { _ = SelectAsync(); };
        }

        private async Task SelectAsync()
        {
            var res = await _model.Sources.SelectFromUi();
            if (res != null)
                DoSelect(res);
        }

        internal static void GoToCreate(SceneEditingModel model)
        {
            if (!CapturePageNotSupported.IsCaptureSupported(model))
                model.GoToPage(new CapturePageNotSupported());
            else
                model.GoToPage(new CapturePageWindow(model, null));
        }
    }

    public class CaptureItemModel
    {
        public Action Select { get; set; }

        public CaptureSource Source { get; set; }

        public Property<bool> IsSelected { get; } = new Property<bool>(false);

        public Property<bool> NotFound { get; } = new Property<bool>(false);
    }
}
