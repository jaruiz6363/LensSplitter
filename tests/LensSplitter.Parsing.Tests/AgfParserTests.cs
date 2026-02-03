using LensSplitter.Parsing.Glass;
using Xunit;

namespace LensSplitter.Parsing.Tests;

public class AgfParserTests
{
    [Fact]
    public void Parse_ValidAgfContent_ReturnsGlasses()
    {
        // Arrange
        var parser = new AgfParser();
        var agfContent = CreateSimpleAgfContent();
        var tempPath = Path.GetTempFileName() + ".agf";

        try
        {
            File.WriteAllText(tempPath, agfContent);

            // Act
            var glasses = parser.Parse(tempPath);

            // Assert
            Assert.NotNull(glasses);
            Assert.True(glasses.Count > 0);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Parse_ExtractsGlassName()
    {
        // Arrange
        var parser = new AgfParser();
        var agfContent = CreateAgfWithGlass("N-BK7", 1, 1.51680, 64.17);
        var tempPath = Path.GetTempFileName() + ".agf";

        try
        {
            File.WriteAllText(tempPath, agfContent);

            // Act
            var glasses = parser.Parse(tempPath);

            // Assert
            Assert.True(glasses.ContainsKey("N-BK7"));
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Parse_ExtractsDispersionModel()
    {
        // Arrange
        var parser = new AgfParser();
        var agfContent = CreateAgfWithGlass("TestGlass", 2, 1.5, 60.0); // Sellmeier1 model
        var tempPath = Path.GetTempFileName() + ".agf";

        try
        {
            File.WriteAllText(tempPath, agfContent);

            // Act
            var glasses = parser.Parse(tempPath);

            // Assert
            Assert.Equal(Core.Models.DispersionModel.Sellmeier1, glasses["TestGlass"].DispersionModel);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Parse_ExtractsCoefficients()
    {
        // Arrange
        var parser = new AgfParser();
        var agfContent = CreateAgfWithCoefficients();
        var tempPath = Path.GetTempFileName() + ".agf";

        try
        {
            File.WriteAllText(tempPath, agfContent);

            // Act
            var glasses = parser.Parse(tempPath);

            // Assert
            Assert.True(glasses["CoeffTest"].Coefficients.Length >= 6);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Parse_FileNotFound_ThrowsException()
    {
        // Arrange
        var parser = new AgfParser();

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => parser.Parse("nonexistent.agf"));
    }

    private string CreateSimpleAgfContent()
    {
        return @"CC Test Glass Catalog
NM TestGlass 1 0 1.5 60.0 0 0 0
CD 2.0 0.01 0.01 0.0001 0.00001 0.000001
";
    }

    private string CreateAgfWithGlass(string name, int model, double nd, double vd)
    {
        return $@"CC Test Glass Catalog
NM {name} {model} 0 {nd} {vd} 0 0 0
CD 1.0 0.01 0.01 0.0001 0.00001 0.000001
";
    }

    private string CreateAgfWithCoefficients()
    {
        return @"CC Test Glass Catalog with Coefficients
NM CoeffTest 2 0 1.5168 64.17 0 0 0
CD 1.03961212 0.00600069867 0.231792344 0.0200179144 1.01046945 103.560653
";
    }
}
