using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DotnetToMd
{
    internal partial class Parser
    {
        private string FormatLinks(string? text, string prefix)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            Regex seeSelfClosingRegex = new("(<see cref=\")(.*)(\"[ ]?/>)");
            Regex seeRegex = new("(<see cref=\")(.*)(\"[ ]?>(.*)</see>)");

            var matchCollection = seeSelfClosingRegex.Matches(text);
            foreach (Match match in matchCollection)
            {
                if (!match.Success)
                {
                    continue;
                }

                var replaceString = match.Value;
                var memberName = match.Groups[2].Value;

                text = text.Replace(replaceString, ToReferenceLink(memberName, prefix));
            }

            matchCollection = seeRegex.Matches(text);
            foreach (Match match in matchCollection)
            {
                if (!match.Success)
                {
                    continue;
                }

                var replaceString = match.Value;
                var memberName = match.Groups[2].Value;
                var overrideName = (match.Groups[3].Value ?? string.Empty).Replace("\">", string.Empty).Replace("</see>", string.Empty);

                text = text.Replace(replaceString, ToReferenceLink(memberName, prefix, overrideName));
            }

            return text;
        }

        private IEnumerable<(string Uri, string? Text)> FormatAdditionalLinks(XElement? el)
        {
            var results = new List<(string Uri, string? Text)>();
            if (el is null)
            {
                return results;
            }

            foreach (var seeNode in el.Descendants("see"))
            {
                var link = seeNode.Attribute("cref") ?? seeNode.Attribute("href");
                if (link is null)
                {
                    continue;
                }
                results.Add(new ValueTuple<string, string?>(link.Value, seeNode.Value));
            }

            foreach (var seeAlsoNode in el.Descendants("seealso"))
            {
                var link = seeAlsoNode.Attribute("cref") ?? seeAlsoNode.Attribute("href");
                if (link is null)
                {
                    continue;
                }
                results.Add(new ValueTuple<string, string?>(link.Value, seeAlsoNode.Value));
            }

            return results;
        }
    }
}
