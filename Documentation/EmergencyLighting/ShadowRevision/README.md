# Emergency Decon shadow lighting

Applied to Evaluation, Tutorial, and Tutorial_Hand on top of main (8b1a022).

Two wide real-time spot lights add soft shadows around the patient bed and treatment area. The existing point fills are reduced from 1.0 to 0.35 so the shadows remain visible. Existing baked lightmaps and probes are retained; no rebake is required.

The Mobile URP asset now supports additional-light shadows and soft shadows (2048 atlas). This is a shared pipeline setting, so other scenes using this asset can also render shadows from their existing shadow-enabled lights. The new lights themselves exist only in the three Emergency Decon scenes.

Use Tools > RadCare > Emergency Lighting > Enhance Shadows in Three Scenes to reapply. Save unrelated scene edits first. Before/After images use the same review camera; Before disables the new spots and restores fill intensity to 1.0, with shadow-capable pipeline settings retained.

Validation: Unity 6000.3.0f1 compiled the editor tool and executed it for all three scenes; rendered output inspected. Gameplay MonoBehaviours, colliders, and rigidbodies compared against the base commit. No changes to wound treatment or detector logic. Actual headset frame rate and interactive VR play were not tested; the two real-time shadow lights add GPU cost.
