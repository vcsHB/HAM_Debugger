using Unity.VisualScripting;
using UnityEngine;
namespace HAM_DeBugger.CombatSystem
{
    public enum DamageType
    {
        Melee,
        Element,
        Blood
    }

    [System.Serializable]
    public struct DamageData
    {
        public float damage;
        public DamageType damageType;
        public bool isCritical;
        public Transform damageOrigin;
        public Vector2 damageDirection; // Normalized

        public static DamageData New(float damage)
        {
            return new DamageData()
            {
                damage = damage
            };
        }

    }

    public struct DamageResponse
    {
        public bool isHit;
        public float reflectionDamage;

        public static DamageResponse New(bool isHit)
        {
            return new DamageResponse()
            {
                isHit = isHit
            };
        }

    }

    public interface IDamageable
    {
        public DamageResponse ApplyDamage(DamageData damage);
    }
}