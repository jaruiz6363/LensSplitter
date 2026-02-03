namespace LensSplitter.Core.Models;

/// <summary>
/// Specifies how the system aperture is defined.
/// </summary>
public enum ApertureType
{
    /// <summary>
    /// Entrance pupil diameter.
    /// </summary>
    EntrancePupilDiameter,

    /// <summary>
    /// Image space F-number.
    /// </summary>
    ImageSpaceFNumber,

    /// <summary>
    /// Object space numerical aperture.
    /// </summary>
    ObjectSpaceNA,

    /// <summary>
    /// Float by stop size.
    /// </summary>
    FloatByStopSize
}
