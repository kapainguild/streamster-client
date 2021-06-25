using System;

namespace Streamster.ClientCore.Models
{
    public class AddLayerPage : IEditingPage
    {
        public Action Camera { get; }

        public Action Image { get; }

        public Action WebPage { get; }

        public Action CaptureDisplay { get; }

        public Action CaptureWindow { get; }

        public Action Lovense { get; }

        public bool LovenseVisible { get; }  

        public AddLayerPage(SceneEditingModel model)
        {
            Camera = () => model.GoToPage(new DevicePage(model, null));
            WebPage = () => model.GoToPage(new WebBrowserPageAdd(model));
            CaptureDisplay = () => CapturePageDisplay.GoToCreate(model);
            CaptureWindow = () => CapturePageWindow.GoToCreate(model);
            Image = () => model.GoToPage(new ImagePage(model, null));

            Lovense = () => LovensePage.GoToCreate(model);
            LovenseVisible = LovensePage.IsLovenseVisible();
        }

        public void UpdateContent(EditingUpdateType updateType)
        {
        }
    }

    public enum EditingSubPage { Settings, Effects, Zoom }

    public class EditLayerPage : IEditingPage
    {
        private IEditingPage _sourceEditor;

        public SceneItemModel Item { get; }

        public Action ShowSettings { get; }

        public Action ShowEffects { get; }

        public Action ShowZoom { get; }

        public Property<EditingSubPage> SubPage { get; } = new Property<EditingSubPage>(EditingSubPage.Settings);

        public Property<object> SubPageContent { get; } = new Property<object>();

        public Property<EditorMoveType> MoveType { get; } = new Property<EditorMoveType>(EditorMoveType.None);

        public SceneEditingEffects Effects { get; }


        public EditLayerPage(SceneEditingModel model, SceneItemModel item, IEditingPage sourceEditor)
        {
            Item = item;
            _sourceEditor = sourceEditor;
            SubPageContent.Value = sourceEditor;
            Effects = new SceneEditingEffects(model, item);

            ShowSettings = () => { SetSubPage(EditingSubPage.Settings); SubPageContent.Value = _sourceEditor; };
            ShowEffects = () => { SetSubPage(EditingSubPage.Effects); SubPageContent.Value = Effects; };
            ShowZoom = () => { SetSubPage(EditingSubPage.Zoom); SubPageContent.Value = item.Zoom; };
        }

        private void SetSubPage(EditingSubPage settings)
        {
            var old = SubPage.Value;
            SubPage.Value = settings;
            MoveType.Value = (int)old > (int)settings ? EditorMoveType.Right : EditorMoveType.Left;
        }

        public void UpdateContent(EditingUpdateType updateType)
        {
            _sourceEditor.UpdateContent(updateType);
            Effects.UpdateContent(updateType);
        }
    }



}
