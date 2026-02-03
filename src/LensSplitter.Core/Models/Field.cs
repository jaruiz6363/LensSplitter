namespace LensSplitter.Core.Models;

/// <summary>
/// Specifies the type of field specification.
/// </summary>
public enum FieldType
{
    /// <summary>
    /// Field specified as angle in degrees.
    /// </summary>
    Angle,

    /// <summary>
    /// Field specified as object height.
    /// </summary>
    ObjectHeight,

    /// <summary>
    /// Field specified as image height.
    /// </summary>
    ImageHeight,

    /// <summary>
    /// Field specified as paraxial image height.
    /// </summary>
    ParaxialImageHeight
}

/// <summary>
/// Represents a field point for ray tracing.
/// </summary>
public class Field
{
    /// <summary>
    /// Gets or sets the field type.
    /// </summary>
    public FieldType FieldType { get; set; } = FieldType.Angle;

    /// <summary>
    /// Gets or sets the X field value.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Gets or sets the Y field value.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Gets or sets the weight for this field in merit function calculations.
    /// </summary>
    public double Weight { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the X vignetting factor.
    /// </summary>
    public double VignettingX { get; set; }

    /// <summary>
    /// Gets or sets the Y vignetting factor.
    /// </summary>
    public double VignettingY { get; set; }

    public Field() { }

    public Field(double x, double y, FieldType fieldType = FieldType.Angle, double weight = 1.0)
    {
        X = x;
        Y = y;
        FieldType = fieldType;
        Weight = weight;
    }

    /// <summary>
    /// On-axis field point.
    /// </summary>
    public static Field OnAxis => new(0, 0);
}
