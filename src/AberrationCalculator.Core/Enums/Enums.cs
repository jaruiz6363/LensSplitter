namespace AberrationCalculator.Core.Enums;

/// <summary>How a surface is shaped.</summary>
public enum SurfaceType
{
    /// <summary>Sphere or plane: sag from curvature alone.</summary>
    Standard = 0,

    /// <summary>Conic plus even-order polynomial terms in r².</summary>
    EvenAsphere = 1,

    /// <summary>Ideal thin lens of a given focal length; no thickness, no aberration.</summary>
    Paraxial = 2,

    /// <summary>
    /// A tilt/decentre between surfaces. Read so a file's structure survives, but this
    /// program analyses rotationally symmetric systems only: a non-trivial coordinate
    /// break puts a design outside what it can evaluate.
    /// </summary>
    CoordinateBreak = 3,

    /// <summary>A black-box ray-transfer matrix. Read, but not analysed.</summary>
    Abcd = 4,

    /// <summary>Recognised by a reader but not modelled here.</summary>
    Unsupported = 99,
}

/// <summary>How the system aperture is specified.</summary>
public enum ApertureType
{
    /// <summary>Entrance pupil diameter, in lens units.</summary>
    EPD = 0,

    /// <summary>Image-space F/number.</summary>
    FNumber = 1,

    /// <summary>Object-space numerical aperture, for finite conjugates.</summary>
    ObjectSpaceNA = 2,
}

/// <summary>How a field point is expressed.</summary>
public enum FieldType
{
    /// <summary>Angle in degrees, for an object at infinity.</summary>
    ObjectAngle = 0,

    /// <summary>Height in lens units, for a finite object.</summary>
    ObjectHeight = 1,
}

/// <summary>Where a surface's clear aperture comes from.</summary>
public enum SemiDiameterMode
{
    /// <summary>Sized to the beam that reaches the surface.</summary>
    Auto = 0,

    /// <summary>Held at the stored value.</summary>
    Fixed = 1,
}

/// <summary>
/// How Auto semi-diameters are derived. Carried so a file's setting survives a round trip;
/// this program reports the aperture a file declares rather than re-solving it.
/// </summary>
public enum SemiDiameterSolve
{
    /// <summary>From real traced rays.</summary>
    RealRay = 0,

    /// <summary>From the paraxial beam footprint.</summary>
    Paraxial = 1,
}

/// <summary>Whether rays are aimed at the stop. Read from files; not used in analysis here.</summary>
public enum RayAimingMode
{
    Off = 0,
    Real = 1,
    Robust = 2,
}

/// <summary>Which surface quantity a pickup drives.</summary>
public enum PickupParameter
{
    Curvature = 0,
    Thickness = 1,
    Conic = 2,
    SemiDiameter = 3,
    Material = 4,
}
