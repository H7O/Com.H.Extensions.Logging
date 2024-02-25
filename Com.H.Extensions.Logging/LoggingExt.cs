using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Com.H.Extensions.Logging
{
    public static class LoggingExt
    {
        public static ILoggingBuilder AddProvider<T>(
            this ILoggingBuilder builder, 
            Func<IServiceProvider, T> factory)
            where T : class, ILoggerProvider
        {
            builder.Services.AddSingleton<ILoggerProvider, T>(factory);
            return builder;
        }

    }
}
