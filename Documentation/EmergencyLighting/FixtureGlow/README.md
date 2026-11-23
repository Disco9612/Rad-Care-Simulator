# Fixture glow

The fixture faces previously appeared as flat white panels. URP Unlit BaseColor is now HDR (9, 8.784, 8.352); the new bloom-only profile uses threshold 2.2, intensity 0.22, and scatter 0.65. Existing sample profiles are replaced in the three Emergency Decon scenes so enabling camera post-processing does not also activate sample vignette/color grading. XR cameras have HDR and post-processing enabled. Mobile pipeline HDR is enabled (a shared setting with potential GPU/bandwidth impact).

Unity 6000.3.0f1 compiled and executed the editor tool in all three scenes. Rendered previews included here use HDR targets and convert linear readback to sRGB for PNG export. Scene comparison confirms only camera HDR/post-processing flags and volume profile references changed. Existing shadows, baked lighting, and gameplay components are retained. Physical headset performance and appearance are not tested.

Reapply with Tools > RadCare > Emergency Lighting > Enhance Fixture Glow.

## Hand bloom correction

The primary OpenXR and Oculus hand renderers in all three scenes now use scene-specific materials with RadCare/BloomSafeHand. This retains the Meta hand shader passes, wrist alpha, skinning and interaction feedback, but clamps fill and outline RGB output to 0..1 before HDR bloom. Package source and shared Hospital hand materials are unchanged. Bloom threshold is raised to 2.2 and fixture radiance to 9 to preserve fixture glow while rejecting ordinary surface brightness.

The HDR render regression test feeds deliberately overbright hand colors to the original and modified shaders on a sphere. Original output peaked at 2048; bounded output peaked at 1. This verifies the brightness bound, not reproduction of the user's exact tracked-hand pose. All three scenes have four primary hand renderers replaced. No gameplay MonoBehaviour or collider changes. Headset retest is still required. Existing PNGs show the earlier fixture tuning and are not hand-fix verification images.
