using System;
using UnityEngine;
using UnityEngine.Events;
namespace HAM_DeBugger.CombatSystem
{

    public class HealthBody : MonoBehaviour, IDamageable
    {
        public UnityEvent OnDieEvent;
        public event Action<float, float> OnHealthIncreaseEvent;
        public event Action<float, float> OnHealthDecreaseEvent;
        public event Action<float, float> OnHealthChangeEvent;
        [SerializeField] private float _currentHealth;
        [SerializeField] private float _maxHealth;
        private bool _isDead;
        public bool IsDead => _isDead;


        public void Initialize()
        {
            
        }

        public DamageResponse ApplyDamage(DamageData damageData)
        {
            if (_isDead) return DamageResponse.New(false);
            _currentHealth -= damageData.damage;

            CheckDie();
            return DamageResponse.New(true);
        }

        private void CheckDie()
        {
            if (_currentHealth > 0)
                return;

            _isDead = true;
            OnDieEvent?.Invoke();

        }
    }
}