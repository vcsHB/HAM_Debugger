using UnityEngine;

namespace HAM_DeBugger.Core.Attributes
{

    public class ShowIfAttribute : PropertyAttribute
    {
        public string ConditionFieldName;
        public bool Invert;

        public ShowIfAttribute(string conditionFieldName, bool invert = false)
        {
            ConditionFieldName = conditionFieldName;
            Invert = invert;
        }
    }
}