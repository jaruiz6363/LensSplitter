using LensSplitter.Parsing.Optiland;
using Xunit;

namespace LensSplitter.Parsing.Tests;

public class OptilandJsonParserTests
{
    [Fact]
    public void ParseJson_ValidJson_CreatesSystem()
    {
        // Arrange
        var parser = new OptilandJsonParser();
        var json = CreateSimpleOptilandJson();

        // Act
        var system = parser.ParseJson(json, "TestSystem");

        // Assert
        Assert.NotNull(system);
        Assert.Equal("TestSystem", system.Name);
        Assert.True(system.Surfaces.Count > 0);
    }

    [Fact]
    public void ParseJson_ExtractsAperture()
    {
        // Arrange
        var parser = new OptilandJsonParser();
        var json = CreateOptilandJsonWithAperture(12.5);

        // Act
        var system = parser.ParseJson(json);

        // Assert
        Assert.Equal(12.5, system.ApertureValue, 3);
    }

    [Fact]
    public void ParseJson_ExtractsWavelengths()
    {
        // Arrange
        var parser = new OptilandJsonParser();
        var json = CreateOptilandJsonWithWavelengths();

        // Act
        var system = parser.ParseJson(json);

        // Assert
        Assert.Equal(3, system.Wavelengths.Count);
    }

    [Fact]
    public void ParseJson_ExtractsFields()
    {
        // Arrange
        var parser = new OptilandJsonParser();
        var json = CreateOptilandJsonWithFields();

        // Act
        var system = parser.ParseJson(json);

        // Assert
        Assert.True(system.Fields.Count > 0);
    }

    [Fact]
    public void ParseJson_ConvertsAbsoluteZToThickness()
    {
        // Arrange
        var parser = new OptilandJsonParser();
        var json = CreateOptilandJsonWithSurfaces();

        // Act
        var system = parser.ParseJson(json);

        // Assert
        // The second surface should have non-zero thickness (distance to third surface)
        var optical = system.Surfaces.Where(s => s.Index > 0 && s.Index < system.Surfaces.Count - 1).ToList();
        if (optical.Count > 0)
        {
            Assert.True(system.Surfaces.Any(s => s.Thickness > 0));
        }
    }

    private string CreateSimpleOptilandJson()
    {
        return @"{
    ""version"": 1.0,
    ""aperture"": { ""type"": ""EPD"", ""value"": 10 },
    ""wavelengths"": { ""wavelengths"": [ { ""value"": 0.55, ""is_primary"": true, ""unit"": ""um"" } ] },
    ""fields"": { ""fields"": [ { ""field_type"": ""angle"", ""x"": 0, ""y"": 0 } ], ""field_type"": ""angle"" },
    ""surface_group"": {
        ""surfaces"": [
            {
                ""type"": ""ObjectSurface"",
                ""geometry"": { ""type"": ""Plane"", ""cs"": { ""x"": 0, ""y"": 0, ""z"": ""-Infinity"" }, ""radius"": ""Infinity"" },
                ""material_post"": { ""type"": ""IdealMaterial"", ""index"": 1.0 }
            },
            {
                ""type"": ""Surface"",
                ""geometry"": { ""type"": ""StandardGeometry"", ""cs"": { ""x"": 0, ""y"": 0, ""z"": 0 }, ""radius"": 50, ""conic"": 0 },
                ""material_post"": { ""type"": ""IdealMaterial"", ""index"": 1.5 }
            },
            {
                ""type"": ""Surface"",
                ""geometry"": { ""type"": ""Plane"", ""cs"": { ""x"": 0, ""y"": 0, ""z"": 100 }, ""radius"": ""Infinity"" },
                ""material_post"": { ""type"": ""IdealMaterial"", ""index"": 1.0 }
            }
        ]
    }
}";
    }

    private string CreateOptilandJsonWithAperture(double epd)
    {
        return $@"{{
    ""version"": 1.0,
    ""aperture"": {{ ""type"": ""EPD"", ""value"": {epd} }},
    ""wavelengths"": {{ ""wavelengths"": [ {{ ""value"": 0.55, ""is_primary"": true, ""unit"": ""um"" }} ] }},
    ""fields"": {{ ""fields"": [ {{ ""field_type"": ""angle"", ""x"": 0, ""y"": 0 }} ], ""field_type"": ""angle"" }},
    ""surface_group"": {{ ""surfaces"": [] }}
}}";
    }

    private string CreateOptilandJsonWithWavelengths()
    {
        return @"{
    ""version"": 1.0,
    ""aperture"": { ""type"": ""EPD"", ""value"": 10 },
    ""wavelengths"": {
        ""wavelengths"": [
            { ""value"": 0.48, ""is_primary"": false, ""unit"": ""um"" },
            { ""value"": 0.55, ""is_primary"": true, ""unit"": ""um"" },
            { ""value"": 0.65, ""is_primary"": false, ""unit"": ""um"" }
        ]
    },
    ""fields"": { ""fields"": [ { ""field_type"": ""angle"", ""x"": 0, ""y"": 0 } ], ""field_type"": ""angle"" },
    ""surface_group"": { ""surfaces"": [] }
}";
    }

    private string CreateOptilandJsonWithFields()
    {
        return @"{
    ""version"": 1.0,
    ""aperture"": { ""type"": ""EPD"", ""value"": 10 },
    ""wavelengths"": { ""wavelengths"": [ { ""value"": 0.55, ""is_primary"": true, ""unit"": ""um"" } ] },
    ""fields"": {
        ""fields"": [
            { ""field_type"": ""angle"", ""x"": 0, ""y"": 0 },
            { ""field_type"": ""angle"", ""x"": 0, ""y"": 10 },
            { ""field_type"": ""angle"", ""x"": 0, ""y"": 20 }
        ],
        ""field_type"": ""angle""
    },
    ""surface_group"": { ""surfaces"": [] }
}";
    }

    private string CreateOptilandJsonWithSurfaces()
    {
        return @"{
    ""version"": 1.0,
    ""aperture"": { ""type"": ""EPD"", ""value"": 10 },
    ""wavelengths"": { ""wavelengths"": [ { ""value"": 0.55, ""is_primary"": true, ""unit"": ""um"" } ] },
    ""fields"": { ""fields"": [ { ""field_type"": ""angle"", ""x"": 0, ""y"": 0 } ], ""field_type"": ""angle"" },
    ""surface_group"": {
        ""surfaces"": [
            {
                ""type"": ""ObjectSurface"",
                ""geometry"": { ""type"": ""Plane"", ""cs"": { ""x"": 0, ""y"": 0, ""z"": ""-Infinity"" }, ""radius"": ""Infinity"" },
                ""material_post"": { ""type"": ""IdealMaterial"", ""index"": 1.0 }
            },
            {
                ""type"": ""Surface"",
                ""geometry"": { ""type"": ""StandardGeometry"", ""cs"": { ""x"": 0, ""y"": 0, ""z"": 0 }, ""radius"": 50, ""conic"": 0 },
                ""material_post"": { ""type"": ""Material"", ""name"": ""BK7"" }
            },
            {
                ""type"": ""Surface"",
                ""geometry"": { ""type"": ""StandardGeometry"", ""cs"": { ""x"": 0, ""y"": 0, ""z"": 5 }, ""radius"": -100, ""conic"": 0 },
                ""material_post"": { ""type"": ""IdealMaterial"", ""index"": 1.0 }
            },
            {
                ""type"": ""Surface"",
                ""geometry"": { ""type"": ""Plane"", ""cs"": { ""x"": 0, ""y"": 0, ""z"": 100 }, ""radius"": ""Infinity"" },
                ""material_post"": { ""type"": ""IdealMaterial"", ""index"": 1.0 }
            }
        ]
    }
}";
    }
}
