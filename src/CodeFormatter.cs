using System;
using System.Text;

namespace DotnetToMd
{
    internal static class CodeFormatter
    {
        private static char[] BreakCharacters = new char[] { ',' };
        private static char SPACE = ' ';

        public static string FormatMethodDefinition(string input, int maxLen = 80)
        {
            try
            {
                if (string.IsNullOrEmpty(input)) return string.Empty;
                if (input.Length <= maxLen)
                {
                    return input;
                }
                var indexOfFirstParen = input.IndexOf('(');
                var newString = new StringBuilder();
                var i = maxLen;
                // starting at the max line length, scan backwards for a desired break character
                while (input.Length > maxLen)
                {
                    if (!TryScanLeft(input, maxLen, out var breakCharacterIndex))
                    {
                        if (!TryScanRight(input, maxLen, out breakCharacterIndex))
                        {
                            break;
                        }
                    }
                    var snip = input.Substring(0, breakCharacterIndex + 1);
                    newString.AppendLine(snip);
                    input = GenerateSpaces(indexOfFirstParen + 1) + input.Substring(breakCharacterIndex + 1);
                }
                newString.AppendLine(input);
                return newString.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"");
                Console.WriteLine($"Exception during {nameof(FormatMethodDefinition)}. input={input}, maxLen={maxLen}");
                Console.WriteLine(ex.ToString());
                throw ex;
            }
        }

        private static bool TryScanLeft(string input, int maxLen, out int breakCharacterIndex)
        {
            breakCharacterIndex = -1;
            var i = maxLen;
            try
            {
                while (i >= 0)
                {
                    var peekChar = input[i];
                    if (BreakCharacters.Contains(peekChar))
                    {
                        // we've found a point where we can insert a linebreak
                        breakCharacterIndex = i;
                        return true;
                    }
                    i--;
                }
                return false;
            }
            catch
            {
                Console.WriteLine($"Exception during {nameof(TryScanLeft)}. input={input}, maxLen={maxLen}, breakCharacterIndex={breakCharacterIndex}, i={i}");
                throw;
            }
        }

        private static bool TryScanRight(string input, int maxLen, out int breakCharacterIndex)
        {
            breakCharacterIndex = -1;
            var i = maxLen;
            try
            {
                while (i < input.Length)
                {
                    var peekChar = input[i];
                    if (BreakCharacters.Contains(peekChar))
                    {
                        // we've found a point where we can insert a linebreak
                        breakCharacterIndex = i;
                        return true;
                    }
                    i++;
                }
                return false;
            }
            catch
            {
                Console.WriteLine($"Exception during {nameof(TryScanRight)}. input={input}, maxLen={maxLen}, breakCharacterIndex={breakCharacterIndex}, i={i}");
                throw;
            }
        }

        private static string GenerateSpaces(int num = 1)
        {
            var builder = new StringBuilder();
            while (num-- > 0)
            {
                builder.Append(SPACE);
            }
            return builder.ToString();
        }
    }
}
