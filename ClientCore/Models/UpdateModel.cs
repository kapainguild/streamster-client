using Streamster.ClientCore.Cross;
using Streamster.ClientData;
using System;

namespace Streamster.ClientCore.Models
{
    public class UpdateModel
    {
        private readonly IAppEnvironment _environment;

        public RootModel Root { get; }

        public Action Exit { get; private set; }

        public UpdateModel(RootModel root, IAppEnvironment environment)
        {
            Root = root;
            _environment = environment;
        }

        internal void Display(LoadBalancerResponse response)
        {
            Exit = () =>
            {
                _environment.OpenUrl(Root.AppData.DownloadAppUrl);
                Root.Exit();
            };

            Root.NavigateTo(this);
        }
    }
}