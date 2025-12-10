using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Utils
{
    public static class AbilityTester
    {
        private static int _passed = 0;
        private static int _failed = 0;
        private static int _skipped = 0;

        public static void RunAllTests()
        {
            _passed = 0;
            _failed = 0;
            _skipped = 0;

            LogHeader("=== STARTING ABILITY LOGIC TESTS ===");

            // --- MOCK BATTLE ENVIRONMENT SETUP ---
            var mockPlayer = CreateDummy(100, 100);
            mockPlayer.IsPlayerControlled = true;
            var mockEnemy = CreateDummy(100, 100);
            mockEnemy.IsPlayerControlled = false;

            // Register a temporary BattleManager
            var mockBattleManager = new BattleManager(new List<BattleCombatant> { mockPlayer }, new List<BattleCombatant> { mockEnemy });
            ServiceLocator.Register(mockBattleManager);

            try
            {
                LogHeader("Testing Stat Modifiers...");
                TestStatModifiers(mockPlayer, mockEnemy);

                LogHeader("Testing Damage Modifiers (Basic)...");
                TestDamageModifiers();

                LogHeader("Testing Damage Modifiers (Complex)...");
                TestComplexDamageModifiers();

                LogHeader("Testing Crit Modifiers...");
                TestCritModifiers();

                LogHeader("Testing Accuracy Modifiers...");
                TestAccuracyModifiers();

                LogHeader("Testing Action Modifiers...");
                TestActionModifiers();

                LogHeader("Testing Incoming Damage Modifiers...");
                TestIncomingDamageModifiers();

                LogHeader("Testing Trigger & Lifecycle Abilities...");
                TestTriggerAbilities();

                LogHeader("Testing Status Abilities...");
                TestStatusAbilities();

                LogHeader("Testing Battle Manager Dependent Abilities...");
                TestBattleDependentAbilities(mockPlayer, mockEnemy);
            }
            catch (Exception ex)
            {
                LogFail($"CRITICAL TEST SUITE FAILURE: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"TEST FAILURE: {ex}");
            }
            finally
            {
                // Cleanup: Remove the mock manager so it doesn't interfere with the real game
                ServiceLocator.Unregister<BattleManager>();
            }

            string resultColor = _failed == 0 ? "[palette_lightgreen]" : "[palette_red]";
            string msg = $"--- TESTS COMPLETE: {resultColor}{_passed} PASSED[/], [palette_red]{_failed} FAILED[/], [palette_yellow]{_skipped} SKIPPED[/] ---";

            // Log to GameLogger directly to ensure tags are parsed by DebugConsole
            GameLogger.Log(LogSeverity.Info, msg);

            // Also publish to EventBus for the in-game terminal (if active)
            EventBus.Publish(new GameEvents.TerminalMessagePublished
            {
                Message = msg,
                BaseColor = _failed == 0 ? Color.Lime : Color.Red
            });
        }

        private static void TestStatModifiers(BattleCombatant player, BattleCombatant enemy)
        {
            // 1. FlatStatBonusAbility
            var mods = new Dictionary<string, int> { { "Strength", 10 } };
            var ability = new FlatStatBonusAbility(mods);
            int result = ability.ModifyStat(OffensiveStatType.Strength, 10, new BattleCombatant());
            Assert(result == 20, "FlatStatBonus (Strength +10)");

            // 2. CorneredAnimalAbility (HP Threshold)
            var ca = new CorneredAnimalAbility(33f, 99, 50f); // 33% HP, Impossible Enemy Count, +50% Agi

            player.Stats.CurrentHP = 10; player.Stats.MaxHP = 100; // Low HP
            int lowResult = ca.ModifyStat(OffensiveStatType.Agility, 10, player);

            player.Stats.CurrentHP = 100; // High HP
            int highResult = ca.ModifyStat(OffensiveStatType.Agility, 10, player);

            Assert(lowResult == 15, "CorneredAnimal (Low HP Trigger)");
            Assert(highResult == 10, "CorneredAnimal (High HP Ignore)");
        }

        private static void TestDamageModifiers()
        {
            // 1. LowHPDamageBonusAbility
            var adrenaline = new LowHPDamageBonusAbility(50f, 100f); // <50% HP = +100% Dmg (2x)
            var lowHpCtx = new CombatContext { Actor = CreateDummy(10, 100) };
            var highHpCtx = new CombatContext { Actor = CreateDummy(100, 100) };

            Assert(Math.Abs(adrenaline.ModifyOutgoingDamage(100f, lowHpCtx) - 200f) < 0.1f, "LowHPDamage (Active)");
            Assert(Math.Abs(adrenaline.ModifyOutgoingDamage(100f, highHpCtx) - 100f) < 0.1f, "LowHPDamage (Inactive)");

            // 2. ElementalDamageBonusAbility
            var pyro = new ElementalDamageBonusAbility(2, 50f); // Fire (ID 2) +50%
            var fireMove = new MoveData { OffensiveElementIDs = new List<int> { 2 } };
            var waterMove = new MoveData { OffensiveElementIDs = new List<int> { 3 } };

            var fireCtx = new CombatContext { Move = fireMove };
            var waterCtx = new CombatContext { Move = waterMove };

            Assert(Math.Abs(pyro.ModifyOutgoingDamage(100f, fireCtx) - 150f) < 0.1f, "ElementalBonus (Matching)");
            Assert(Math.Abs(pyro.ModifyOutgoingDamage(100f, waterCtx) - 100f) < 0.1f, "ElementalBonus (Non-Matching)");

            // 3. FirstAttackDamageAbility
            var firstBlood = new FirstAttackDamageAbility(50f); // +50%
            var freshActor = new CombatContext { Actor = new BattleCombatant { HasUsedFirstAttack = false } };
            var tiredActor = new CombatContext { Actor = new BattleCombatant { HasUsedFirstAttack = true } };

            Assert(Math.Abs(firstBlood.ModifyOutgoingDamage(100f, freshActor) - 150f) < 0.1f, "FirstAttack (Active)");
            Assert(Math.Abs(firstBlood.ModifyOutgoingDamage(100f, tiredActor) - 100f) < 0.1f, "FirstAttack (Used)");

            // 4. GlassCannonAbility
            var glass = new GlassCannonAbility(50f, 20f); // +50% Out, +20% In
            var ctx = new CombatContext();
            Assert(Math.Abs(glass.ModifyOutgoingDamage(100f, ctx) - 150f) < 0.1f, "GlassCannon (Outgoing)");
            Assert(Math.Abs(glass.ModifyIncomingDamage(100f, ctx) - 120f) < 0.1f, "GlassCannon (Incoming)");

            // 5. RecklessAbandonAbility
            var reckless = new RecklessAbandonAbility(50f, -10); // +50% Dmg, -10 Acc
            var contactMove = new MoveData { MakesContact = true };
            var rangedMove = new MoveData { MakesContact = false };
            var contactCtx = new CombatContext { Move = contactMove };
            var rangedCtx = new CombatContext { Move = rangedMove };

            Assert(Math.Abs(reckless.ModifyOutgoingDamage(100f, contactCtx) - 150f) < 0.1f, "RecklessAbandon (Contact Dmg)");
            Assert(Math.Abs(reckless.ModifyOutgoingDamage(100f, rangedCtx) - 100f) < 0.1f, "RecklessAbandon (Ranged Dmg)");
            Assert(reckless.ModifyAccuracy(100, contactCtx) == 90, "RecklessAbandon (Contact Acc)");
            Assert(reckless.ModifyAccuracy(100, rangedCtx) == 100, "RecklessAbandon (Ranged Acc)");

            // 6. BloodletterAbility
            var bloodletter = new BloodletterAbility(50f); // +50% Void Dmg
            var voidMove = new MoveData { OffensiveElementIDs = new List<int> { 9 } };
            var voidCtx = new CombatContext { Move = voidMove };
            Assert(Math.Abs(bloodletter.ModifyOutgoingDamage(100f, voidCtx) - 150f) < 0.1f, "Bloodletter (Void Dmg)");

            // 7. ChainReactionAbility
            var chain = new ChainReactionAbility(5f); // +5% MultiHit
            var multiMove = new MoveData { Effects = new Dictionary<string, string> { { "MultiHit", "2,5" } } };
            var singleMove = new MoveData();
            var multiCtx = new CombatContext { Move = multiMove };
            var singleCtx = new CombatContext { Move = singleMove };
            Assert(Math.Abs(chain.ModifyOutgoingDamage(100f, multiCtx) - 105f) < 0.1f, "ChainReaction (MultiHit)");
            Assert(Math.Abs(chain.ModifyOutgoingDamage(100f, singleCtx) - 100f) < 0.1f, "ChainReaction (SingleHit)");
        }

        private static void TestComplexDamageModifiers()
        {
            // 1. StatusedTargetDamageAbility (Vulture's Pendant)
            var opportunist = new StatusedTargetDamageAbility(100f); // +100% (2x)
            var cleanTarget = CreateDummy(100, 100);
            var dirtyTarget = CreateDummy(100, 100);
            dirtyTarget.ActiveStatusEffects.Add(new StatusEffectInstance(StatusEffectType.Poison, 3));

            var ctxClean = new CombatContext { Target = cleanTarget };
            var ctxDirty = new CombatContext { Target = dirtyTarget };

            Assert(Math.Abs(opportunist.ModifyOutgoingDamage(100f, ctxClean) - 100f) < 0.1f, "Opportunist (No Status)");
            Assert(Math.Abs(opportunist.ModifyOutgoingDamage(100f, ctxDirty) - 200f) < 0.1f, "Opportunist (Has Status)");

            // 2. LastStandAbility (Anchor Charm)
            var anchor = new LastStandAbility(30f); // +30%
            var ctxFirst = new CombatContext { IsLastAction = false };
            var ctxLast = new CombatContext { IsLastAction = true };

            Assert(Math.Abs(anchor.ModifyOutgoingDamage(100f, ctxFirst) - 100f) < 0.1f, "LastStand (First Action)");
            Assert(Math.Abs(anchor.ModifyOutgoingDamage(100f, ctxLast) - 130f) < 0.1f, "LastStand (Last Action)");

            // 3. FullHPDamageAbility (Pristine Gem)
            var pristine = new FullHPDamageAbility(50f); // +50%
            var fullActor = CreateDummy(100, 100);
            var hurtActor = CreateDummy(99, 100);
            var ctxFull = new CombatContext { Actor = fullActor };
            var ctxHurt = new CombatContext { Actor = hurtActor };

            Assert(Math.Abs(pristine.ModifyOutgoingDamage(100f, ctxFull) - 150f) < 0.1f, "FullPower (Full HP)");
            Assert(Math.Abs(pristine.ModifyOutgoingDamage(100f, ctxHurt) - 100f) < 0.1f, "FullPower (Hurt)");
        }

        private static void TestIncomingDamageModifiers()
        {
            // 1. VigorAbility (Oaken Heart)
            var vigor = new VigorAbility(75f, 50f); // >75% HP -> -50% Dmg
            var healthyActor = CreateDummy(80, 100); // 80%
            var weakActor = CreateDummy(70, 100); // 70%

            var ctxHealthy = new CombatContext { Actor = healthyActor };
            var ctxWeak = new CombatContext { Actor = weakActor };

            Assert(Math.Abs(vigor.ModifyIncomingDamage(100f, ctxHealthy) - 50f) < 0.1f, "Vigor (High HP)");
            Assert(Math.Abs(vigor.ModifyIncomingDamage(100f, ctxWeak) - 100f) < 0.1f, "Vigor (Low HP)");

            // 2. PhysicalDamageReductionAbility
            var thickSkin = new PhysicalDamageReductionAbility(20f); // -20% Phys
            var physMove = new MoveData { ImpactType = ImpactType.Physical };
            var magMove = new MoveData { ImpactType = ImpactType.Magical };
            var physCtx = new CombatContext { Move = physMove };
            var magCtx = new CombatContext { Move = magMove };

            Assert(Math.Abs(thickSkin.ModifyIncomingDamage(100f, physCtx) - 80f) < 0.1f, "ThickSkin (Physical)");
            Assert(Math.Abs(thickSkin.ModifyIncomingDamage(100f, magCtx) - 100f) < 0.1f, "ThickSkin (Magical)");

            // 3. SunBlessedLeafAbility
            var photo = new SunBlessedLeafAbility(10, 25f); // Light (10) -> 0 Dmg
            var lightMove = new MoveData { OffensiveElementIDs = new List<int> { 10 } };
            var darkMove = new MoveData { OffensiveElementIDs = new List<int> { 9 } };
            var actor = CreateDummy(100, 100);
            var lightCtx = new CombatContext { Move = lightMove, Actor = actor };
            var darkCtx = new CombatContext { Move = darkMove, Actor = actor };

            Assert(Math.Abs(photo.ModifyIncomingDamage(100f, lightCtx) - 0f) < 0.1f, "Photosynthesis (Light)");
            Assert(Math.Abs(photo.ModifyIncomingDamage(100f, darkCtx) - 100f) < 0.1f, "Photosynthesis (Dark)");

            // 4. ElementalImmunityAbility (Rubber Soles)
            var rubber = new ElementalImmunityAbility(11); // Electric (11)
            var elecMove = new MoveData { OffensiveElementIDs = new List<int> { 11 } };
            var elecCtx = new CombatContext { Move = elecMove };
            Assert(Math.Abs(rubber.ModifyIncomingDamage(100f, elecCtx) - 0f) < 0.1f, "RubberSoles (Electric)");

            // 5. GhostlySlippersAbility (Dodge Miss)
            var slippers = new GhostlySlippersAbility(100); // 100% chance to miss if dodging
            var dodgingActor = CreateDummy(100, 100);
            dodgingActor.ActiveStatusEffects.Add(new StatusEffectInstance(StatusEffectType.Dodging, 1));
            var normalActor = CreateDummy(100, 100);
            var dodgeCtx = new CombatContext { Target = dodgingActor };
            var normalCtx = new CombatContext { Target = normalActor };

            Assert(Math.Abs(slippers.ModifyIncomingDamage(100f, dodgeCtx) - 0f) < 0.1f, "GhostlySlippers (Dodging)");
            Assert(Math.Abs(slippers.ModifyIncomingDamage(100f, normalCtx) - 100f) < 0.1f, "GhostlySlippers (Normal)");
        }

        private static void TestTriggerAbilities()
        {
            // 1. SanguineThirstAbility (Heal on Hit)
            var thirst = new SanguineThirstAbility(25f); // 25% Lifesteal
            var actor = CreateDummy(50, 100); // 50/100 HP
            var contactMove = new MoveData { MakesContact = true };
            var ctx = new CombatContext { Actor = actor, Move = contactMove };

            thirst.OnHit(ctx, 40); // Dealt 40 dmg -> Heal 10
            Assert(actor.Stats.CurrentHP == 60, "SanguineThirst (Heal)");

            // 2. ThornsAbility (Damage Attacker)
            var thorns = new ThornsAbility(10f); // 10% Max HP Dmg
            var defender = CreateDummy(100, 100); // Has thorns
            var attacker = CreateDummy(100, 100); // Attacks
            var thornsCtx = new CombatContext { Target = attacker, Move = contactMove }; // Target in context is the one taking damage (Attacker)

            thorns.OnDamaged(thornsCtx, 10);
            Assert(attacker.Stats.CurrentHP == 90, "Thorns (Damage Attacker)");

            // 3. SpellweaverAbility (Action -> Spell Boost)
            var weaver = new SpellweaverAbility(50f); // +50%
            var weaverActor = CreateDummy(100, 100);
            var actionMove = new MoveData { MoveType = MoveType.Action };
            var spellMove = new MoveData { MoveType = MoveType.Spell };
            var actionQ = new QueuedAction { ChosenMove = actionMove };
            var ctxWeaver = new CombatContext { Move = spellMove };

            weaver.OnActionComplete(actionQ, weaverActor); // Trigger
            float dmg = weaver.ModifyOutgoingDamage(100f, ctxWeaver); // Check
            Assert(Math.Abs(dmg - 150f) < 0.1f, "Spellweaver (Active)");

            // 4. MomentumAbility (Kill -> Boost)
            var momentum = new MomentumAbility(50f);
            var momActor = CreateDummy(100, 100);
            var killMove = new MoveData { Power = 50 }; // Must have power > 0
            var ctxMom = new CombatContext { Actor = momActor, Move = killMove };

            momentum.OnKill(ctxMom); // Trigger
            float momDmg = momentum.ModifyOutgoingDamage(100f, ctxMom);
            Assert(Math.Abs(momDmg - 150f) < 0.1f, "Momentum (Active)");

            // 5. EscalationAbility (Turn End -> Boost)
            var escalation = new EscalationAbility(10f, 100f); // +10% per turn
            var escActor = CreateDummy(100, 100);
            var ctxEsc = new CombatContext();

            escalation.OnTurnEnd(escActor); // 1 stack
            escalation.OnTurnEnd(escActor); // 2 stacks (+20%)
            float escDmg = escalation.ModifyOutgoingDamage(100f, ctxEsc);
            Assert(Math.Abs(escDmg - 120f) < 0.1f, "Escalation (2 Turns)");

            // 6. RegenAbility (Turn End -> Heal)
            var regen = new RegenAbility(10f); // 10%
            var regenActor = CreateDummy(50, 100);
            regen.OnTurnEnd(regenActor);
            Assert(regenActor.Stats.CurrentHP == 60, "Regen (Turn End)");

            // 7. PainFuelAbility (Crit Received -> Stats)
            var pain = new PainFuelAbility();
            var painActor = CreateDummy(100, 100);
            var painCtx = new CombatContext { Target = painActor };
            pain.OnCritReceived(painCtx);
            Assert(painActor.StatStages[OffensiveStatType.Strength] == 2, "PainFuel (Strength Up)");
            Assert(painActor.StatStages[OffensiveStatType.Intelligence] == 2, "PainFuel (Int Up)");

            // 8. SadistAbility (Status Applied -> Stats)
            var sadist = new SadistAbility();
            var sadistActor = CreateDummy(100, 100);
            var sadistCtx = new CombatContext { Actor = sadistActor };
            sadist.OnStatusApplied(sadistCtx, new StatusEffectInstance(StatusEffectType.Poison, 3));
            Assert(sadistActor.StatStages[OffensiveStatType.Strength] == 1, "Sadist (Strength Up)");

            // 9. CausticBloodAbility (Lifesteal -> Damage)
            var caustic = new CausticBloodAbility();
            var leechActor = CreateDummy(100, 100); // Trying to heal
            var causticOwner = CreateDummy(100, 100); // Has the ability
            bool handled = caustic.OnLifestealReceived(leechActor, 20, causticOwner);

            Assert(handled == true, "CausticBlood (Intercepted)");
            Assert(leechActor.Stats.CurrentHP == 80, "CausticBlood (Damaged Attacker)");

            // 10. ReactiveStatusAbility (Voltaic Mantle)
            var reactive = new ReactiveStatusAbility(StatusEffectType.Stun, 100, 1, true); // 100% Stun on contact
            var stunAttacker = CreateDummy(100, 100);
            var stunDefender = CreateDummy(100, 100);
            var stunCtx = new CombatContext { Actor = stunDefender, Target = stunAttacker, Move = contactMove };

            reactive.OnDamaged(stunCtx, 10);
            Assert(stunAttacker.HasStatusEffect(StatusEffectType.Stun), "ReactiveStatus (Stun Applied)");

            // 11. DefensePenetrationAbility (Scorpion Stinger)
            var pen = new DefensePenetrationAbility(25f); // 25% Pen
            var penCtx = new CombatContext();
            Assert(Math.Abs(pen.GetDefensePenetration(penCtx) - 0.25f) < 0.01f, "DefensePenetration");

            // 12. ApplyStatusOnHitAbility (Venomous)
            var venom = new ApplyStatusOnHitAbility(StatusEffectType.Poison, 100, 3, true); // 100% Poison on contact
            var venomActor = CreateDummy(100, 100);
            var venomTarget = CreateDummy(100, 100);
            var venomCtx = new CombatContext { Actor = venomActor, Target = venomTarget, Move = contactMove };

            venom.OnHit(venomCtx, 10);
            Assert(venomTarget.HasStatusEffect(StatusEffectType.Poison), "ApplyStatusOnHit (Poison)");
        }

        private static void TestStatusAbilities()
        {
            // 1. StatusImmunityAbility (Sealed Envelope)
            var immune = new StatusImmunityAbility(new[] { StatusEffectType.Stun });
            var actor = CreateDummy(100, 100);
            actor.RegisterAbility(immune);

            bool blocked = actor.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Stun, 1));
            bool allowed = actor.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Poison, 1));

            Assert(!actor.HasStatusEffect(StatusEffectType.Stun), "StatusImmunity (Blocked Stun)");
            Assert(actor.HasStatusEffect(StatusEffectType.Poison), "StatusImmunity (Poison Applied)");

            // 2. StatusDurationAbility (Cursed Hourglass)
            var duration = new StatusDurationAbility(1); // +1 turn
            var durActor = CreateDummy(100, 100);
            durActor.RegisterAbility(duration);

            int baseDur = 3;
            int modDur = baseDur;
            foreach (var mod in durActor.OutgoingStatusModifiers)
            {
                modDur = mod.ModifyStatusDuration(StatusEffectType.Poison, modDur, durActor);
            }
            Assert(modDur == 4, "StatusDuration (+1 Turn)");
        }

        private static void TestBattleDependentAbilities(BattleCombatant player, BattleCombatant enemy)
        {
            // 1. ToxicAuraAbility (Idol of Disease)
            var aura = new ToxicAuraAbility(100, 3); // 100% chance
            aura.OnTurnEnd(player); // Should poison the enemy
            Assert(enemy.HasStatusEffect(StatusEffectType.Poison), "ToxicAura (Poisoned Enemy)");

            // 2. IntimidateAbility (Gorgon's Gaze)
            var intimidate = new IntimidateAbility(OffensiveStatType.Strength, -1);
            intimidate.OnCombatantEnter(player); // Should lower enemy strength
            Assert(enemy.StatStages[OffensiveStatType.Strength] == -1, "Intimidate (Lowered Strength)");

            // 3. ContagionAbility (Miasma Vial)
            var contagion = new ContagionAbility(100, 1);
            // Give player a status to spread
            player.AddStatusEffect(new StatusEffectInstance(StatusEffectType.Burn, 3));
            var ctx = new CombatContext { Actor = player, Target = player }; // Self-inflicted or just context
            // Contagion triggers when a status is applied.
            // Let's manually trigger it.
            contagion.OnStatusApplied(ctx, new StatusEffectInstance(StatusEffectType.Burn, 3));
            Assert(enemy.HasStatusEffect(StatusEffectType.Burn), "Contagion (Spread Burn)");
        }

        private static void TestCritModifiers()
        {
            var sniper = new FlatCritBonusAbility(10f); // +10%
            var ctx = new CombatContext();
            Assert(Math.Abs(sniper.ModifyCritChance(0.05f, ctx) - 0.15f) < 0.001f, "FlatCritBonus");

            var bulwark = new CritDamageReductionAbility(50f); // -50% Crit Dmg taken
            Assert(Math.Abs(bulwark.ModifyCritDamage(2.0f, ctx) - 1.0f) < 0.01f, "CritDamageReduction");
        }

        private static void TestAccuracyModifiers()
        {
            var keenEye = new IgnoreEvasionAbility();
            var ctx = new CombatContext();
            Assert(keenEye.ShouldIgnoreEvasion(ctx) == true, "IgnoreEvasionAbility");
        }

        private static void TestActionModifiers()
        {
            var ambush = new AmbushPredatorAbility(1, -20f); // +1 Prio, -20% Power
            var actor = new BattleCombatant { HasUsedFirstAttack = false };
            var move = new MoveData { Power = 100, Priority = 0 };
            var action = new QueuedAction { ChosenMove = move };

            ambush.ModifyAction(action, actor);

            Assert(action.Priority == 1, "AmbushPredator (Priority)");
            Assert(action.ChosenMove.Power == 80, "AmbushPredator (Power Penalty)");

            // Test Used State
            actor.HasUsedFirstAttack = true;
            var move2 = new MoveData { Power = 100, Priority = 0 };
            var action2 = new QueuedAction { ChosenMove = move2 };
            ambush.ModifyAction(action2, actor);

            Assert(action2.Priority == 0, "AmbushPredator (Inactive)");
        }

        // --- Helpers ---

        private static BattleCombatant CreateDummy(int currentHp, int maxHp)
        {
            var c = new BattleCombatant
            {
                Name = "Dummy",
                Stats = new CombatantStats { CurrentHP = currentHp, MaxHP = maxHp },
                BattleSlot = 0
            };
            // IsActiveOnField is read-only and derived from BattleSlot.
            // Setting BattleSlot = 0 makes IsActiveOnField true.
            return c;
        }

        private static void Assert(bool condition, string testName)
        {
            if (condition)
            {
                _passed++;
                string msg = $"  [palette_lightgreen]PASS:[/] {testName}";
                GameLogger.Log(LogSeverity.Info, msg);
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = msg });
            }
            else
            {
                _failed++;
                string msg = $"  [palette_red]FAIL:[/] {testName}";
                GameLogger.Log(LogSeverity.Error, msg);
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = msg });
            }
        }

        private static void LogInfo(string message)
        {
            string tagged = $"[palette_teal]{message}[/]";
            GameLogger.Log(LogSeverity.Info, tagged);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = tagged });
        }

        private static void LogHeader(string message)
        {
            string tagged = $"[palette_teal]{message}[/]";
            GameLogger.Log(LogSeverity.Info, tagged);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = tagged });
        }

        private static void LogFail(string message)
        {
            string tagged = $"[palette_red]{message}[/]";
            GameLogger.Log(LogSeverity.Error, tagged);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = tagged });
        }

        private static void LogSkipped(string message)
        {
            _skipped++;
            string tagged = $"  [palette_yellow]SKIPPED:[/] {message}";
            GameLogger.Log(LogSeverity.Warning, tagged);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = tagged });
        }
    }
}