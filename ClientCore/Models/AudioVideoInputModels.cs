using Clutch.DeltaModel;
using Streamster.ClientCore.Services;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace Streamster.ClientCore.Models
{
    public class LocalBaseInputModel
    {
        public IInput Source { get; }

        public InputType Type { get; }

        public Property<InputState> State { get; } = new Property<InputState>();

        public Action Select { get; set; }

        public LocalBaseInputModel(IInput videoInput, CoreData coreData)
        {
            Source = videoInput;
            State.Value = videoInput.State;

            bool remote = videoInput.Owner != coreData.ThisDeviceId;
            Type = remote ? InputType.Remote : videoInput.Type;
        }
    }

    public class LocalVideoInputModel : LocalBaseInputModel
    {
        private LocalSettingsService _localSettingsService;

        public Property<bool> IsPreviewing { get; } = new Property<bool>();

        public Property<byte[]> Preview { get; } = new Property<byte[]>();

        public LocalVideoInputModel(IInput videoInput, CoreData coreData, MainSourcesModel videoSourceModel) : base(videoInput, coreData)
        {
            IsPreviewing.OnChange = (o, n) =>
            {
                if (!_localSettingsService.Settings.EnableVideoPreview)
                    videoSourceModel.CurrentPreview.Value = null;
                else
                    videoSourceModel.CurrentPreview.Value = n ? this : null;
            };

            Select = () => videoSourceModel.LocalRequest(this);
        }

        public LocalVideoInputModel(IInput videoInput, CoreData coreData, MainSourcesModel videoSourceModel, LocalSettingsService localSettingsService) : this(videoInput, coreData, videoSourceModel)
        {
            _localSettingsService = localSettingsService;
        }
    }

    public class LocalAudioInputModel : LocalBaseInputModel
    {
        public LocalAudioInputModel(IInput videoInput, CoreData coreData, MainSourcesModel videoSourceModel) : base(videoInput, coreData)
        {
            Select = () => videoSourceModel.LocalRequest(this);
        }

        public Property<bool> SameOwnerAsVideo { get; } = new Property<bool>();
    }
}
