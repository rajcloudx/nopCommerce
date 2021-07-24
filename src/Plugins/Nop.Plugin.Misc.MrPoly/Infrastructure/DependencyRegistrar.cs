﻿using Autofac;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Core.Infrastructure.DependencyManagement;
using Nop.Plugin.Misc.MrPoly.Catalog;
using Nop.Services.Catalog;

namespace Nop.Plugin.Misc.MrPoly.Infrastructure
{
    public class DependencyRegistrar : IDependencyRegistrar
    {
        public int Order => 2;

        public void Register(ContainerBuilder builder, ITypeFinder typeFinder, NopConfig config)
        {
            builder.RegisterType<PolyProductService>().As<IProductService>().InstancePerLifetimeScope();
        }
    }
}
