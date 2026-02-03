using LensSplitter.Core.Models;
using LensSplitter.Parsing.Glass;
using LensSplitter.Parsing.Zmx;
using Xunit;

namespace LensSplitter.Parsing.Tests;

public class ZmxParserTests
{
    /// <summary>
    /// Creates a glass catalog with common test glasses.
    /// </summary>
    private static GlassCatalogManager CreateTestCatalog()
    {
        var catalog = new GlassCatalogManager();

        // Add common test glasses
        catalog.AddGlass(new Core.Models.Glass
        {
            Name = "BK7",
            Catalog = "TEST",
            Nd = 1.5168,
            Vd = 64.17,
            DispersionModel = DispersionModel.Constant,
            Coefficients = new[] { 1.5168 }
        });

        catalog.AddGlass(new Core.Models.Glass
        {
            Name = "N-BK7",
            Catalog = "TEST",
            Nd = 1.5168,
            Vd = 64.17,
            DispersionModel = DispersionModel.Constant,
            Coefficients = new[] { 1.5168 }
        });

        return catalog;
    }

    [Fact]
    public void Parse_ValidZmxContent_CreatesSystem()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        var parser = new ZmxParser(catalog);
        var zmxContent = CreateSimpleZmxContent();
        var tempPath = Path.GetTempFileName() + ".zmx";

        try
        {
            File.WriteAllText(tempPath, zmxContent, System.Text.Encoding.Unicode);

            // Act
            var system = parser.Parse(tempPath);

            // Assert
            Assert.NotNull(system);
            Assert.True(system.Surfaces.Count > 0);
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
        var parser = new ZmxParser();

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => parser.Parse("nonexistent.zmx"));
    }

    [Fact]
    public void Parse_ExtractsWavelengths()
    {
        // Arrange
        var catalog = CreateTestCatalog();
        var parser = new ZmxParser(catalog);
        var zmxContent = CreateZmxWithWavelengths();
        var tempPath = Path.GetTempFileName() + ".zmx";

        try
        {
            File.WriteAllText(tempPath, zmxContent, System.Text.Encoding.Unicode);

            // Act
            var system = parser.Parse(tempPath);

            // Assert
            Assert.True(system.Wavelengths.Count > 0);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Parse_ExtractsAperture()
    {
        // Arrange - No glass in this test content, so no catalog needed
        var parser = new ZmxParser();
        var zmxContent = CreateZmxWithAperture(15.0);
        var tempPath = Path.GetTempFileName() + ".zmx";

        try
        {
            File.WriteAllText(tempPath, zmxContent, System.Text.Encoding.Unicode);

            // Act
            var system = parser.Parse(tempPath);

            // Assert
            Assert.Equal(15.0, system.ApertureValue, 3);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private string CreateSimpleZmxContent()
    {
        return @"! Simple Test Lens
MODE SEQ
UNIT MM X W X CM MR CPMM
ENPD 10.0

SURF 0
TYPE STANDARD
CURV 0
DISZ INFINITY

SURF 1
TYPE STANDARD
CURV 0.02
DISZ 5.0
GLAS BK7
DIAM 10.0

SURF 2
TYPE STANDARD
CURV 0
DISZ 95.0
DIAM 10.0

SURF 3
TYPE STANDARD
CURV 0
DISZ 0
";
    }

    private string CreateZmxWithWavelengths()
    {
        return CreateSimpleZmxContent() + @"
WAVM 1 0.4861 1.0
WAVM 2 0.5876 1.0
WAVM 3 0.6563 1.0
";
    }

    private string CreateZmxWithAperture(double epd)
    {
        return $@"! Test Lens with Aperture
MODE SEQ
UNIT MM X W X CM MR CPMM
ENPD {epd}

SURF 0
TYPE STANDARD
CURV 0
DISZ INFINITY

SURF 1
TYPE STANDARD
CURV 0.02
DISZ 5.0
DIAM 10.0

SURF 2
TYPE STANDARD
CURV 0
DISZ 0
";
    }

    [Fact]
    public void Parse_FieldTypeAngle_ParsesCorrectly()
    {
        // Arrange
        var parser = new ZmxParser();
        var zmxContent = CreateZmxWithFieldType(0, 0, 14.0); // FTYP 0 = Angle
        var tempPath = Path.GetTempFileName() + ".zmx";

        try
        {
            File.WriteAllText(tempPath, zmxContent, System.Text.Encoding.Unicode);

            // Act
            var system = parser.Parse(tempPath);

            // Assert
            Assert.True(system.Fields.Count > 0);
            Assert.Equal(FieldType.Angle, system.Fields[0].FieldType);
            Assert.Equal(14.0, system.Fields[0].Y, 3);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Parse_FieldTypeObjectHeight_ParsesCorrectly()
    {
        // Arrange
        var parser = new ZmxParser();
        var zmxContent = CreateZmxWithFieldType(1, 0, 10.0); // FTYP 1 = Object Height
        var tempPath = Path.GetTempFileName() + ".zmx";

        try
        {
            File.WriteAllText(tempPath, zmxContent, System.Text.Encoding.Unicode);

            // Act
            var system = parser.Parse(tempPath);

            // Assert
            Assert.True(system.Fields.Count > 0);
            Assert.Equal(FieldType.ObjectHeight, system.Fields[0].FieldType);
            Assert.Equal(10.0, system.Fields[0].Y, 3);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Parse_FieldTypeParaxialImageHeight_ParsesCorrectly()
    {
        // Arrange
        var parser = new ZmxParser();
        var zmxContent = CreateZmxWithFieldType(2, 0, 21.6); // FTYP 2 = Paraxial Image Height
        var tempPath = Path.GetTempFileName() + ".zmx";

        try
        {
            File.WriteAllText(tempPath, zmxContent, System.Text.Encoding.Unicode);

            // Act
            var system = parser.Parse(tempPath);

            // Assert
            Assert.True(system.Fields.Count > 0);
            Assert.Equal(FieldType.ParaxialImageHeight, system.Fields[0].FieldType);
            Assert.Equal(21.6, system.Fields[0].Y, 3);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Parse_MultipleFields_ParsesCorrectly()
    {
        // Arrange
        var parser = new ZmxParser();
        var zmxContent = CreateZmxWithMultipleFields();
        var tempPath = Path.GetTempFileName() + ".zmx";

        try
        {
            File.WriteAllText(tempPath, zmxContent, System.Text.Encoding.Unicode);

            // Act
            var system = parser.Parse(tempPath);

            // Assert
            Assert.Equal(3, system.Fields.Count);
            Assert.Equal(0.0, system.Fields[0].Y, 3);   // On-axis
            Assert.Equal(7.0, system.Fields[1].Y, 3);   // 0.5 field
            Assert.Equal(14.0, system.Fields[2].Y, 3);  // Full field
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private string CreateZmxWithFieldType(int fieldType, double xField, double yField)
    {
        return $@"! Test Lens with Field Type
MODE SEQ
UNIT MM X W X CM MR CPMM
ENPD 10.0
FTYP {fieldType} 0 0 0 0 0 0
XFLN {xField}
YFLN {yField}

SURF 0
TYPE STANDARD
CURV 0
DISZ INFINITY

SURF 1
TYPE STANDARD
CURV 0.02
DISZ 5.0
DIAM 10.0

SURF 2
TYPE STANDARD
CURV 0
DISZ 0
";
    }

    private string CreateZmxWithMultipleFields()
    {
        return @"! Test Lens with Multiple Fields
MODE SEQ
UNIT MM X W X CM MR CPMM
ENPD 10.0
FTYP 0 0 0 0 0 0 0
XFLN 0 0 0
YFLN 0 7.0 14.0

SURF 0
TYPE STANDARD
CURV 0
DISZ INFINITY

SURF 1
TYPE STANDARD
CURV 0.02
DISZ 5.0
DIAM 10.0

SURF 2
TYPE STANDARD
CURV 0
DISZ 0
";
    }
}
