using System.Collections.Generic;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;

/// <summary>
/// 근접 공격 로직. PlayerCombat이 소유·구동하는 순수 C# 헬퍼(런타임 AddComponent의
/// 수명주기·실행순서 함정을 피하려 MonoBehaviour로 만들지 않는다).
///
/// 설계(docs/00_authority/2026-06-03-demo-weapon-lineup.md + PM/Codex 자문 반영):
/// - 좌클릭 홀드 = 쿨마다 자동 스윙(원거리와 입력 통일). 이동은 절대 스윙을 막지 않는다.
/// - 스윙 즉시 전방 부채꼴 판정(이동 desync 없음 = 후한 판정) + 초근접 각도 무시(헛스윙 방지).
/// - 부채꼴 내 전원 클리브(군중제어). 같은 스윙 중복 타격은 HashSet으로 차단.
/// - 무기별 데미지/리치/부채꼴/넉백/죽음연출/사운드 차등. 방망이→쇠지렛대 진화(profile 스왑).
/// </summary>
public class MeleeAttacker
{
    readonly Transform _owner;
    readonly float _muzzleHeight;
    readonly LayerMask _zombieMask;
    readonly LayerMask _obstacleMask;
    readonly MMSpringScale _ownerSpring;   // 있으면 스윙 시 플레이어 반응(없으면 무시)

    WeaponLoadout.Weapon _profile;

    float _cooldownTimer;
    Vector3 _aimDir = Vector3.forward;

    // 스윙 호(arc) 비주얼 — 런타임 생성, 짧게 보였다 사라짐
    LineRenderer _arc;
    const int ArcSegments = 14;
    float _arcTimer;
    const float ArcShowTime = 0.13f;

    // 초근접은 각도와 무관하게 맞힌다(헛스윙 짜증 방지).
    const float PointBlankRadius = 0.9f;

    readonly HashSet<ZombieController> _hitThisSwing = new HashSet<ZombieController>();

    public MeleeAttacker(Transform owner, WeaponLoadout.Weapon profile, float muzzleHeight,
                         LayerMask zombieMask, LayerMask obstacleMask)
    {
        _owner = owner;
        _profile = profile;
        _muzzleHeight = muzzleHeight;
        _zombieMask = zombieMask;
        _obstacleMask = obstacleMask;
        _ownerSpring = owner.GetComponent<MMSpringScale>();

        _arc = CreateArc();
    }

    /// <summary>방망이 → 쇠지렛대 진화(데모: 디버그키/카드 트리거에서 호출).</summary>
    public void Evolve(WeaponLoadout.Weapon evolved) => _profile = evolved;

    public string WeaponName => _profile.name;

    /// <summary>PlayerCombat.Update에서 매 프레임 호출.</summary>
    public void Tick(bool attackHeld, Vector3 aimDir, bool locked)
    {
        _aimDir = aimDir;

        if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

        if (!locked && attackHeld && _cooldownTimer <= 0f)
            Swing();

        UpdateArcVisual();
    }

    void Swing()
    {
        // 속사/연사 카드(FireRateMult)로 스윙 쿨 단축 — 원거리와 동일 훅 공유.
        _cooldownTimer = _profile.fireCooldown / Mathf.Max(0.01f, PlayerStats.FireRateMult);

        Vector3 origin = _owner.position;
        Vector3 eye = origin + Vector3.up * _muzzleHeight;
        int dmg = _profile.damage + PlayerStats.DamageBonus;   // 강선 등 데미지 카드 공유

        _hitThisSwing.Clear();
        bool hitAny = false;
        bool killedAny = false;
        Vector3 firstHitPos = origin + _aimDir * _profile.range;

        // 리치 + 좀비 반경 여유만큼 후하게 수집. 좀비 콜라이더는 트리거라 Collide 필수.
        float gather = _profile.range + 0.5f;
        Collider[] cols = Physics.OverlapSphere(origin, gather, _zombieMask, QueryTriggerInteraction.Collide);
        foreach (var c in cols)
        {
            var z = c.GetComponentInParent<ZombieController>();
            if (z == null || _hitThisSwing.Contains(z)) continue;

            Vector3 to = z.transform.position - origin;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist > gather) continue;

            // 부채꼴 판정: 초근접은 각도 무시(후한 판정), 그 외는 반각 이내.
            if (dist > PointBlankRadius)
            {
                if (Vector3.Angle(_aimDir, to) > _profile.arcHalfAngle) continue;

                // 벽 너머 좀비 제외(총의 벽 막힘과 동일한 시야 체크).
                Vector3 dir = (z.transform.position + Vector3.up * _muzzleHeight) - eye;
                float d = dir.magnitude;
                if (d > 0.001f && Physics.Raycast(eye, dir / d, d, _obstacleMask, QueryTriggerInteraction.Ignore))
                    continue;
            }

            _hitThisSwing.Add(z);
            if (!hitAny) firstHitPos = z.transform.position;
            hitAny = true;

            bool killed = z.TakeMeleeHit(dmg, origin, _profile.knockback, _profile.stagger, _profile.deathStyle);
            killedAny |= killed;
        }

        // 스윙 소음(낮음 — 근접의 정체성). 맞히든 헛치든 휘두르는 소리.
        NoiseManager.Instance?.EmitImpulse(_profile.gunshotNoise);

        // 사운드: 맞으면 무기별 타격음(스윙당 1회), 헛치면 공기 가르는 소리.
        if (hitAny) MeleeSfx.PlayHit(_profile.deathStyle, firstHitPos);
        else MeleeSfx.PlayWhiff(eye);

        // 멈춤감: 스윙당 1회만(다중킬 시 DieByWeapon이 각자 내면 스택돼 슬로모 고착 → 여기서 소유).
        // 죽이면 더 묵직하게, 비살상 타격이면 짧게.
        float stop = killedAny ? _profile.hitstop * 1.5f : (hitAny ? _profile.hitstop : 0f);
        if (stop > 0f)
            MMTimeScaleEvent.Trigger(MMTimeScaleMethods.For, stop, 0.05f, false, 0f, false);

        // 플레이어 반응: 스윙 펀치(있으면).
        _ownerSpring?.Bump(new Vector3(-0.12f, 0.08f, -0.12f));

        ShowArc();
    }

    // ── 스윙 호 비주얼 ────────────────────────────────────────

    LineRenderer CreateArc()
    {
        var go = new GameObject("MeleeArc");
        go.transform.SetParent(_owner, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = ArcSegments + 1;
        lr.widthMultiplier = 0.1f;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        var sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        lr.material = new Material(sh);
        lr.enabled = false;
        return lr;
    }

    void ShowArc()
    {
        Vector3 center = _owner.position + Vector3.up * (_muzzleHeight * 0.5f);
        float half = _profile.arcHalfAngle;
        for (int i = 0; i <= ArcSegments; i++)
        {
            float a = Mathf.Lerp(-half, half, i / (float)ArcSegments);
            Vector3 dir = Quaternion.AngleAxis(a, Vector3.up) * _aimDir;
            _arc.SetPosition(i, center + dir * _profile.range);
        }
        _arcTimer = ArcShowTime;
        _arc.enabled = true;
    }

    void UpdateArcVisual()
    {
        if (!_arc.enabled) return;
        _arcTimer -= Time.deltaTime;
        float a = Mathf.Clamp01(_arcTimer / ArcShowTime);
        // 쇠지렛대는 더 차갑게, 방망이는 따뜻하게(무기 정체성 색).
        Color baseC = _profile.deathStyle == WeaponLoadout.DeathStyle.Crunch
            ? new Color(0.7f, 0.85f, 1f, 1f)
            : new Color(1f, 0.85f, 0.4f, 1f);
        baseC.a *= a;
        _arc.startColor = baseC;
        _arc.endColor = baseC;
        if (_arcTimer <= 0f) _arc.enabled = false;
    }

    /// <summary>PlayerCombat.OnDestroy에서 호출 — 런타임 생성물 정리.</summary>
    public void Cleanup()
    {
        if (_arc != null)
        {
            if (_arc.material != null) Object.Destroy(_arc.material);
            Object.Destroy(_arc.gameObject);
        }
    }
}
