using Microsoft.Extensions.Configuration;

namespace Solace.AppHost;

internal static class AspireExtension
{
    extension<T>(IResourceBuilder<T> builder)
        where T : IResourceWithEnvironment
    {
        public IResourceBuilder<T> WithEnvironmentSection(
            IConfiguration config,
            string sectionPath,
            string? prefixToRemove = null,
            string? prefixToAdd = null,
            params ReadOnlySpan<ConfigParameter> parameterOverrides)
        {
            var section = config.GetSection(sectionPath);

            foreach (var kvp in section.AsEnumerable())
            {
                if (kvp.Value is null)
                {
                    continue;
                }

                var envName = TransformKey(kvp.Key, prefixToRemove, prefixToAdd);

                var overrideParam = Find(parameterOverrides, p => p.ConfigPath == kvp.Key);

                if (overrideParam.Parameter is not null)
                {
                    builder.WithEnvironment(envName, overrideParam.Parameter);
                }
                else
                {
                    builder.WithEnvironment(envName, kvp.Value);
                }
            }

            return builder;

            static TItem? Find<TItem>(ReadOnlySpan<TItem> collection, Func<TItem, bool> predicate)
            {
                foreach (var item in collection)
                {
                    if (predicate(item))
                    {
                        return item;
                    }
                }

                return default;
            }
        }

        public IResourceBuilder<T> WithEnvironmentParameter(
            ConfigParameter configParam,
            string? prefixToRemove = null,
            string? prefixToAdd = null)
        {
            if (configParam.Parameter is null)
            {
                return builder;
            }

            var envName = TransformKey(configParam.ConfigPath, prefixToRemove, prefixToAdd);
            return builder.WithEnvironment(envName, configParam.Parameter);
        }
    }

    private static string TransformKey(string key, string? prefixToRemove, string? prefixToAdd)
    {
        var envName = key;
        if (!string.IsNullOrEmpty(prefixToRemove) && envName.StartsWith(prefixToRemove, System.StringComparison.Ordinal))
        {
            envName = envName[prefixToRemove.Length..];
        }

        if (!string.IsNullOrEmpty(prefixToAdd))
        {
            if (!prefixToAdd.EndsWith(':') && !envName.StartsWith(':'))
            {
                prefixToAdd += ":";
            }

            envName = prefixToAdd + envName;
        }

        envName = envName.TrimStart(':');
        return envName.Replace(":", "__", System.StringComparison.Ordinal);
    }

    extension(IDistributedApplicationBuilder builder)
    {
        public ConfigParameter AddConfigParameter(
            string configPath,
            string defaultValue = "",
            bool isSecret = false)
        {
            var parameterName = configPath.Replace(':', '-');

            var parameter = builder.AddParameter(
                parameterName,
                () => builder.Configuration[configPath] ?? defaultValue,
                secret: isSecret
            );

            return new ConfigParameter(configPath, parameter);
        }
    }

    public readonly record struct ConfigParameter(string ConfigPath, IResourceBuilder<ParameterResource>? Parameter);
}