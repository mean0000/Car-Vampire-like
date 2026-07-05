using UnityEngine;

/// <summary>
/// ★VFX 스폰 단일 경로(2026-07-05 교통정리) — 그동안 세 벌로 중복 구현되던 스폰 로직
/// (KatanaWeapon.SpawnSkillVfx / PlayerAttackVfx.OnAttack / ChargePhantomEmitter.SpawnSlashGo)을 합친다.
/// 규약(전부 기존 동작 보존): ①임베디드 사운드 차단(VFX는 시각 전용 — 손맛 사운드는 무기/SfxData 단일 소유)
/// ②PlayOnAwake=false 프리팹도 강제 Play ③scale/playbackSpeed 0=1 폴백(미설정 직렬화값 함정 가드)
/// ④lifetime 후 자동 소멸(0=1.5 폴백).
/// </summary>
public static class WeaponVfxSpawner
{
    /// <summary>원시 스폰 — pos/rot을 호출자가 이미 계산한 경우(콤보 슬래시·팬텀 슬래시).</summary>
    public static GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot,
                                   float scale, float playbackSpeed, float lifetime, Transform parent = null)
    {
        if (prefab == null) return null;
        var go = Object.Instantiate(prefab, pos, rot);
        StripEmbeddedAudio(go);
        if (parent != null) go.transform.SetParent(parent, true);
        float s = scale > 0f ? scale : 1f;
        if (!Mathf.Approximately(s, 1f)) go.transform.localScale *= s;
        float spd = playbackSpeed > 0f ? playbackSpeed : 1f;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
        {
            if (!Mathf.Approximately(spd, 1f)) { var main = ps.main; main.simulationSpeed *= spd; }
            ps.Play(false);
        }
        Object.Destroy(go, lifetime > 0f ? lifetime : 1.5f);
        return go;
    }

    /// <summary>basis 해석 포함 스폰 — <see cref="WeaponVfxData"/>(액션 SO/콤보 단) 경로.
    /// Weapon: 무기(칼) 앵커 기준(슬래시 — 휘두름 따라) / Player: 소유자 위치+조준 기준(불렛/전방 발사, z=앞).
    /// Weapon 기준인데 앵커가 없으면 Player 기준 폴백(구 KatanaWeapon.SpawnSkillVfx와 동일).</summary>
    public static GameObject Spawn(WeaponVfxData v, Transform owner, Transform weaponAnchor, Vector3 aimDir)
    {
        if (v == null || v.prefab == null) return null;
        Vector3 pos; Quaternion rot;
        if (v.basis == VfxBasis.Weapon && weaponAnchor != null)
        {
            pos = weaponAnchor.TransformPoint(v.posOffset);
            rot = weaponAnchor.rotation * Quaternion.Euler(v.eulerOffset);
        }
        else
        {
            if (owner == null) return null;
            Vector3 fwd = aimDir.sqrMagnitude > 0.0001f ? aimDir.normalized : owner.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            Quaternion aimRot = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            pos = owner.position + aimRot * v.posOffset;
            rot = aimRot * Quaternion.Euler(v.eulerOffset);
        }
        return Spawn(v.prefab, pos, rot, v.scale, v.playbackSpeed, v.lifetime,
                     v.parentToWeapon ? weaponAnchor : null);
    }

    /// <summary>스폰된 VFX 인스턴스의 임베디드 AudioSource 즉시 차단 — VFX 프리팹(예: Vefects)은 playOnAwake
    /// 사운드를 달고 오는데, 손맛 사운드는 무기(SfxData)가 단일 소유한다(2D·통제 가능). Instantiate 직후
    /// Stop()으로 첫 프레임 발성 차단. (구 PlayerAttackVfx.StripEmbeddedAudio 이동 — 원본 위치는 폐기.)</summary>
    public static void StripEmbeddedAudio(GameObject go)
    {
        foreach (var src in go.GetComponentsInChildren<AudioSource>(true))
        {
            src.Stop();
            src.playOnAwake = false;
            src.enabled = false;
        }
    }
}
