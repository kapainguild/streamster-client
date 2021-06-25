using Streamster.ClientCore.Support;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Streamster.ClientCore.Models
{
    public class WebBrowserPage : IEditingPage
    {
        public static Resolution Custom = new Resolution(0, 0);

        public bool Editing { get; set; }

        public ValidatedProperty<string> Url { get; } = new ValidatedProperty<string>();

        public List<Resolution> Resolutions { get; } = new List<Resolution> { new Resolution(3840, 2160), new Resolution(1920, 1080), new Resolution(1280, 720), Custom};

        public Property<Resolution> Resolution { get; } = new Property<Resolution>();

        public ValidatedProperty<string> CustomWidth { get; } = new ValidatedProperty<string>("");

        public ValidatedProperty<string> CustomHeight { get; } = new ValidatedProperty<string>("");

        public Property<bool> CustomIsSelected { get; } = new Property<bool>(false);

        public ActionCommand Go { get; } = new ActionCommand();

        public WebBrowserPage()
        {
            Url.Validate = v => Uri.IsWellFormedUriString(v, UriKind.Absolute);
            CustomWidth.Validate = v => int.TryParse(v, out var parsed) && parsed > 0 && parsed < 5000;
            CustomHeight.Validate = v => int.TryParse(v, out var parsed) && parsed > 0 && parsed < 5000;

            Resolution.Value = Resolutions[1];
            CustomWidth.Value = Resolution.Value.Width.ToString();
            CustomHeight.Value = Resolution.Value.Height.ToString();
            CustomIsSelected.Value = Equals(Resolution.Value, Custom);

            Url.OnChange = (o, n) => Update();
            CustomWidth.OnChange = (o, n) => Update();
            CustomHeight.OnChange = (o, n) => Update();
            Resolution.OnChange = (o, n) => Update();
        }


        public virtual void UpdateContent(EditingUpdateType updateType)
        {
        }

        protected virtual void Update()
        {
            CustomIsSelected.Value = Equals(Resolution.Value, Custom);
            Go.CanExecute.Value = IsAllValid();
        }

        protected bool IsAllValid() => Url.IsValid && (!CustomIsSelected.Value || CustomHeight.IsValid && CustomWidth.IsValid);

        protected int GetWidth() => CustomIsSelected.Value ? int.Parse(CustomWidth.Value) : Resolution.Value.Width;

        protected int GetHeight() => CustomIsSelected.Value ? int.Parse(CustomHeight.Value) : Resolution.Value.Height;

        protected SceneItemSource GetModel()
        {
            return new SceneItemSource
            {
                Web = new SceneItemSourceWeb
                {
                    Url = Url.Value,
                    Width = GetWidth(),
                    Height = GetHeight()
                }
            };
        }

        public static string GetName(SceneItemSourceWeb web)
        {
            if (!string.IsNullOrWhiteSpace(web.Url) &&
                Uri.TryCreate(web.Url, UriKind.Absolute, out var result))
                return result.Host;

            return "Web page";
        }
    }

    public class WebBrowserPageAdd : WebBrowserPage
    {
        public WebBrowserPageAdd(SceneEditingModel parent)
        {
            Go.Execute = () => parent.AddSourceToScene(GetModel());
        }
    }


    public class WebBrowserPageEdit : WebBrowserPage
    {
        private readonly ISceneItem _item;

        public WebBrowserPageEdit(SceneEditingModel parent, ISceneItem item)
        {
            Editing = true;
            _item = item;

            var web = item.Source.Web;
            Url.Value = web.Url;
            Resolution.Value = Resolutions.FirstOrDefault(s => s.Width == web.Width && s.Height == web.Height) ?? Custom;
            CustomWidth.Value = web.Width.ToString();
            CustomHeight.Value = web.Height.ToString();

            Go.Execute = () =>
            {
                item.Source = GetModel();
                Update();
            };
        }

        protected override void Update()
        {
            CustomIsSelected.Value = Equals(Resolution.Value, Custom);
            Go.CanExecute.Value = IsAllValid() && !_item.Source.Equals(GetModel());
        }
    }
}
