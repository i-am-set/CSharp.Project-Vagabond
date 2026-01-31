using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Utils
{
    public class TagContainer
    {
        private readonly HashSet<string> _tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void Add(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag))
                _tags.Add(tag);
        }

        public void Remove(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag))
                _tags.Remove(tag);
        }

        public bool Has(string tag)
        {
            return _tags.Contains(tag);
        }

        public bool HasAny(IEnumerable<string> tags)
        {
            return tags.Any(t => _tags.Contains(t));
        }
    }
}