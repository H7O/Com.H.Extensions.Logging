using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Com.H.Data.Common;
using Microsoft.Extensions.Logging;
using System.Xml.Linq;

namespace Com.H.Extensions.Logging
{
    public class DbLoggerProvider : ILoggerProvider
    {
        private readonly IConfiguration? _logSection;
        private readonly DbConnection? _dbc;
        private readonly LazyConcurrentDictionary<string, DbLogger> _loggers = [];
        public DbLoggerProvider()
        {
            
        }

        public DbLoggerProvider(IConfiguration? logSection = null, DbConnection? dbc = null)
        {
            _logSection = logSection;
            _dbc = dbc;
        }


        // A method to create a logger with the given category name
        public ILogger CreateLogger(string categoryName)
        {

            return _loggers.AddOrUpdate(categoryName, _ =>
            {
                return CreateLoggerImplementation(categoryName);
            },
                (_, l) => l
            ) ?? throw new InvalidOperationException("Unable to create DbLogger");
        }

        private DbLogger CreateLoggerImplementation(string categoryName)
        {
            var disabled = _logSection?.GetValue<bool?>("disabled") ?? false;
            var logToConsole = _logSection?.GetValue<bool?>("log_to_console") ?? false;
            var logQuery = _logSection?.GetValue<string>("log_query");

            if (_dbc == null
                || string.IsNullOrWhiteSpace(logQuery)
                )
            {
                disabled = true;
            }

            #region log levels

            // get all probable log levels
            var logLevelEnumValues = Enum.GetValues(typeof(LogLevel))
                .Cast<LogLevel>().ToDictionary(k => k.ToString(), v => (int)(object)v);
            // the above should have values like:
            // { "Trace", 0 }, { "Debug", 1 }, { "Information", 2 }, { "Warning", 3 }, { "Error", 4 }, { "Critical", 5 }

            // if cache_settings is false then read the log level settings from the configuration
            // on every log request to detect any changes in the log level settings and apply them
            // in real time.
            // Else read the log level settings from the configuration and store them in a dictionary, 
            // which means any changes in the log level settings will not be detected and applied in real time,
            // hence the log level settings changes will be applied only when the application is restarted.

            var cacheSettings = _logSection?.GetValue<bool>("cache_settings") ?? false;


            Func<string, LogLevel, bool> logIsEnabledCheck;
            IConfigurationSection? logLevelSection;

            if (cacheSettings)
            {
                #region cached settings

                // read the category names and and their log levels from the configuration
                // and store them in a dictionary
                logLevelSection = _logSection?.GetSection("log_level");
                if (logLevelSection == null || !logLevelSection.Exists())
                {
                    logIsEnabledCheck = new Func<string, LogLevel, bool>((name, logLevel) =>
                        logLevel >= LogLevel.Information && categoryName?.Equals(name) == true);
                }
                else
                {
                    var defaultLogLevel = logLevelSection?.GetValue<string>("default") ?? string.Empty;

                    // if the default log level value is not within any of the possible log level values of LogLevel enum
                    // then set it to string.Empty
                    if (!logLevelEnumValues.ContainsKey(defaultLogLevel))
                        defaultLogLevel = string.Empty;

                    // get all the log level categories 
                    // that are within the possible available values of LogLevel enum
                    // then store them in a dictionary
                    // (note: ignore the default log level node when doing so)
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8621 // Nullability of reference types in return type doesn't match the target delegate (possibly because of nullability attributes).

                    Dictionary<string, string> logLevelCategories =
                    logLevelSection?.GetSection("category")?
                    .GetChildren()?
                        .Where(x =>
                            x is not null
                            && !string.IsNullOrEmpty(x.GetValue<string>("name"))
                            && !string.IsNullOrEmpty(x.GetValue<string>("level"))
                            && logLevelEnumValues.ContainsKey(x.GetValue<string>("level")))?
                        // all nullable values are checked in the above condition
                        .ToDictionary(k => k.GetValue<string>("name"), v => v.GetValue<string>("level")) ?? [];

#pragma warning restore CS8621 // Nullability of reference types in return type doesn't match the target delegate (possibly because of nullability attributes).
#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.


                    logIsEnabledCheck = new Func<string, LogLevel, bool>((name, logLevel) =>
                    {
                        if (string.IsNullOrWhiteSpace(name) || logLevel == LogLevel.None)
                        {
                            return false;
                        }

                        if (logLevelCategories?.ContainsKey(name) == true)
                        {
                            return (int)logLevel >= logLevelEnumValues[logLevelCategories[name]];
                        }
                        // if the category name is not in the log level categories
                        // then check if the default log level is greater than or equal to the log level of the category
                        if (!string.IsNullOrWhiteSpace(defaultLogLevel))
                        {
                            return (int)logLevel >= logLevelEnumValues[defaultLogLevel];
                        }
                        // if the category name is not in the log level categories
                        // and the default log level is not set
                        // then return false
                        return false;
                    });
                }
                #endregion
            }
            // non-cached settings
            else 
                logIsEnabledCheck =
                new Func<string, LogLevel, bool>((name, logLevel) =>
                {
                    if (string.IsNullOrWhiteSpace(name) || logLevel == LogLevel.None)
                    {
                        return false;
                    }
                    // read the category names and and their log levels from the configuration
                    // and store them in a dictionary
                    logLevelSection = _logSection?.GetSection("log_level");

                    if (logLevelSection == null || !logLevelSection.Exists())
                    {
                        return logLevel >= LogLevel.Information && categoryName?.Equals(name) == true;
                    }

                    var defaultLogLevel = logLevelSection?.GetValue<string>("default") ?? string.Empty;

                    // if the default log level value is not within any of the possible log level values of LogLevel enum
                    // then set it to string.Empty
                    if (!logLevelEnumValues.ContainsKey(defaultLogLevel))
                        defaultLogLevel = string.Empty;

                    // get all the log level categories 
                    // and are within the possible available values of LogLevel enum
                    // then store them in a dictionary
                    // (note: ignore the default log level node when doing so)
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8621 // Nullability of reference types in return type doesn't match the target delegate (possibly because of nullability attributes).
                    
                    Dictionary<string, string> logLevelCategories = 
                    logLevelSection?.GetSection("category")?
                    .GetChildren()?
                        .Where(x => 
                            x is not null
                            && !string.IsNullOrEmpty(x.GetValue<string>("name"))
                            && !string.IsNullOrEmpty(x.GetValue<string>("level"))
                            && logLevelEnumValues.ContainsKey(x.GetValue<string>("level")))?
                        // all nullable values are checked in the above condition
                        .ToDictionary(k => k.GetValue<string>("name"), v => v.GetValue<string>("level")) ?? [];

#pragma warning restore CS8621 // Nullability of reference types in return type doesn't match the target delegate (possibly because of nullability attributes).
#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.

                    
                    if (logLevelCategories?.ContainsKey(name) == true)
                    {
                        return (int)logLevel >= logLevelEnumValues[logLevelCategories[name]];
                    }
                    // if the category name is not in the log level categories
                    // then check if the default log level is greater than or equal to the log level of the category
                    if (!string.IsNullOrWhiteSpace(defaultLogLevel))
                    {
                        return (int)logLevel >= logLevelEnumValues[defaultLogLevel];
                    }
                    // if the category name is not in the log level categories
                    // and the default log level is not set
                    // then return false
                    return false;

                }
            );

            #endregion

            return new DbLogger(categoryName,
                logIsEnabledCheck,
                !disabled ? _dbc : null, !disabled ? logQuery : null,
                logToConsole);
        }

        // A method to dispose the provider
        public void Dispose()
        {
            if (_dbc != null)
            {
                this._dbc?.EnsureClosedAsync()?.GetAwaiter().GetResult();
            }
            GC.SuppressFinalize(this);
        }
    }
}