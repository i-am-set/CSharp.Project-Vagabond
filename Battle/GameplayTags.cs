namespace ProjectVagabond.Battle
{
    public static class GameplayTags
    {
        public static class States
        {
            public const string Stunned = "State.Stunned";
            public const string Dazed = "State.Dazed";
            public const string Silenced = "State.Silenced";
            public const string Protected = "State.Protected";
        }

        public static class Effects
        {
            public const string Recoil = "Effect.Recoil";
            public const string ManaDump = "Effect.ManaDump";
            public const string ManaMod = "Effect.ManaMod";
            public const string FixedDamage = "Effect.FixedDamage";
            public const string Damage = "Effect.Damage";
            public const string Mana = "Effect.Mana";
            public const string MultiHit = "Effect.MultiHit";
        }

        public static class Properties
        {
            public const string Contact = "Prop.Contact";
            public const string ProperNoun = "Prop.ProperNoun";
        }

        public static class Targets
        {
            public const string Self = "Target.Self";
        }

        public static class Types
        {
            public const string Player = "Type.Player";
            public const string Enemy = "Type.Enemy";
            public const string Ally = "Type.Ally";
        }

        public static class Genders
        {
            public const string Male = "Gender.Male";
            public const string Female = "Gender.Female";
            public const string Thing = "Gender.Thing";
            public const string Neutral = "Gender.Neutral";
        }

        public static class Rules
        {
            public const string IgnoreDefense = "Rules.IgnoreDefense";
            public const string IgnoreEvasion = "Rules.IgnoreEvasion";
            public const string TrueHit = "Rules.TrueHit";
            public const string CriticalGuaranteed = "Rules.CriticalGuaranteed";
        }
    }
}