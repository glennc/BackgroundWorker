using System;
using System.Collections.Generic;
using System.Text;
using BackgroundWork;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Hosting;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddBackgroundWorkScheduler(this IServiceCollection services)
        {
            if(services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<IBackgroundWorkScheduler, BackgroundWorkScheduler>();
            return services;
        }

        public static IServiceCollection AddBackgroundWorkScheduler(this IServiceCollection services, Action<BackgroundWorkSchedulerOptions> configureOptions)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if(configureOptions == null)
            {
                throw new ArgumentNullException(nameof(configureOptions));
            }

            services.Configure(configureOptions);
            services.AddBackgroundWorkScheduler();

            return services;
        }
    }
}
