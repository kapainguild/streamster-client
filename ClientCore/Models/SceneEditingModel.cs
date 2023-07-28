using DeltaModel;
using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Services;
using Streamster.ClientData;
using Streamster.ClientData.Model;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Models
{
    public class SceneEditingModel
    {
        private readonly StreamSettingsModel _streamSettings;
        private readonly StreamingSourcesModel _streamingSourcesModel;

        public CoreData CoreData { get; }

        public LocalSourcesModel LocalSources { get; }

        public IAppEnvironment Environment { get; }
        public ResourceService ResourceService { get; }
        public IImageHelper ImageHelper { get; }
        public SceneState SceneState { get; set; }

        public ObservableCollection<SceneItemModel> Items { get; } = new ObservableCollection<SceneItemModel>();

        public Property<SceneItemModel> SelectedItem { get; } = new Property<SceneItemModel>();

        public Action AddLayer { get; }

        public Property<bool> AddLayerSelected { get; } = new Property<bool>();

        public Property<bool> EditingMode { get; } = new Property<bool>();

        public Property<bool> EditingModeDelayedVisibility { get; } = new Property<bool>();

        public Action Close { get; }

        public Property<IEditingPage> MainContent { get; } = new Property<IEditingPage>();

        public Property<EditorMoveType> MoveType { get; } = new Property<EditorMoveType>(EditorMoveType.None);

        public Property<bool> DragIsNotRequired { get; } = new Property<bool>(true);

        public Property<bool> EditingEnabled { get; } = new Property<bool>(true);

        public SceneEditingModel(CoreData coreData, LocalSourcesModel sources, IAppEnvironment environment, ResourceService resourceService, 
            IImageHelper imageHelper, StreamSettingsModel streamSettings, StreamingSourcesModel streamingSourcesModel)
        {
            CoreData = coreData;
            LocalSources = sources;
            Environment = environment;
            ResourceService = resourceService;
            ImageHelper = imageHelper;
            _streamSettings = streamSettings;
            _streamingSourcesModel = streamingSourcesModel;
            AddLayer = SelectAddLayer;

            Close = DoClose;
        }

        public void DoClose()
        {
            AddLayerSelected.Value = false;
            SelectItem(null);
            RefreshEditingMode();
        }

        public void Start()
        {
            CreateSceneIfNeeded();
            RefreshItems();

            CoreData.Subscriptions.SubscribeForAnyProperty<IInputDevice>((i, c, e, r) => UpdateMainContent(EditingUpdateType.Sources));
            CoreData.Subscriptions.SubscribeForProperties<IDevice>( s => s.Displays, (i, c, e) => UpdateMainContent(EditingUpdateType.Sources));
            CoreData.Subscriptions.SubscribeForProperties<IDevice>(s => s.Windows, (i, c, e) => UpdateMainContent(EditingUpdateType.Sources));
            CoreData.Subscriptions.SubscribeForProperties<IScene>(s => s.VideoIssues, (i, c, e) => UpdateIssues());
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.SelectedScene, (i, c, e) => RefreshItems());

            CoreData.Subscriptions.SubscribeForAnyProperty<ISceneItem>((a, b, c, d) =>
            {
                RefreshItems();
                if (SelectedItem.Value?.Model == a)
                    UpdateMainContent(EditingUpdateType.Content);
            });

            UpdateIssues();
        }

        private void UpdateIssues()
        {
            if (SceneState != null)
            {
                var issues = SceneState.Scene.VideoIssues;

                foreach (var item in Items)
                {
                    var issue = issues != null ? issues.FirstOrDefault(s => s.Id == item.Id) : null;
                    if (issue == null)
                        item.SourceIssue.Value = null;
                    else
                        item.SourceIssue.Value = GetIssueString(issue.Desc);
                }
            }
        }

        public static string GetIssueString(InputIssueDesc desc) => desc switch
        {
            InputIssueDesc.NoAudioSelected => "No audio selected",
            InputIssueDesc.AudioRemoved => "Audio device removed",
            InputIssueDesc.UnknownTypOfSource => "Unknown type of source",
            InputIssueDesc.VideoRemoved => "Video device removed",
            InputIssueDesc.ImageNotFound => "Image not found",
            InputIssueDesc.ImageUnknownFormat => "Unknown format",
            InputIssueDesc.PluginIsNotInstalled => "Plugin is not installed or failed to load",
            InputIssueDesc.CaptureNotFound => "Capture target is not found",
            InputIssueDesc.Failed => "Input/output error",
            InputIssueDesc.NoFrames => "No data comes from the source",
            InputIssueDesc.TooManyFrames => "Source works incorrectly",
            InputIssueDesc.InUse => "Device failed, may be in use by another app",
            _ => "Unknown error"
        };


        private void UpdateMainContent(EditingUpdateType type)
        {
            MainContent.Value?.UpdateContent(type);
        }

        public void GoToPage(IEditingPage page, EditorMoveType moveType = EditorMoveType.Right)
        {
            var old = MainContent.Value;
            if (old is IDisposable disposable)
                disposable.Dispose();

            MoveType.Value = moveType;
            MainContent.Value = page;
        }

        private void CreateSceneIfNeeded()
        {
            // try first my scene
            var myScene = CoreData.Root.Scenes.Values.FirstOrDefault(s => s.Owner == CoreData.ThisDeviceId);
            if (myScene == null)
            {
                var id = IdGenerator.New();
                CoreData.Root.Scenes[id] = CreateDefaultScene();

                if (CoreData.Settings.SelectedScene == null)
                    _streamingSourcesModel.SelectScene(id);
            }
        }

        internal void AddSourceToScene(SceneItemSource source, bool fullScreen = false, SceneRect desiredRect = null)
        {
            var state = SceneState;
            var zorder = state.Scene.Items.Count > 0 ? state.Scene.Items.Values.Max(s => s.ZOrder) + 1 : 0;

            var deltaX = 0.02 * state.Scene.Items.Count;
            var deltaY = 0.015 * state.Scene.Items.Count;

            var sceneItem = CoreData.Create<ISceneItem>();

            if (fullScreen)
                desiredRect = SceneRect.Full();
            else if (desiredRect != null)
                desiredRect = new SceneRect { T = 0.1 + deltaY, L = 0.4 - deltaX, H = desiredRect.H, W = desiredRect.W };
            else 
                desiredRect = new SceneRect { T = 0.1 + deltaY, L = 0.4 - deltaX, H = 0.5, W = 0.5 };

            sceneItem.Rect = desiredRect; 
            sceneItem.Ptz = new SceneRect { H = 1, W = 1 };
            sceneItem.Visible = true;
            sceneItem.ZOrder = zorder;
            sceneItem.Source = source;
            var shortId = IdGenerator.NewShortId(state.Scene.Items);
            state.Scene.Items.Add(shortId, sceneItem);

            Log.Information($"Adding source '{source}'");

            RefreshItems();
            SelectItem(Items.FirstOrDefault(s => s.Id == shortId));
        }

        private IScene CreateDefaultScene()
        {
            bool hFlip = false;
            var video = LocalSources.GetBestDefaultVideo();
            var audio = LocalSources.GetBestDefaultAudio();

            IScene scene = CoreData.Create<IScene>();
            scene.Owner = CoreData.ThisDeviceId;

            if (video != null)
            {
                var sceneItem = CoreData.Create<ISceneItem>();
                sceneItem.Rect = new SceneRect { H = 1, W = 1 };
                sceneItem.Ptz = new SceneRect { H = 1, W = 1 };
                sceneItem.Visible = true;
                sceneItem.Source = new SceneItemSource
                {
                    Device = new SceneItemSourceDevice { DeviceName = new DeviceName { DeviceId = CoreData.GetId(video), Name = video.Name } }
                };

                if (hFlip)
                {
                    sceneItem.Filters = new SceneItemFilters { Filters = new[] { new SceneItemFilter { Enabled = true, Type = SceneItemFilterType.HFlip } } };
                }

                scene.Items.Add(IdGenerator.NewShortId(scene.Items), sceneItem);
            }


            var mic = CoreData.Create<ISceneAudio>();
            mic.Source = new SceneAudioSource
            {
                DeviceName = audio == null ? null : new DeviceName { DeviceId = CoreData.GetId(audio), Name = audio.Name }
            };
            scene.Audios.Add(SceneAudioConsts.MicrophoneId, mic);

            var da = CoreData.Create<ISceneAudio>();
            da.Muted = true;
            da.Source = new SceneAudioSource { DesktopAudio = true };
            scene.Audios.Add(SceneAudioConsts.DesktopAudioId, da);

            return scene;
        }

        private void RefreshItems()
        {
            if (_streamingSourcesModel.TryGetCurrentSceneDevice(out var scene, out var device) &&
                ClientConstants.SupportsSceneEditing(device.Type)) 
            {
                EditingEnabled.Value = true;
                SceneState = new SceneState(scene, device, device == CoreData.ThisDevice);
                var newSelection = ListHelper.UpdateCollectionWithSelection(CoreData,
                    scene.Items.Values.Where(s => s.Source != null).OrderByDescending(s => s.ZOrder).ToList(),
                    Items,
                    SelectedItem,
                    t => t.Id,
                    CreateItemModel);

                if (newSelection.ChengeSelection)
                {
                    if (newSelection.Selection == null)
                        SelectAddLayer();
                    else
                        SelectItem(newSelection.Selection);
                }

                int idx = 0;
                foreach (var item in Items)
                {
                    RefreshItem(item, idx++);
                }
            }
            else
            {
                SceneState = null;
                EditingEnabled.Value = false;
                Items.Clear();
                DoClose();
            }
        }

        public void SelectAddLayer()
        {
            AddLayerSelected.Value = true;
            SelectItem(null);
            RefreshEditingMode();

            GoToPage(new AddLayerPage(this), EditorMoveType.Down);
        }

        public void SelectItem(SceneItemModel item)
        {
            if (item == null)
            {
                Items.ToList().ForEach(s => s.IsSelected.Value = false);
                SelectedItem.Value = null;
            }
            else
            {
                var lastSelectedIdx = SelectedItem.Value != null ? Items.IndexOf(SelectedItem.Value) : -1;
                var newSelectedIdx = Items.IndexOf(item);


                Items.Where(s => s != item).ToList().ForEach(s => s.IsSelected.Value = false);
                SelectedItem.Value = item;
                SelectedItem.Value.IsSelected.Value = true;
                AddLayerSelected.Value = false;
                GoToPage(new EditLayerPage(this, item, CreateSourceEditor(item)), newSelectedIdx > lastSelectedIdx ? EditorMoveType.Up : EditorMoveType.Down);
            }
            RefreshEditingMode();
        }

        private IEditingPage CreateSourceEditor(SceneItemModel item)
        {
            var source = item.Model.Source;
            if (source.Device != null)
                return new DevicePage(this, item.Model);
            else if (source.Web != null)
                return new WebBrowserPageEdit(this, item.Model);
            else if (source.CaptureWindow != null)
                return new CapturePageWindow(this, item.Model);
            else if (source.CaptureDisplay != null)
                return new CapturePageDisplay(this, item.Model);
            else if (source.Lovense != null)
                return new LovensePage(this, item.Model);
            else if (source.Image != null)
                return new ImagePage(this, item.Model);
            return null;
        }

        public void RefreshEditingMode()
        {
            var on = (AddLayerSelected.Value || SelectedItem.Value != null) && EditingEnabled.Value;
            if (EditingMode.Value != on)
            {
                EditingMode.Value = on;
                if (on)
                {
                    EditingModeDelayedVisibility.Value = true;
                    CoreData.ThisDevice.PreviewSources = true;
                    _streamSettings.SelectedLayout.Value = LayoutType.Standart;
                }
                else
                {
                    Items.ToList().ForEach(s => s.IsMouseOver.Value = false);
                    CoreData.ThisDevice.PreviewSources = false;
                    GoToPage(null, EditorMoveType.Up);
                    _ = RestEditingModeDelayedVisibility();
                }
            }
        }

        private async Task RestEditingModeDelayedVisibility()
        {
            await Task.Delay(300);
            EditingModeDelayedVisibility.Value = false;
        }

        private SceneItemModel CreateItemModel(ISceneItem model, string modelId)
        {
            var m = new SceneItemModel(model, modelId, GetSceneItemModelType(model));
            m.Select = () => SelectItem(m);
            m.MoveDown.Execute = () => MoveItem(m, +1);
            m.MoveUp.Execute = () => MoveItem(m, -1);
            m.Maximize.Execute = () => m.Model.Rect = SceneRect.Full();
            m.Delete = () =>
            {
                if (SceneState != null && 
                    SceneState.Scene.Items.TryGetValue(modelId, out var item))
                {
                    Log.Information($"Removing source '{item?.Source}'");
                    SceneState.Scene.Items.Remove(modelId);
                }
            };

            return m;
        }

        private void MoveItem(SceneItemModel item, int sign)
        {
            int index = Items.IndexOf(item);
            if (index >= 0)
            {
                int newIndex = index + sign;
                if (newIndex >= 0 && newIndex < Items.Count)
                {
                    var toReplace = Items[newIndex];
                    // swap zorder of toReplace <> item
                    var z1 = toReplace.Model.ZOrder;
                    var z2 = item.Model.ZOrder;

                    toReplace.Model.ZOrder = z2;
                    item.Model.ZOrder = z1;
                }
            }
        }

        private void RefreshItem(SceneItemModel item, int index)
        {
            var name = GetName(item.Model);
            if (name != item.Name.Value)
                item.Name.Value = name;

            item.MoveDown.CanExecute.Value = index != Items.Count - 1;
            item.MoveUp.CanExecute.Value = index != 0;
            item.UpdateFromModel();

            
        }

        private string GetName(ISceneItem model)
        {
            var source = model.Source;
            return source switch
            {
                { Device: not null } => source.Device.DeviceName.Name,
                { CaptureDisplay: not null } => source.CaptureDisplay.Source.Name,
                { CaptureWindow: not null } => source.CaptureWindow.Source.Name,
                { Web: not null } => WebBrowserPage.GetName(source.Web),
                { Image: not null } => ImagePage.GetName(source.Image, CoreData),
                { Lovense: not null } => "Lovense"
            };
        }

        private SceneItemModelType GetSceneItemModelType(ISceneItem model)
        {
            var source = model.Source;
            return source switch
            {
                { Device: not null } => SceneItemModelType.Device,
                { CaptureDisplay: not null } => SceneItemModelType.ScreenCapture,
                { CaptureWindow: not null } => SceneItemModelType.WindowCapture,
                { Web: not null } => SceneItemModelType.WebPage,
                { Image: not null } => SceneItemModelType.Image,
                { Lovense: not null } => SceneItemModelType.Lovense
            };
        }
    }

    public interface IEditingPage
    {
        void UpdateContent(EditingUpdateType updateType);
    }

    public enum EditingUpdateType { Content, Sources }

    public enum EditorMoveType { None, Up, Down, Right, Left}
    
    public record SceneState(IScene Scene, IDevice Device, bool IsLocal);

    public class SceneItemModel
    {
        public string Id { get; }

        public ISceneItem Model { get; set; }

        public SceneItemModelType Type { get; }

        public Property<SceneRect> Rect { get; }

        public Property<bool> IsSelected { get; } = new Property<bool>();

        public Property<bool> IsMouseOver { get; } = new Property<bool>();

        public Property<string> Name { get; } = new Property<string>();

        public Action Select { get; set; }

        public ActionCommand Maximize { get; } = new ActionCommand();

        public Action Delete { get; set; }

        public ActionCommand MoveUp { get; } = new ActionCommand();

        public ActionCommand MoveDown{ get; } = new ActionCommand();

        public Property<string> SourceIssue { get; } = new Property<string>();

        public ZoomModel Zoom { get; }

        public SceneItemModel(ISceneItem item, string id, SceneItemModelType type)
        {
            Id = id;
            Type = type;
            Model = item;
            Rect = new Property<SceneRect>(item.Rect);
            Zoom = new ZoomModel();
            Rect.OnChange = (o, n) => Model.Rect = n;
            Zoom.Changed = () =>
            {
                Model.Ptz = Zoom.GetPtz();
                Model.ZoomBehavior = Zoom.ZoomBehavior.Value.Value;
            };
        }

        public void UpdateFromModel()
        {
            if (!Equals(Rect.Value, Model.Rect))
                Rect.SilentValue = Model.Rect;

            Maximize.CanExecute.Value = !Equals(Model.Rect, SceneRect.Full());
            Zoom.SetPtz(Model.Ptz, Model.ZoomBehavior, Model.Source?.Device != null);
        }
    }

    public enum SceneItemModelType { Device, Image, WebPage, ScreenCapture, WindowCapture, Lovense}

    public class ActionCommand
    {
        public Action Execute { get; set; }
        public Property<bool> CanExecute { get; } = new Property<bool>();
    }
}
