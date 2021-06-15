using FreneticUtilities.FreneticExtensions;
using SharpDenizenTools.MetaObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpDenizenTools.MetaHandlers
{
    /// <summary>Helper to parse tags.</summary>
    public static class TagHelper
    {
        /// <summary>Parses the plaintext of a tag into something analyzable.</summary>
        public static SingleTag Parse(string tag, Action<string> trackErrors)
        {
            tag = tag.ToLowerFast();
            int brackets = 0;
            int firstBracket = 0;
            int start = 0;
            bool foundABracket = false;
            SingleTag output = new SingleTag();
            bool declaredMisformat = false;
            for (int i = 0; i < tag.Length; i++)
            {
                if (tag[i] == '[')
                {
                    brackets++;
                    if (brackets == 1)
                    {
                        output.Parts.Add(new SingleTag.Part() { Text = tag[start..i], StartChar = start });
                        foundABracket = true;
                        start = i;
                        firstBracket = i;
                    }
                }
                else if (tag[i] == ']')
                {
                    brackets--;
                    if (brackets == -1)
                    {
                        trackErrors("Invalid tag format, too many ']' symbols");
                    }
                    if (brackets == 0)
                    {
                        output.Parts[^1].Context = tag.Substring(firstBracket + 1, i - firstBracket - 1);
                    }
                }
                else if (tag[i] == '.' && brackets == 0)
                {
                    if (!foundABracket)
                    {
                        output.Parts.Add(new SingleTag.Part() { Text = tag[start..i], StartChar = start });
                    }
                    foundABracket = false;
                    start = i + 1;
                }
                else if (tag[i] == '|' && brackets == 0 && i + 1 < tag.Length && tag[i + 1] == '|')
                {
                    if (!foundABracket)
                    {
                        output.Parts.Add(new SingleTag.Part() { Text = tag[start..i], StartChar = start });
                    }
                    output.EndChar = i;
                    output.Fallback = tag[(i + 2)..];
                    return output;
                }
                else if (foundABracket && brackets == 0 && !declaredMisformat)
                {
                    declaredMisformat = true;
                    trackErrors("Invalid tag format, text after closing ']' symbol before '.' symbol");
                }
            }
            if (brackets != 0)
            {
                trackErrors("Invalid tag format, too many '[' symbols");
            }
            if (!foundABracket)
            {
                output.Parts.Add(new SingleTag.Part() { Text = tag[start..], StartChar = start });
            }
            output.EndChar = tag.Length;
            return output;
        }
    }

    /// <summary>Represents a single parsed tag.</summary>
    public class SingleTag
    {
        /// <summary>Represents a single part (between dots) of a tag.</summary>
        public class Part
        {
            /// <summary>The tag text (tag name).</summary>
            public string Text;

            /// <summary>The tag context (if any).</summary>
            public string Context;

            /// <summary>The index in the original string where this tag-part started.</summary>
            public int StartChar;
        }

        /// <summary>The parts of the tag.</summary>
        public List<Part> Parts = new List<Part>();

        /// <summary>The tag fallback text (if any).</summary>
        public string Fallback;

        /// <summary>Index of either where the tag ends, or a fallback starts.</summary>
        public int EndChar;
    }
}
