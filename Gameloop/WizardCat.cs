using ProjectVagabond.Battle;
using System.Collections.Generic;

namespace ProjectVagabond
{
    public class WizardCat
    {
        public string Name { get; set; }
        public int MaxHP => Tenacity * 2;
        public int CurrentHP { get; set; }
        public int Strength { get; set; }
        public int Intelligence { get; set; }
        public int Tenacity { get; set; }
        public int Agility { get; set; }

        public int PortraitIndex { get; set; } = 0;

        public string SparName { get; set; }
        public int SparBasePower { get; set; }
        public string SparEffectType { get; set; }

        public WizardCat() { }

        public WizardCat Clone()
        {
            var clone = (WizardCat)this.MemberwiseClone();

            return clone;
        }
    }
}