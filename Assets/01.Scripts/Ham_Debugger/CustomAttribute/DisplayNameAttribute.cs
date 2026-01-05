namespace HAM_DeBugger.Core.Attributes
{
    public class DisplayNameAttribute : System.Attribute
    {
        public string Name { get; }
        public DisplayNameAttribute(string name) => Name = name;
    }

}