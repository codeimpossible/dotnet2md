using System.Xml.Linq;

namespace DotnetToMd
{
    internal partial class Parser
    {
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
