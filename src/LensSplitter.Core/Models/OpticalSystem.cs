namespace LensSplitter.Core.Models;

/// <summary>
/// Represents a complete optical system with surfaces, wavelengths, and fields.
/// </summary>
public class OpticalSystem
{
    /// <summary>
    /// Gets or sets the name of the optical system.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets notes or description for the system.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Gets or sets the list of surfaces in the system.
    /// Surface 0 is the object, last surface is the image.
    /// </summary>
    public List<Surface> Surfaces { get; set; } = new();

    /// <summary>
    /// Gets or sets the wavelengths used for analysis.
    /// </summary>
    public List<Wavelength> Wavelengths { get; set; } = new();

    /// <summary>
    /// Gets or sets the field points.
    /// </summary>
    public List<Field> Fields { get; set; } = new();

    /// <summary>
    /// Gets or sets the aperture specification type.
    /// </summary>
    public ApertureType ApertureType { get; set; } = ApertureType.EntrancePupilDiameter;

    /// <summary>
    /// Gets or sets the aperture value (interpretation depends on ApertureType).
    /// </summary>
    public double ApertureValue { get; set; } = 10.0;

    /// <summary>
    /// Gets the index of the stop surface.
    /// Returns -1 if no stop is defined.
    /// </summary>
    public int StopIndex => Surfaces.FindIndex(s => s.IsStop);

    /// <summary>
    /// Gets the entrance pupil diameter.
    /// </summary>
    public double EntrancePupilDiameter
    {
        get
        {
            return ApertureType switch
            {
                ApertureType.EntrancePupilDiameter => ApertureValue,
                // For other types, would need paraxial calculation
                _ => ApertureValue
            };
        }
    }

    /// <summary>
    /// Gets the primary wavelength.
    /// </summary>
    public Wavelength PrimaryWavelength =>
        Wavelengths.FirstOrDefault(w => w.IsPrimary) ?? Wavelengths.FirstOrDefault() ?? Wavelength.DLine;

    /// <summary>
    /// Gets the number of optical surfaces (excluding object and image).
    /// </summary>
    public int OpticalSurfaceCount => Math.Max(0, Surfaces.Count - 2);

    /// <summary>
    /// Gets the lens elements in the system.
    /// </summary>
    /// <returns>List of lens elements.</returns>
    public List<LensElement> GetLensElements()
    {
        var elements = new List<LensElement>();

        // Skip object surface (index 0) and image surface (last)
        int elementIndex = 0;
        for (int i = 1; i < Surfaces.Count - 1; i++)
        {
            var surface = Surfaces[i];

            // Check if this surface has glass (starts an element)
            if (surface.Glass != null && surface.Glass.Name != "AIR" && !string.IsNullOrEmpty(surface.GlassName))
            {
                // Find the rear surface (next surface with air or end)
                if (i + 1 < Surfaces.Count)
                {
                    var element = new LensElement
                    {
                        Index = elementIndex++,
                        FrontSurface = surface,
                        RearSurface = Surfaces[i + 1],
                        Glass = surface.Glass
                    };
                    elements.Add(element);
                }
            }
            else if (surface.GlassName != null && surface.GlassName != "AIR" && surface.GlassName != string.Empty)
            {
                // Glass reference exists but not yet resolved
                if (i + 1 < Surfaces.Count)
                {
                    var element = new LensElement
                    {
                        Index = elementIndex++,
                        FrontSurface = surface,
                        RearSurface = Surfaces[i + 1],
                        Glass = surface.Glass ?? Glass.Ideal(1.5) // Temporary placeholder
                    };
                    elements.Add(element);
                }
            }
        }

        return elements;
    }

    /// <summary>
    /// Gets a specific lens element by index.
    /// </summary>
    /// <param name="elementIndex">The element index (0-based).</param>
    /// <returns>The lens element, or null if not found.</returns>
    public LensElement? GetElement(int elementIndex)
    {
        var elements = GetLensElements();
        return elementIndex >= 0 && elementIndex < elements.Count ? elements[elementIndex] : null;
    }

    /// <summary>
    /// Gets the total track length (sum of all thicknesses).
    /// </summary>
    public double TotalTrack => Surfaces.Sum(s => s.Thickness);

    /// <summary>
    /// Creates a deep copy of this optical system.
    /// </summary>
    public OpticalSystem Clone()
    {
        return new OpticalSystem
        {
            Name = Name,
            Notes = Notes,
            Surfaces = Surfaces.Select(s => s.Clone()).ToList(),
            Wavelengths = Wavelengths.Select(w => new Wavelength(w.ValueMicrons, w.IsPrimary, w.Weight)).ToList(),
            Fields = Fields.Select(f => new Field(f.X, f.Y, f.FieldType, f.Weight)).ToList(),
            ApertureType = ApertureType,
            ApertureValue = ApertureValue
        };
    }

    /// <summary>
    /// Ensures the system has valid wavelengths (adds d-line if empty).
    /// </summary>
    public void EnsureWavelengths()
    {
        if (Wavelengths.Count == 0)
        {
            Wavelengths.Add(Wavelength.DLine);
        }
    }

    /// <summary>
    /// Ensures the system has valid fields (adds on-axis if empty).
    /// </summary>
    public void EnsureFields()
    {
        if (Fields.Count == 0)
        {
            Fields.Add(Field.OnAxis);
        }
    }

    /// <summary>
    /// Re-indexes all surfaces to have sequential indices starting from 0.
    /// </summary>
    public void ReindexSurfaces()
    {
        for (int i = 0; i < Surfaces.Count; i++)
        {
            Surfaces[i].Index = i;
        }
    }

    public override string ToString()
    {
        return $"{Name}: {OpticalSurfaceCount} surfaces, {GetLensElements().Count} elements";
    }
}
