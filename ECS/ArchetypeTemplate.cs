using System.Collections.Generic;

namespace ProjectVagabond
{
    public class ArchetypeTemplate
    {
        public string Id { get; }
        public string Name { get; }
        public List<IComponent> TemplateComponents { get; }

        public ArchetypeTemplate(string id, string name, List<IComponent> templateComponents)
        {
            Id = id;
            Name = name;
            TemplateComponents = templateComponents;
        }
    }
}