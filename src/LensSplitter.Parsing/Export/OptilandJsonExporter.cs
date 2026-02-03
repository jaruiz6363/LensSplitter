using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LensSplitter.Core.Models;

namespace LensSplitter.Parsing.Export;

/// <summary>
/// Exports optical systems to Optiland JSON format.
/// </summary>
public class OptilandJsonExporter
{
    /// <summary>
    /// Exports an optical system to an Optiland JSON file.
    /// </summary>
    /// <param name="system">The optical system to export.</param>
    /// <param name="path">Output file path.</param>
    public void Export(OpticalSystem system, string path)
    {
        var content = GenerateJson(system);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// Generates Optiland JSON content.
    /// </summary>
    /// <param name="system">The optical system.</param>
    /// <returns>JSON content.</returns>
    public string GenerateJson(OpticalSystem system)
    {
        var sb = new StringBuilder();
        using var sw = new StringWriter(sb);
        using var writer = new Utf8JsonWriter(new MemoryStream(), new JsonWriterOptions { Indented = true });

        // Build JSON manually to handle infinity values
        var ms = new MemoryStream();
        using (var jw = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
        {
            WriteSystem(jw, system);
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private void WriteSystem(Utf8JsonWriter writer, OpticalSystem system)
    {
        writer.WriteStartObject();

        writer.WriteNumber("version", 1.0);

        // Aperture
        writer.WriteStartObject("aperture");
        writer.WriteString("type", system.ApertureType switch
        {
            ApertureType.EntrancePupilDiameter => "EPD",
            ApertureType.ImageSpaceFNumber => "imageFNO",
            _ => "EPD"
        });
        writer.WriteNumber("value", system.ApertureValue);
        writer.WriteBoolean("object_space_telecentric", false);
        writer.WriteEndObject();

        // Fields
        // Determine container-level field type from first field
        var containerFieldType = system.Fields.Count > 0 ? system.Fields[0].FieldType : FieldType.Angle;
        string containerFieldTypeStr = containerFieldType switch
        {
            FieldType.Angle => "angle",
            FieldType.ObjectHeight => "object_height",
            FieldType.ParaxialImageHeight => "paraxial_image_height",
            FieldType.ImageHeight => "image_height",
            _ => "angle"
        };

        writer.WriteStartObject("fields");
        writer.WriteStartArray("fields");
        foreach (var field in system.Fields)
        {
            writer.WriteStartObject();
            writer.WriteString("field_type", field.FieldType switch
            {
                FieldType.Angle => "angle",
                FieldType.ObjectHeight => "object_height",
                FieldType.ParaxialImageHeight => "paraxial_image_height",
                FieldType.ImageHeight => "image_height",
                _ => "angle"
            });
            writer.WriteNumber("x", field.X);
            writer.WriteNumber("y", field.Y);
            writer.WriteNumber("vx", field.VignettingX);
            writer.WriteNumber("vy", field.VignettingY);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteBoolean("telecentric", false);
        writer.WriteString("field_type", containerFieldTypeStr);
        writer.WriteBoolean("object_space_telecentric", false);
        writer.WriteEndObject();

        // Wavelengths
        writer.WriteStartObject("wavelengths");
        writer.WriteStartArray("wavelengths");
        foreach (var wl in system.Wavelengths)
        {
            writer.WriteStartObject();
            writer.WriteNumber("value", wl.ValueMicrons);
            writer.WriteBoolean("is_primary", wl.IsPrimary);
            writer.WriteString("unit", "um");
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteString("polarization", "ignore");
        writer.WriteEndObject();

        // Pickups (empty)
        writer.WriteStartArray("pickups");
        writer.WriteEndArray();

        // Solves
        writer.WriteStartObject("solves");
        writer.WriteStartArray("solves");
        writer.WriteEndArray();
        writer.WriteEndObject();

        // Surface group
        writer.WriteStartObject("surface_group");
        WriteSurfaces(writer, system);
        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    private void WriteSurfaces(Utf8JsonWriter writer, OpticalSystem system)
    {
        writer.WriteStartArray("surfaces");

        double zPosition = 0;

        for (int i = 0; i < system.Surfaces.Count; i++)
        {
            var surface = system.Surfaces[i];
            bool isFirst = i == 0;

            // Calculate absolute Z position
            if (isFirst)
            {
                zPosition = double.NegativeInfinity;
            }
            else if (i == 1)
            {
                zPosition = 0;
            }
            else if (i > 1)
            {
                var prevSurface = system.Surfaces[i - 1];
                if (!double.IsInfinity(zPosition) && !double.IsInfinity(prevSurface.Thickness))
                {
                    zPosition += prevSurface.Thickness;
                }
            }

            writer.WriteStartObject();
            writer.WriteString("type", isFirst ? "ObjectSurface" : "Surface");

            // Geometry
            WriteGeometry(writer, surface, zPosition);

            // Material pre
            if (i > 0)
            {
                writer.WritePropertyName("material_pre");
                WriteMaterial(writer, system.Surfaces[i - 1]);
            }

            // Material post
            writer.WritePropertyName("material_post");
            WriteMaterial(writer, surface);

            writer.WriteBoolean("is_stop", surface.IsStop);
            writer.WriteNull("aperture");
            writer.WriteNull("coating");
            writer.WriteNull("bsdf");
            writer.WriteBoolean("is_reflective", false);

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private void WriteGeometry(Utf8JsonWriter writer, Surface surface, double zPosition)
    {
        bool isPlane = surface.IsFlat;

        writer.WriteStartObject("geometry");
        writer.WriteString("type", isPlane ? "Plane" : "StandardGeometry");

        // Coordinate system
        writer.WriteStartObject("cs");
        writer.WriteNumber("x", 0);
        writer.WriteNumber("y", 0);
        WriteDoubleOrInfinity(writer, "z", zPosition);
        writer.WriteNumber("rx", 0);
        writer.WriteNumber("ry", 0);
        writer.WriteNumber("rz", 0);
        writer.WriteNull("reference_cs");
        writer.WriteEndObject();

        WriteDoubleOrInfinity(writer, "radius", surface.Radius);
        if (!isPlane)
        {
            writer.WriteNumber("conic", surface.Conic);
        }

        writer.WriteEndObject();
    }

    private void WriteMaterial(Utf8JsonWriter writer, Surface surface)
    {
        var glass = surface.Glass;

        writer.WriteStartObject();

        if (glass == null || glass.Name == "AIR" || string.IsNullOrEmpty(surface.GlassName))
        {
            writer.WriteString("type", "IdealMaterial");
            writer.WriteNumber("index", 1.0);
            writer.WriteNumber("absorp", 0.0);
        }
        else
        {
            writer.WriteString("type", "Material");
            writer.WriteString("name", glass.Name);
            if (!string.IsNullOrEmpty(glass.Catalog))
            {
                writer.WriteString("reference", glass.Catalog.ToLowerInvariant());
            }
            else
            {
                writer.WriteNull("reference");
            }
            writer.WriteBoolean("robust_search", true);
            writer.WriteNull("min_wavelength");
            writer.WriteNull("max_wavelength");
        }

        writer.WriteEndObject();
    }

    private void WriteDoubleOrInfinity(Utf8JsonWriter writer, string propertyName, double value)
    {
        if (double.IsPositiveInfinity(value))
        {
            writer.WriteString(propertyName, "Infinity");
        }
        else if (double.IsNegativeInfinity(value))
        {
            writer.WriteString(propertyName, "-Infinity");
        }
        else if (double.IsNaN(value))
        {
            writer.WriteString(propertyName, "NaN");
        }
        else
        {
            writer.WriteNumber(propertyName, value);
        }
    }
}
