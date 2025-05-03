namespace DotnetToMd.Metadata
{
    public abstract class InformationBase
    {
        public string? Name { get; protected set; }
        public string? Summary { get; set; }
        public string? Remarks { get; set; }
        public string? MinimumVersion { get; set; }
        public readonly List<(string Uri, string? Text)> AdditionalLinks = new();
    }
}
