using UnityEngine;
namespace HAM_DeBugger.CombatSystem
{

    public interface IHealable
    {
        public void Heal(float healAmount);
    }
}