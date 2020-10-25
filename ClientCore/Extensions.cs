using Autofac;

namespace Streamster.ClientCore
{
    public static class Extensions
    {
        public static ContainerBuilder Add<T>(this ContainerBuilder builder)
        {
            builder.RegisterType<T>().AsSelf().SingleInstance();
            return builder;
        }

        public static ContainerBuilder Add<TInterface, T>(this ContainerBuilder builder)
        {
            builder.RegisterType<T>().As<TInterface>().SingleInstance();
            return builder;
        }
    }
}
