using UnityEngine;
namespace HAM_DeBugger.AgentSystem
{

    public interface IAgentComponent 
    {
        public void Initialize(Agent agent);
        public void AfterInit();
        public void Dispose();
    }
}
