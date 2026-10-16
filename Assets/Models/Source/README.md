# Razorback source model

`Razorback_Blueprint_v2.glb` is the owner-supplied Razorback revised against the
project's existing orthographic blueprint. Its SHA-256 is
`e943a21e54606b991ffc76c98444414debc5905b58a5dd61e5019fbf469dbae3`.

The editable builder, original v1 and standalone comparison remain in the owner's
Razorback v2 delivery ZIP and working artifacts. This GLB is the source for the
bundled native asset; it is not copied into the installed runtime separately.

Rebuild from the repository root with Python and NumPy:

    python tools/Convert-HoloScene.py Assets/Models/Source/Razorback_Blueprint_v2.glb Assets/Models/Razorback.ship 150000 FX

The final `FX` argument excludes the optional drive glow for the idle model. No
geometry reduction is needed. See `../ATTRIBUTION.txt` for source and rights notes.
