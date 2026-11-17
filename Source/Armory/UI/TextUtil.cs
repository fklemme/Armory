using System.Collections.Generic;
using System.Text;

namespace Armory
{
    /// <summary>
    /// Small helpers for labels that may contain Quality Colors rich-text tags.
    /// </summary>
    public static class TextUtil
    {
        private static readonly Dictionary<string, string> cache = new Dictionary<string, string>(256);

        /// <summary>
        /// Removes rich-text tags so a label can be measured and truncated safely.
        /// Results are cached because table cells are redrawn every frame.
        /// </summary>
        public static string StripTags(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0)
            {
                return text;
            }

            string cached;
            if (cache.TryGetValue(text, out cached))
            {
                return cached;
            }

            StringBuilder builder = new StringBuilder(text.Length);
            bool inTag = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<')
                {
                    inTag = true;
                }
                else if (c == '>')
                {
                    inTag = false;
                }
                else if (!inTag)
                {
                    builder.Append(c);
                }
            }

            string result = builder.ToString();
            if (cache.Count > 2048)
            {
                cache.Clear();
            }
            cache[text] = result;
            return result;
        }
    }
}
