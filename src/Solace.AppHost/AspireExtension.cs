using Microsoft.Extensions.Configuration;

namespace Solace.AppHost;

internal static class AspireExtension
{
    extension<T>(IResourceBuilder<T> builder)
        where T : IResourceWithEnvironment
    {
        public IResourceBuilder<T> WithEnvironmentFromSection(
            IConfiguration config,
            string sectionPath,
            string? prefixToRemove = null,
            string? prefixToAdd = null)
        {
            var section = config.GetSection(sectionPath);

            foreach (var kvp in section.AsEnumerable())
            {
                if (kvp.Value is null)
                {
                    continue;
                }

                var envName = kvp.Key;
                if (!string.IsNullOrEmpty(prefixToRemove) && envName.StartsWith(prefixToRemove, StringComparison.Ordinal))
                {
                    envName = envName[prefixToRemove.Length..];
                }

                if (!string.IsNullOrEmpty(prefixToAdd))
                {
                    if (!prefixToAdd.EndsWith(':') && !envName.StartsWith(':'))
                    {
                        prefixToAdd = prefixToAdd + ":";
                    }

                    envName = prefixToAdd + envName;
                }

                envName = envName.TrimStart(':');

                envName = envName.Replace(":", "__", StringComparison.Ordinal);
                builder.WithEnvironment(envName, kvp.Value);
            }

            return builder;
        }

        public IResourceBuilder<T> WithEnvironmentFromConfig(ParamterForEnvironment paramter)
            => builder.WithEnvironment(paramter.EnvironmentName, paramter.Parameter);
    }

    extension(IDistributedApplicationBuilder builder)
    {
        public ParamterForEnvironment AddParameterForEnvironment(string configPath,
            string defaultValue = "",
            string? prefixToRemove = null,
            string? prefixToAdd = null,
            bool isSecret = false)
        {
            var envName = configPath;
            if (!string.IsNullOrEmpty(prefixToRemove) && envName.StartsWith(prefixToRemove, StringComparison.Ordinal))
            {
                envName = envName[prefixToRemove.Length..];
            }

            if (!string.IsNullOrEmpty(prefixToAdd))
            {
                if (!prefixToAdd.EndsWith(':') && !envName.StartsWith(':'))
                {
                    prefixToAdd = prefixToAdd + ":";
                }

                envName = prefixToAdd + envName;
            }

            envName = envName.TrimStart(':');

            envName = envName.Replace(":", "__", StringComparison.Ordinal);

            var parameterName = configPath.Replace(':', '-');

            var parameter = builder.AddParameter(
                parameterName,
                () => builder.Configuration[configPath] ?? defaultValue,
                secret: isSecret
            );

            return new ParamterForEnvironment(envName, parameter);
        }
    }

    public readonly record struct ParamterForEnvironment(string EnvironmentName, IResourceBuilder<ParameterResource> Parameter);
}