---
name: project-frank-fbx-animevent-gotchas
description: How to add AnimationEvents to Frank pack FBX clips without scaling/length corruption — edit the .meta directly with NORMALIZED times, never programmatic clipAnimations rebuild
metadata:
  type: project
---

Adding AnimationEvents to a Frank Slash Pack FBX clip (e.g. Katana S1_Attack01/02/03). Two coupled importer gotchas bit hard (2026-06-18, KatanaMelee combo wiring) — burned several iterations.

**★Gotcha 1 — FBX event `time` in the .meta is stored NORMALIZED [0..1], importer multiplies by clip length on import.** Verified on Frank Katana Attack01 (len 1.25s): meta `time: 0.342` → runtime event @0.4275s (0.342×1.25). So to land an event at absolute second `T`, write `time: T / clipLength` in the meta. Table used (all 3 land exactly): Attack01(1.25s) hit 0.428→meta 0.342, window 0.50→0.40, end 1.15→0.92. Attack02/03(1.333s) end 1.227→meta 0.92, etc. (0.92 = 92% end-marker, same normalized value across clips since it's a fraction.)

**★Gotcha 2 — `ModelImporter.clipAnimations = new ModelImporterClipAnimation[]{...}; SaveAndReimport()` EXPANDS clip length.** Even with explicit firstFrame=0/lastFrame=75, rebuilding the clipAnimations array programmatically made the take import at FULL length (1.25→1.4375s, ×1.15; 1.333→1.636, ×1.227) AND re-scaled event times. Root cause: pack uses `avatarSetup: 2` (CopyFromOther avatar) + empty `importer.clipAnimations` (auto clips); the reconstructed array lost the trim binding. **DO NOT build clipAnimations in code for these.**

**✓ Correct path = edit the `.FBX.meta` clipAnimations `events:` block DIRECTLY with the Edit tool, then `AssetDatabase.ImportAsset(path, ForceUpdate)`.** This preserves length (no take expansion) and applies the normalized→absolute scaling predictably. This is also how the project's pre-existing Attack01 hit event was placed (it worked).

**Reversion note:** Frank_Slash_Pack is **git-untracked** (added this session) → can't `git checkout` a corrupted .meta. The original meta values are the only backup; capture them via Read BEFORE editing. Pristine pack clips ship with `events: []` and `maskType: 1`.

**Benign warning to ignore:** every reimport logs "Rig Error: Copied Avatar Rig Configuration mis-match. Bone length ... position error = N mm" (thigh/hand/etc., <6mm) + "translation animation will be discarded". This is the standard Frank copy-avatar + translation-DOF-discard warning ([[project_frank_slash_retarget_gate]]), NOT caused by event edits. unity-mcp RunCommand reports the whole command as `success:false` when ANY warning fires — read the `[Log]` lines; the work usually succeeded.

**Blade-impact frame measurement (how hit frames were chosen):** instantiate `Frank_Katana_Skin.FBX`, `clip.SampleAnimation(inst, t)` per frame, read `Weapon_Blade` bone world pos, compute per-frame speed. Peak blade speed = the cut. Bone telemetry is reliable per-step ([[feedback_editmode_capture_one_pose_per_invoke]] — only SkinnedMesh render is stale, not bone transforms). Measured: Attack01 down-diag cut @f26, Attack02 deep downward chop @f26, Attack03 upward finisher @f32. Good 3-beat combo shape.
