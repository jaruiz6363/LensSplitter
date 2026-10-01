"""Build a lens inside Optiland from this program's own parsed prescription, and ask it for
Seidel sums and real rays.

Why build it here rather than let Optiland read the lens file: optiland 0.6.2 resolves glass
names without the catalog the file names (Schott F4 arrives as CDGM's F4), so a lens it
imported is not the lens this program analysed. Everything below is handed over explicitly:
radii, thicknesses, conics, even-asphere coefficients, indices at one wavelength and the stop.

The C# side (OptilandOptic.cs) calls `build` once per lens, then `seidels` and `trace`,
passing JSON and getting JSON back, so the Python.NET surface stays small.
"""

from __future__ import annotations

import json

import numpy as np
from optiland.aberrations.third_order import ThirdOrderAberrations
from optiland.materials import IdealMaterial
from optiland.optic import Optic
from optiland.rays import RealRays


def build(spec_json: str):
    """Build an Optic from a JSON prescription. Returns the Optic."""
    spec = json.loads(spec_json)
    optic = Optic()

    for i, s in enumerate(spec["surfaces"]):
        kwargs = {
            "index": i,
            "thickness": np.inf if s["thickness"] is None else s["thickness"],
            "is_stop": bool(s.get("is_stop", False)),
            "radius": np.inf if s["radius"] is None else s["radius"],
            "conic": s.get("conic", 0.0),
        }

        coefficients = s.get("coefficients") or []
        if coefficients:
            # Optiland's even asphere is sum C_i r^(2i) from i = 1, so coefficients[0]
            # multiplies r^2 - the same indexing as Surface.AsphericCoefficients here.
            kwargs["surface_type"] = "even_asphere"
            kwargs["coefficients"] = list(coefficients)

        if s.get("mirror", False):
            kwargs["material"] = "mirror"
        else:
            n = s.get("index_after", 1.0)
            kwargs["material"] = "air" if abs(n - 1.0) < 1e-12 else IdealMaterial(n=n)

        optic.surfaces.add(**kwargs)

    optic.set_aperture(aperture_type="EPD", value=spec["epd"])
    optic.fields.set_type(spec["field_type"])
    optic.fields.add(y=0.0)
    if spec["max_field"] > 0:
        optic.fields.add(y=spec["max_field"])
    optic.wavelengths.add(value=spec["wavelength_um"], is_primary=True)
    return optic


def _floats(a) -> list:
    """Plain floats for JSON, with NaN and infinity as null, which JSON can carry."""
    return [float(v) if np.isfinite(v) else None for v in np.asarray(a, dtype=float).ravel()]


def seidels(optic) -> str:
    """Optiland's Seidel sums: the totals from its public API, and the per-surface split.

    optic.aberrations.seidels() returns only the totals. The split comes from the same class's
    per-surface terms, scaled exactly as its _sum_seidels scales their sum - private API, so the
    C# side checks that the split adds up to the public totals before believing it.
    """
    totals = _floats(optic.aberrations.seidels())

    t = ThirdOrderAberrations(optic)
    t._precalculations()
    factor = float(np.asarray(t._n[-1] * t._ua[-1] * 2, dtype=float).ravel()[0])
    split = []
    for term in (t._TSC_term, t._CC_term, t._TAC_term, t._TPC_term, t._DC_term):
        split.append(_floats(-np.asarray(t._compute_over_surfaces(term), dtype=float).ravel() * factor))

    return json.dumps({"totals": totals, "per_surface": split})


def trace(optic, spec_json: str) -> str:
    """Trace rays launched exactly as this program's RealRayTrace launches them.

    The launch is NOT Optiland's aiming. Optiland's paraxial trace takes a surface's power
    from its radius alone and ignores an r^2 coefficient, so on such a design its paraxial
    entrance pupil is not this program's, and "pupil coordinate 0.7" would mean a different
    ray on each side. Here a collimated ray at the field angle crosses THIS program's paraxial
    entrance pupil at the fractional coordinates asked for, and from there on everything is
    Optiland's own real-ray trace. The image surface was placed at this program's paraxial
    focus when the lens was built.
    """
    spec = json.loads(spec_json)
    field = np.radians(np.asarray(spec["field_deg"], dtype=float))
    py = np.asarray(spec["py"], dtype=float)
    pz = np.asarray(spec["pz"], dtype=float)
    epr, ep = spec["epr"], spec["ep"]

    z1 = float(np.asarray(optic.surfaces[1].geometry.cs.z, dtype=float).ravel()[0])
    L = np.zeros_like(field)
    M = np.sin(field)
    N = np.cos(field)

    # The pupil point, then back along the ray to a plane safely in front of surface 1.
    xp, yp, zp = pz * epr, py * epr, np.full_like(field, z1 + ep)
    back = (zp - (z1 - spec["start_offset"])) / N
    rays = RealRays(xp - back * L, yp - back * M, zp - back * N, L, M, N,
                    np.ones_like(field), np.full_like(field, optic.primary_wavelength))
    optic.surfaces.trace(rays)

    x = np.asarray(rays.x, dtype=float).ravel()
    y = np.asarray(rays.y, dtype=float).ravel()
    ok = np.isfinite(x) & np.isfinite(y) & (np.asarray(rays.i, dtype=float).ravel() > 0)

    # A ray that missed a surface comes back NaN, which JSON cannot carry; it goes as null.
    def column(v):
        return [float(a) if good else None for a, good in zip(v, ok)]

    return json.dumps({"x": column(x), "y": column(y), "ok": ok.tolist()})


def describe(optic) -> str:
    """First-order data from Optiland's own paraxial trace, for checking the lens arrived."""
    _, ub = optic.paraxial.chief_ray()
    return json.dumps({
        "efl": _floats(optic.paraxial.f2())[0],
        "epd": _floats(optic.paraxial.EPD())[0],
        "epl": _floats(optic.paraxial.EPL())[0],
        # The paraxial chief ray's slope in object space, at the full field.
        "chief_slope": _floats(ub)[0],
        "surfaces": len(optic.surfaces.surfaces),
    })


def sags(path: str, r: float) -> str:
    """Load a lens FILE with Optiland's own reader and return each surface's sag at height r.

    The one place Optiland reads a file this program wrote: a save-back into Optiland .json is
    only right if Optiland itself loads it and finds the figuring that was written.
    """
    import optiland.fileio as fio

    optic = fio.load_optiland_file(path)
    out = []
    for s in optic.surfaces.surfaces:
        g = s.geometry
        out.append({
            "type": type(g).__name__,
            "sag": _floats(g.sag(np.array([0.0]), np.array([r])))[0],
        })
    return json.dumps(out)
