namespace DotnetToMd
{
    public class ConfigurationOptions
    {
        internal const string ReferenceLinksHtmlFiles = "HTML";
        internal const string ReferenceLinksDirectories = "Directories";

        [ConfigurationOptionArg(Name = "-s")]
        [ConfigurationOptionArg(Name = "--source")] public string SourcePath { get; set; } = string.Empty;
        [ConfigurationOptionArg(Name = "-o")]
        [ConfigurationOptionArg(Name = "--output")] public string OutputPath { get; set; } = string.Empty;

        [ConfigurationOptionArg(Name = "-rls")]
        [ConfigurationOptionArg(Name = "--reference-link-style")] public string InternalReferenceLinkFormat { get; set; } = ReferenceLinksHtmlFiles;
    }
}
