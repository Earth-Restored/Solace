using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Solace.Common.Asp.Json;

// todo: when [JsonNamingPolicy(JsonKnownNamingPolicy.PascalCase)] is fixed, remove
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ForcePascalCaseAttribute : Attribute
{
     public static void PascalCaseModifier(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind is not JsonTypeInfoKind.Object)
        {
            return;
        }

        if (Attribute.IsDefined(typeInfo.Type, typeof(ForcePascalCaseAttribute)))
        {
            foreach (var property in typeInfo.Properties)
            {
                if (property.AttributeProvider is MemberInfo memberInfo)
                {
                    if (memberInfo.GetCustomAttribute<JsonPropertyNameAttribute>() is null)
                    {
                        property.Name = memberInfo.Name;
                    }
                }
            }
        }
    }
}
