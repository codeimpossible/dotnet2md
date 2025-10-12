using System.Text;
using System.Text.RegularExpressions;

namespace DotnetToMd
{
    internal partial class Parser
    {
        /// <summary>
        /// This will format a paragraph based on the contents of the <example /> node.
        /// </summary>
        private string FormatExample(string? text = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            text = FormatCodeBlock(text);
            if (text is null || string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }
            text = $"### Code example\n{text}";
            return text;
        }

        private string? FormatCodeBlock(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("<code>", "\n```csharp\n").Replace("</code>", "\n```\n");
        }
    }
}
