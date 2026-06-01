using UnityEngine;   // ★ 필수 — ScriptableObject/CreateAssetMenu/Header

public enum ZombieType { General, Signal }

[CreateAssetMenu(menuName = "ZombieCrush/ZombieConfig")]
public class ZombieConfig : ScriptableObject
{
    [Header("Identity")]
    public ZombieType zombieType;

    [Header("Movement")]
    public float moveSpeed = 3f;
    public float acceleration = 6f;

    [Header("Health")]
    public int maxHP = 3;

    [Header("Detection - Sight")]
    public float sightRange = 12f;
    public float sightHalfAngle = 60f;

    [Header("Detection - Hearing")]
    public float hearingMultiplier = 1f;

    [Header("Combat")]
    public float attackRange = 1.5f;
    public float attackCooldown = 1.5f;
    public float attackDamage = 20f;   // ★ infection-noise §5.4 확정: 좀비 타격 20 (플레이어 100HP=5대 사망)

    [Header("Investigation")]
    public float investigateLookTime = 3f;
    public float investigateTimeout = 8f;

    [Header("Signal")]
    public bool isSignalZombie;
    public float signalRadius = 15f;
    public float signalDelay = 3f;
    public int signalSummonCount = 4;
    public float signalCooldown = 15f;   // ★ 재소환 쿨다운 (하드코딩 제거)

    [Header("XP Drop")]
    public int xpOrbCountMin = 3;
    public int xpOrbCountMax = 5;
}
