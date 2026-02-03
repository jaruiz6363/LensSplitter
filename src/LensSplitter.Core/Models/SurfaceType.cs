namespace LensSplitter.Core.Models;

/// <summary>
/// Specifies the type of optical surface.
/// </summary>
public enum SurfaceType
{
    /// <summary>
    /// Standard spherical surface.
    /// </summary>
    Standard,

    /// <summary>
    /// Plane (flat) surface.
    /// </summary>
    Plane,

    /// <summary>
    /// Even asphere surface.
    /// </summary>
    EvenAsphere,

    /// <summary>
    /// Object surface (first surface in the system).
    /// </summary>
    Object,

    /// <summary>
    /// Image surface (last surface in the system).
    /// </summary>
    Image,

    /// <summary>
    /// Coordinate break (for tilts and decenters).
    /// </summary>
    CoordinateBreak,

    /// <summary>
    /// Paraxial lens (ideal thin lens).
    /// </summary>
    Paraxial
}
