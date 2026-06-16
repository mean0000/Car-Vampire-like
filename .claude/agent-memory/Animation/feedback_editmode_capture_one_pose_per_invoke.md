---
name: editmode-capture-one-pose-per-invoke
description: Edit-mode SkinnedMeshRenderer capture trap — multiple cam.Render() in one script all render the SAME (stale) pose; one pose per RunCommand invocation is the only trustworthy way
metadata:
  type: feedback
---

In edit mode, stepping an Animator (`anim.Update(dt)`) then calling `cam.Render()` multiple times **within a single RunCommand script** produces frames that all show the SAME stale pose — even though `GetBoneTransform().position` telemetry between steps correctly shows distinct poses. The transform hierarchy updates, but the GPU-skinned SkinnedMeshRenderer the camera draws does NOT re-evaluate between renders without a real frame tick. `SkinnedMeshRenderer.BakeMesh()` into a throwaway mesh does NOT fix it (bakes to a copy, not the live draw).

**Why:** edit-mode has no frame loop; skinning runs lazily. Telemetry (Transform) and render (skinned mesh) desync within one synchronous script.

**How to apply:** For trustworthy *distinct* still frames across an animation arc, use **one pose per RunCommand invocation** (Rebind → step to ONE target time → single Render → write). Confirmed: the spawn+single-Update+single-shot path renders the correct pose every time; the 5-shots-in-a-loop path rendered 5 identical frames twice in a row. Alternative = play mode with timeScale near 0. Bone-height telemetry (hipsY/headY) is still reliable per-step and is the cheap proof the retarget arc is real even when frames lie. See [[feedback_measure_rootmotion_by_stepping]].
