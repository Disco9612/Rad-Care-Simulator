# Fixture glow

The fixture faces previously appeared as flat white panels. URP Unlit BaseColor is now HDR (5, 4.88, 4.64); the new bloom-only profile uses threshold 1.2, intensity 0.22, and scatter 0.65. Existing sample profiles are replaced in the three Emergency Decon scenes so enabling camera post-processing does not also activate sample vignette/color grading. XR cameras have HDR and post-processing enabled. Mobile pipeline HDR is enabled (a shared setting with potential GPU/bandwidth impact).

Unity 6000.3.0f1 compiled and executed the editor tool in all three scenes. Rendered previews included here use HDR targets and convert linear readback to sRGB for PNG export. Scene comparison confirms only camera HDR/post-processing flags and volume profile references changed. Existing shadows, baked lighting, and gameplay components are retained. Physical headset performance and appearance are not tested.

Reapply with Tools > RadCare > Emergency Lighting > Enhance Fixture Glow.
