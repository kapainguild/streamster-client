using DynamicStreamer.Extensions.WebBrowser;
using Streamster.ClientCore.Cross;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Streamster.ClientCore.Models
{
    public enum LovenseState { Installed, NotInstalledLocal, NotInstalledRemove }


    public class LovensePage : IEditingPage
    {
        private readonly SceneEditingModel _model;
        private readonly ISceneItem _editedItem;

        public Action Refresh { get; }

        public Action Download { get; }

        public Property<LovenseState> State { get; } = new Property<LovenseState>();

        public Action HowToUseIt { get; }

        public LovensePage(SceneEditingModel model, ISceneItem editedItem)
        {
            _model = model;
            _editedItem = editedItem;

            RefreshState();

            Refresh = () =>
            {
                PluginContextSetup.TryToLoad();
                if (editedItem == null && IsInstalled(_model))
                    AddPlugin(model);
                else 
                    RefreshState();
            };

            Download = () => model.Environment.OpenUrl("https://streamster.io/how-to-install-the-lovense-streamster-toolset");

            HowToUseIt = () => model.Environment.OpenUrl("https://streamster.io/how-to-install-the-lovense-streamster-toolset");
        }

        internal static bool IsLovenseVisible()
        {
            PluginContextSetup.TryToLoad();
            return PluginContextSetup.IsLoaded() || PluginContextSetup.IsInstalledForOthers();
        }

        private void RefreshState()
        {
            if (IsInstalled(_model))
                State.Value = LovenseState.Installed;
            else if(_model.SceneState.IsLocal)
                State.Value = LovenseState.NotInstalledLocal;
            else
                State.Value = LovenseState.NotInstalledRemove;
        }
        private static void AddPlugin(SceneEditingModel model)
        {
            var source = new SceneItemSource { Lovense = new SceneItemSourceLovense() };
            model.AddSourceToScene(source, true);
        }

        internal static void GoToCreate(SceneEditingModel model)
        {
            bool isInstalled = IsInstalled(model);
            if (isInstalled)
            {
                AddPlugin(model);
            }
            else
                model.GoToPage(new LovensePage(model, null));
        }

        public static bool IsInstalled(SceneEditingModel model)
        {
            return model.SceneState.IsLocal ?
                PluginContextSetup.IsLoaded() : 
                (model.SceneState.Device.PluginFlags & ((int)PluginFlags.Lovense)) > 0;
        }

        public void UpdateContent(EditingUpdateType updateType) { }
    }
}
