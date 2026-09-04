using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Solace.Common.Asp.Json;

namespace Solace.Common.Asp.Tests;

public sealed class ForcePascalCaseAttributeTests
{
    [ForcePascalCase]
    public sealed class TestForcePascalCaseModel
    {
        public string FirstName { get; set; } = "John";
        public int UserAge { get; set; } = 30;

        [JsonPropertyName("custom_override")]
        public string CustomProperty { get; set; } = "Value";
    }

    public sealed class TestNormalModel
    {
        public string FirstName { get; set; } = "John";
    }

    [Test]
    public async Task PascalCaseModifier_AppliedToAnnotatedClass_ForcesPascalCase()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { ForcePascalCaseAttribute.PascalCaseModifier, },
            },
        };

        var model = new TestForcePascalCaseModel();
        var json = JsonSerializer.Serialize(model, options);

        await Assert.That(json).Contains("""
            "FirstName":"John"
            """);
        await Assert.That(json).Contains("""
            "UserAge":30
            """);
        await Assert.That(json).Contains("""
            "custom_override":"Value"
            """);
    }

    [Test]
    public async Task PascalCaseModifier_NotAppliedToUnannotatedClass_UsesDefaultNamingPolicy()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { ForcePascalCaseAttribute.PascalCaseModifier, },
            },
        };

        var model = new TestNormalModel();
        var json = JsonSerializer.Serialize(model, options);

        await Assert.That(json).Contains("""
            "firstName":"John"
            """);
    }
}
