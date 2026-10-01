# The MCP server

`abcalc-mcp` exposes the calculator over the Model Context Protocol, so an assistant can read
a lens file and ask for its aberration coefficients directly. It is a thin layer over the same
`ReportWriter` the command line uses, so the two cannot give different answers.

## Registering it, the easy way

    dotnet build tools/mcpsetup -c Release
    tools/mcpsetup/bin/Release/net8.0-windows/mcp-setup.exe

A small window. It finds the built server, or builds it for you; it says which Claude
clients it found; and it writes the registration.

**It edits a file you did not write and probably care about.** Your Claude configuration
usually holds other MCP servers, so `mcp-setup` copies the file aside with a timestamp
before every write, merges rather than replaces, leaves every key it does not own alone,
and prints the list of registered servers before and after so you can see for yourself that
nothing went missing. If it ever does, it says which one and tells you to restore the
backup. `ClaudeConfigTests` covers all of that; every test in it is a test that something
was left alone.

There is a **Remove** button too, so trying it is not a commitment.

**Choose the scope.** Claude Code keeps three, and the window defaults to `user` rather than
the CLI's own default of `local`:

| scope | where the server is available |
|---|---|
| `user` | everywhere, for you |
| `local` | this folder only |
| `project` | this folder, and committed to the repository as `.mcp.json` |

`local` is the CLI default and it is rarely what someone wants from a lens tool: registering
it inside the repository and then finding it absent everywhere else looks exactly like the
registration having failed.

**Restart afterwards.** A client reads its MCP servers once, when it starts. Until then it
will not list the server and nothing is wrong - which is a confusing enough half-hour that
the window now says so in a box you cannot miss, rather than in a line at the bottom.

## Registering it by hand

    dotnet build src/AberrationCalculator.Mcp -c Release

Then point the client at the built executable. For Claude Code:

    claude mcp add abcalc -- <repo>/src/AberrationCalculator.Mcp/bin/Release/net8.0/abcalc-mcp

or, in a client that takes JSON:

    {
      "mcpServers": {
        "abcalc": {
          "command": "<repo>/src/AberrationCalculator.Mcp/bin/Release/net8.0/abcalc-mcp"
        }
      }
    }

## The tools

Every REPORTING tool takes `lens_file` and an optional `glass_dir`. Most return tab-separated
tables, so a caller can parse a number rather than scrape prose; the four marked *text* are
verdicts and breakdowns that read far better ruled than flattened into one row per cell.

Three tools take more than a lens and so carry their own arguments, listed separately below:
`optimize`, which CHANGES a design; `nodal_aberrations`, which has to be told how the surfaces
are out of place; and `base_path`, which says what folder bare file names are taken against.

| tool | what it gives |
|---|---|
| `analyse_lens` | the whole analysis, formatted to read (*text*) |
| `prescription` | one row per surface |
| `first_order` | focal length, pupils, F-number, track |
| `paraxial_rays` | marginal and chief ray at every surface |
| `indices` | each material at each wavelength |
| `seidel` | third-order coefficients, per surface and totalled |
| `buchdahl` | third, fifth and seventh order |
| `rms_spot` | predicted RMS spot per field and wavelength, and PRMSA |
| `contributions` | which aberration is costing the design its performance |
| `surface_breakdown` | intrinsic, aspheric and induced, per surface |
| `quaternary_spherical` | the coefficient of **quaternary - ninth-order - spherical aberration**, per surface, with the intermediate rows Buchdahl prints beside it. Everything else here stops at the seventh order, so an on-axis residual has had to be *attributed* to the ninth rather than measured; this makes it arithmetic. Buchdahl IV, *J. Opt. Soc. Am.* **48**, 757 (1958). **Spherical surfaces only** - a figured design is refused with its reason, because he published no aspheric arrangement at this order |
| `surface_share` | each surface's share of the spot, and how much of it is induced |
| `seventh_order` | third, fifth and seventh order per surface, intrinsic and induced, the seventh by the Forbes series trace - the one that handles aspheres. Takes an optional `degree`, 3 to 8: three is the seventh order and higher carries the orders ABOVE it, which is how to find out whether a design's residual is seventh order at all (*text*) |
| `aspheric_screen` | whether a design would exercise the aspheric seventh-order path hard enough to test it (*text*) |
| `distortion_from_coefficients` | how far the coefficients can be trusted for distortion, against rays - NOT the way to get a distortion figure, for which the traced column beside them is the answer (*text*) |

### `optimize`

Optimises a lens against a merit function given inline as text, and reports what changed.
Every derivative it uses is analytic - see [docs/optimizer.md](optimizer.md) - including through
PRMSA, the predicted spot. **Conics and even aspheres are carried**, as values and as variables
(`CC`, `A4`, `A6`, `A8`): a figured surface takes the aspheric arrangement of Buchdahl's Sec. 85,
which agrees with Forbes' series trace to 2E-10 or better on a refracting design (Forbes
declines a mirror, where the reflected rays are the check), a spherical one takes his own
published table bit for bit, and a figured flat in collimated light takes the same chain in
Laurent series arithmetic, differentiated. A design is refused only when that series route cannot
vouch for its answer. The reporting tools above are unaffected and handle figuring throughout.

**Any of the thirty-seven aberration coefficients can be targeted**, for the system or for one
surface, written as its own name -
`B, 1, TAR 0`, `Tau15, 2, TAR 0`, `M2, 1, TAR 0, 5` for surface 5 alone, `M2.IND, 1, MAX 1e-3, 5`
for the part of it induced there - which is how the
report prints them. The shares add to the total exactly. They are free in bulk,
because all thirty-seven come out of one run of the scheme.

The tool's own description carries the full merit-function and variables syntax, generated from
the same tables the parsers read, and `McpToolsTests` requires every example in it to parse. A
tool description that lies about its own arguments is worse than one that says nothing, because
the caller has no way to check it.

| argument | |
|---|---|
| `lens_file` | the lens to optimise. It is read, never written |
| `merit` | the merit function as text, or `merit_file` for a path |
| `variables` | variables and pickups as text, in the `.var` format. A `.lhlt` carries its own |
| `method` | `lm`, `psd2`, `psd3` (default) or `hj` |
| `iterations` | local iterations, or iterations per hop |
| `hops`, `chains`, `seed`, `hop_sigma` | basin hopping, off by default. `hop_sigma` is the per-hop kick in units of each variable's own scale, default 0.001 - a whisper rather than a shove, and measured to be right: a large kick lands the design somewhere unrelated and the acceptance test then compares two unfinished designs |
| `initial_perturb_sigma` | the kick on the **first hop only**, before the design has ever been minimised. Default 0.001, the same as `hop_sigma`. Its job is different: it breaks exact symmetry, since a design sitting on a stationary point has nowhere to go. Raise it to start from a deliberately disturbed design - useful off a skeleton, where the starting point is a guess - without making every later hop that violent |
| `hop_figuring` | whether a hop also kicks the **conic and aspheric terms**. Default false. They are still *optimised* at every hop; this governs the random kick only. A figuring term is a nearly-linear correction the local stage refits from wherever it starts, so throwing it does not choose a different basin - it discards a figure that is about to be fitted again. Set it true to kick them anyway, which is defensible for a conic: at -1 and at 0 that is a genuinely different surface, not a small correction |
| `glass_substitution` | name of a substitution catalogue the hopping may take glasses from, e.g. `CoreSet28` |
| `save_to` | where to write the result. Under hopping this is a **folder**, and one design per chain goes into it. **Nothing is written without it**. A CODE V, OSLO or OPTALIX design whose conic or aspheric terms moved is not saved - the result says `NOT SAVED` and why, and the report above it holds the values |

**The lens on disk is never modified.** A run that made the design worse costs nothing, and
the report says so rather than handing back something nobody asked for.

Formats are taken from the extension: `.zmx`, `.seq`, `.otx`, `.opt`, `.len`, `.osl`, `.json`
(Optiland) and `.lhlt`.


### `nodal_aberrations`

What the aberrations do when the surfaces are **not on a common axis**: where each surface's
aberration field has been displaced to, where the **nodes** of the system's field are, and what
the fifth order does to the third. Third and fifth order, both in the design's own units. This
is the command line's `--nat`, and [docs/nodal-aberration-theory.md](nodal-aberration-theory.md) is the long form.

| argument | |
|---|---|
| `lens_file` | the lens to analyse. It is read, never written |
| `alignment` | the perturbation as text, in the `.align` grammar. Omit to read `<lens>.align` beside the lens |
| `full_field` | return the field **grid** as a tab-separated table instead of the report - magnitude and orientation of every aberration type over a grid of field points, which is what a node map is plotted from |
| `glass_dir` | optional folder of `.agf` catalogs |

The alignment text goes through the same parser the sidecar uses, so the grammar cannot drift
between the two ways of stating a perturbation, and a bad line comes back with its line number:

    TILT 2 Y 0.115      degrees, about the surface vertex
    DEC  3 X 0.05       the design's length units
    ZERN 1 Z10 0.0005   a Fringe Zernike overlay, as surface sag
    TILT 2 FREE         drops the tilt only, leaving any decentre

**Every tool on this page reads `<lens>.align` if it is there.** A perturbation is a statement
about one built instance rather than about the design, so it lives in a sidecar - but it is part
of what the lens *is* once stated, and the command line applies it to every analysis. The server
did not, for a while, which meant the same lens read one way through the CLI and another way
here with nothing to say so.

**On an aligned design it still runs and says so.** Every displacement is zero, the sums
collapse to the ordinary Seidel ones, and every node sits at the field centre: the theory
reducing correctly, not a case it declines.

### `base_path`

Sets the folder that bare file names are taken to mean, so `lens_file` can be `L.zmx` rather
than a full path.

| argument | |
|---|---|
| `path` | the folder. Omit to report the base in force without changing it |
| `clear` | forget the stored base |

**This matters more here than on the command line.** A terminal has a working directory the user
chose; this server has whatever the client started it in, which the user cannot see and cannot
change - so without a base, every path has to be absolute. Set it once at the start of a session
and every tool above resolves against it.

It is the same setting as the command line's `BASE`, kept between runs, and it reports where the
base came from as well as what it is - `--dir`, `ABCALC_DIR`, `BASE`, or the working directory,
in that order of precedence. An absolute path is never re-rooted.

## Two things worth knowing

**A glass name does not say whose glass it is.** Some formats carry a catalog and some do
not. Optiland's JSON, for instance, gives only a name, so "F2" resolves against whatever
catalogs happen to be loaded - and on the Cooke triplet in `tests/fixtures` the wrong vendor's
F2 moves the focal length from 50.000 to 49.063 with no error anywhere. Pass `glass_dir` when
it matters.

**Unresolved materials are reported, not thrown.** If a glass cannot be found the analysis
still runs and still returns, with a warning at the top of the result: every quantity that
depends on the missing glass is meaningless, and the caller has no other way to learn that.

## The protocol

JSON-RPC 2.0 over stdin and stdout, newline-delimited: `initialize`, `tools/list`,
`tools/call`, `ping`. It is implemented directly rather than through a package - three methods
is less than a preview dependency would cost, in a program whose point is that its numbers can
be traced to something. stdout carries the protocol and nothing else; diagnostics go to stderr,
because a stray line on stdout shows up at the client as a parse error rather than as whatever
actually went wrong.
