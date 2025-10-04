namespace DotnetToMd
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class ConfigurationOptionArgAttribute : Attribute
    {
        public string Name { get; set; } = string.Empty;
    }
}
