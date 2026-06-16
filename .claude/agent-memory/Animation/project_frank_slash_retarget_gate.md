---
name: frank-slash-retarget-gate
description: Frank Slash Pack ($50) retarget-to-Synty-Sidekick visual gate result (2026-06-16) — retarget clean, but verticality flattens at our 45deg top-down
metadata:
  type: project
---

Pre-purchase visual gate for Frank Slash Pack (free Tumbling_L trial clip) retargeted onto our player Synty Sidekick (Starter_02-avatar, isHuman). Tested in Greybox_CombatLab.

**Result — retarget quality: PASS (structurally).** Humanoid→Humanoid auto-retarget of Frank's clip onto Synty Sidekick produces a clean, coherent body — no exploded limbs, no detached parts, no foot/ground penetration in the poses checked. Closeup 3/4 shows a crisp deep-fold tumble pose. The big katana is a strong silhouette anchor. Bone telemetry confirms a real motion arc (headY 1.14→0.57→0.78→1.38→1.57 across 0.83s).

**Result — top-down readability: WEAK (the real concern).** At our actual game camera (45deg pitch, perspective fov50, ~7.5m on-screen char size), the dramatic forward-fold compresses into a nearly-upright compact blob. The tumble's verticality (the whole point of a roll) reads poorly from a steep top-down. The motion that's gorgeous in 3/4 mostly flattens from above. This is the lens, not the retarget — a Frank-pack purchase decision must weigh that many of its acrobatic/vertical melee flourishes will lose impact at our camera angle. Ground-plane, horizontal-sweep motions will survive; vertical/aerial ones won't.

**Import warning: NON-FATAL.** `m_AnimationImportWarnings` = the standard "translation animation that will be discarded" list (thigh/calf/spine/arm/finger bones) + "Activate translation DOF to improve retargeting quality." This is the normal Humanoid muscle-space message for any non-translation-DOF avatar; only minor secondary squash/stretch is dropped. Root motion and the readable pose arc survive intact. No action needed.

**Claude could NOT judge (user play gate):** dynamic feel / speed / 슬라이드 of the tumble — single-clip standalone playback with no code locomotion, stills only. Whether the flattened top-down read is "acceptable motion" vs "mushy" is a user-play call.

**Verdict handed to user:** retarget pipeline is sound; the gating risk is camera-angle readability of Frank's vertical-heavy motion vocabulary, not rig breakage.
