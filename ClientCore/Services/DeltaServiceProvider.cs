using Autofac;
using DeltaModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace Streamster.ClientCore.Services
{
    public class DeltaServiceProvider : IDeltaServiceProvider
    {
        private readonly ILifetimeScope _lifetimeScope;

        public DeltaServiceProvider(ILifetimeScope lifetimeScope)
        {
            _lifetimeScope = lifetimeScope;
        }

        public T Get<T>() => _lifetimeScope.Resolve<T>();
    }
}
