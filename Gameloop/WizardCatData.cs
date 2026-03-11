using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    public class WizardCatData
    {
        public string MemberID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public int HP { get; set; }
        public int Power { get; set; }
        public int Tenacity { get; set; }
        public int Agility { get; set; }

        public string Move1 { get; set; }
        public string Move2 { get; set; }
        public string Move3 { get; set; }
        public string Move4 { get; set; }

        public string ActiveSpell { get; set; }
    }
}