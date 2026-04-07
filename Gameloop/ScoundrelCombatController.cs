using Microsoft.Xna.Framework;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class ScoundrelCombatController
    {
        public int Health { get; set; }
        public int LastSlainValue { get; set; }
        public int CardsResolvedThisRoom { get; set; }
        public int PotionsUsedThisRoom { get; set; }
        public bool CanSkip { get; set; }

        public float FloorTimer { get; set; }
        public int TotalCardsInFloor { get; set; }

        public float SpeedGoldTargetSecondsPerCard = 1.0f;
        public int SpeedGoldTargetAmount = 5;
        public float SpeedGoldZeroSecondsPerCard = 6.0f;
        public int SpeedGoldMaxAmount = 15;

        public Card? ResolvingMonster { get; set; }
        public float ResolveTimer { get; set; }
        public int ResolveDamage { get; set; }
        public bool ResolveDamageApplied { get; set; }
        public bool ResolveWeaponUsed { get; set; }
        public float ResolveTargetRotation { get; set; }

        public int DisplayScore { get; set; }
        public int TargetScore { get; set; }
        public float ScoreAnimTimer { get; set; }
        public bool ScoreSlamPlayed { get; set; }

        private Random _random = new Random();
        private readonly List<(float timer, string sfx)> _audioQueue = new List<(float, string)>();

        public void Reset(int startingHealth)
        {
            Health = startingHealth;
            LastSlainValue = 99;
            CardsResolvedThisRoom = 0;
            PotionsUsedThisRoom = 0;
            CanSkip = true;

            FloorTimer = 0f;
            TotalCardsInFloor = 0;

            ResolvingMonster = null;
            ResolveTimer = 0f;
            ResolveDamage = 0;
            ResolveDamageApplied = false;
            ResolveWeaponUsed = false;

            DisplayScore = -208;
            TargetScore = 0;
            ScoreAnimTimer = 0f;
            ScoreSlamPlayed = false;

            _audioQueue.Clear();
        }

        public void Update(float dt)
        {
            for (int i = _audioQueue.Count - 1; i >= 0; i--)
            {
                var item = _audioQueue[i];
                item.timer -= dt;
                if (item.timer <= 0)
                {
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx(item.sfx, 0.15f);
                    _audioQueue.RemoveAt(i);
                }
                else
                {
                    _audioQueue[i] = item;
                }
            }
        }

        public void StartMonsterResolution(Card? monster, int damage, bool weaponUsed)
        {
            if (monster == null) return;

            ResolvingMonster = monster;
            ResolveTimer = 0f;
            ResolveDamage = damage;
            ResolveDamageApplied = false;
            ResolveWeaponUsed = weaponUsed;

            float minRot = 12f * (MathF.PI / 180f);
            float maxRot = 30f * (MathF.PI / 180f);
            ResolveTargetRotation = (minRot + (float)_random.NextDouble() * (maxRot - minRot)) * (_random.Next(2) == 0 ? 1 : -1);
        }

        public void UpdateResolution(float dt, ScoundrelBoardController board, ScoundrelUIController ui, Core core, HapticsManager haptics, RunContext runContext, Action onComplete)
        {
            ResolveTimer += dt;

            float lungeTime = 0.1f;
            float retreatTime = 0.25f;
            float totalTime = lungeTime + retreatTime;

            if (ResolveTimer < lungeTime)
            {
                float p = ResolveTimer / lungeTime;
                if (ResolvingMonster != null) ResolvingMonster.VisualYOffset = Easing.EaseInBack(p) * 24f;
            }
            else if (ResolveTimer < totalTime)
            {
                if (!ResolveDamageApplied)
                {
                    if (ResolveDamage > 0) ApplyDamage(ResolveDamage, ui, core, haptics);
                    else if (ResolveWeaponUsed && ResolveDamage == 0)
                    {
                        runContext.Gold += 1;
                        Vector2 popupPos = ResolvingMonster != null ? ResolvingMonster.Position + new Vector2(0, -20) : new Vector2(Global.VIRTUAL_WIDTH / 2f, 24f);
                        ui.AddFloatingText(1, false, true, popupPos);
                        ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=1600;slide=200;atk=0.01;sus=0.05;dec=0.2;vol=0.05", 0.1f);
                    }

                    if (ResolveWeaponUsed)
                    {
                        string sfxWeaponBlock = "proc:wave=0;freq=400;slide=-100;atk=0.01;sus=0.02;dec=0.2;duty=0.2;vol=0.15|wave=4;freq=100;atk=0.01;sus=0.02;dec=0.15;vol=0.2|wave=6;freq=500;atk=0.01;sus=0.01;dec=0.1;vol=0.1";
                        ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx(sfxWeaponBlock, 0.2f);
                    }

                    string sfxPlayerDamage = "proc:wave=4;freq=120;slide=-40;atk=0.01;sus=0.05;dec=0.2;dist=0.8;lpf=600;vol=0.2|wave=5;freq=100;atk=0.01;sus=0.05;dec=0.2;vol=0.15";
                    string sfxMonsterDamage = "proc:wave=4;freq=60;slide=-20;atk=0.01;sus=0.05;dec=0.2;dist=0.8;lpf=300;vol=0.2|wave=5;freq=50;atk=0.01;sus=0.05;dec=0.2;vol=0.15";

                    float baseFreq = 250f;
                    if (ResolvingMonster != null) baseFreq = MathHelper.Lerp(700f, 250f, (ResolvingMonster.Value - 2f) / 12f);
                    float slideFreq = baseFreq * 0.6f;
                    string sfxMonsterDie = $"proc:wave=4;freq={baseFreq:F0};slide=-{slideFreq:F0};atk=0.02;sus=0.1;dec=0.35;detune=0.04;vibdepth=15;vibspeed=12;vol=0.15|wave=2;freq={baseFreq / 2:F0};slide=-{slideFreq / 2:F0};atk=0.02;sus=0.1;dec=0.35;vol=0.15";

                    if (ResolveDamage > 0)
                    {
                        ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx(sfxPlayerDamage, 0.2f);
                    }

                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx(sfxMonsterDamage, 0.2f);
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx(sfxMonsterDie, 0.2f);

                    ResolveDamageApplied = true;
                }

                float hitT = ResolveTimer - lungeTime;
                float p = hitT / retreatTime;

                if (ResolvingMonster != null)
                {
                    ResolvingMonster.VisualYOffset = MathHelper.Lerp(24f, 0f, Easing.EaseOutBack(p));

                    float rotEase = Easing.EaseOutCubic(p);
                    ResolvingMonster.Rotation = ResolveTargetRotation * rotEase;
                    ResolvingMonster.TargetRotation = ResolvingMonster.Rotation;

                    float decay = 1f - Easing.EaseOutQuad(p);
                    float shakeX = (float)(_random.NextDouble() * 2 - 1) * 10f * decay;
                    float shakeY = (float)(_random.NextDouble() * 2 - 1) * 10f * decay;
                    ResolvingMonster.ShakeOffset = new Vector2(shakeX, shakeY);

                    ResolvingMonster.FlashWhiteIntensity = p < 0.15f ? 1f : (1f - (p - 0.15f) / 0.85f);
                }
            }
            else
            {
                if (ResolvingMonster != null)
                {
                    ResolvingMonster.ShakeOffset = Vector2.Zero;
                    ResolvingMonster.FlashWhiteIntensity = 0f;
                    ResolvingMonster.VisualYOffset = 0f;
                    ResolvingMonster.TargetRotation = ResolveTargetRotation;

                    if (ResolveWeaponUsed)
                    {
                        LastSlainValue = ResolvingMonster.Value;
                        board.MoveToSlainPile(ResolvingMonster);
                    }
                    else
                    {
                        board.MoveToDiscard(ResolvingMonster);
                    }
                }

                onComplete?.Invoke();
            }
        }

        public void CalculateSpeedGold(RunContext runContext, ScoundrelUIController ui)
        {
            float secondsPerCard = TotalCardsInFloor > 0 ? FloorTimer / TotalCardsInFloor : 0f;
            float t = Math.Clamp((SpeedGoldZeroSecondsPerCard - secondsPerCard) / (SpeedGoldZeroSecondsPerCard - SpeedGoldTargetSecondsPerCard), 0f, (float)SpeedGoldMaxAmount / SpeedGoldTargetAmount);
            int speedGold = (int)MathF.Round(t * SpeedGoldTargetAmount);
            speedGold = Math.Clamp(speedGold, 0, SpeedGoldMaxAmount);

            if (speedGold > 0)
            {
                runContext.Gold += speedGold;
                ui.AddFloatingText(speedGold, false, true, new Vector2(Global.VIRTUAL_WIDTH - 30, 24f));
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=1600;slide=200;atk=0.01;sus=0.05;dec=0.2;vol=0.05", 0.1f);
            }
        }

        public void ApplyDamage(int amount, ScoundrelUIController ui, Core core, HapticsManager haptics)
        {
            if (!ServiceLocator.Get<Global>().DebugGodMode)
            {
                Health -= amount;
            }

            ui.HealthPlink.Start(0f, 0.3f);
            haptics.TriggerShake(amount * 1.5f, 0.2f);
            core.TriggerFullscreenFlash(Color.White * 0.4f, 0.05f);

            ui.HpTextFlashTimer = 0.3f;
            ui.HpTextFlashColor = Color.White;

            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=1;freq=200;slide=-100;atk=0.01;sus=0.1;dec=0.2;dist=0.5;vol=0.15", 0.2f);

            ui.AddFloatingText(amount, false);
        }

        public void ApplyHeal(int amount, int maxHealth, ScoundrelUIController ui)
        {
            int actualHeal = Math.Min(amount, maxHealth - Health);
            Health += actualHeal;
            ui.HealthPlink.Start(0f, 0.3f);

            ui.HpTextFlashTimer = 0.3f;
            ui.HpTextFlashColor = Color.White;

            if (actualHeal == 0)
            {
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=300;slide=-50;atk=0.02;sus=0.05;dec=0.15;detune=0.01;vol=0.15", 0.15f);
            }
            else if (Health == maxHealth)
            {
                PlayHealFull();
            }
            else
            {
                PlayHealPartial();
            }

            if (actualHeal > 0)
            {
                ui.AddFloatingText(actualHeal, true);
            }
        }

        public void CalculateTargetScore(ScoundrelBoardController board, int maxHealth)
        {
            TargetScore = Health;
            if (Health <= 0)
            {
                int remainingMonsters = board.Deck.Concat(board.Room).Where(c => c.Type == CardType.Monster).Sum(c => c.Value);
                TargetScore = Health - remainingMonsters;
            }
            else if (Health == maxHealth)
            {
                var bestPotion = board.Room.Where(c => c.Type == CardType.Potion).OrderByDescending(c => c.Value).FirstOrDefault();
                if (bestPotion != null) TargetScore += bestPotion.Value;
            }
            DisplayScore = -208;
            ScoreAnimTimer = 0f;
            ScoreSlamPlayed = false;
        }

        public void UpdateScoreAnimation(float dt)
        {
            if (ScoreAnimTimer < 3.0f)
            {
                ScoreAnimTimer += dt;
                float p = Math.Clamp(ScoreAnimTimer / 3.0f, 0f, 1f);
                float ease = Easing.EaseOutQuint(p);

                if (Health <= 0)
                {
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().MusicPitchOffset = MathHelper.Lerp(0f, -1f, ease);
                }

                int newScore = (int)MathF.Round(MathHelper.Lerp(-208, TargetScore, ease));

                if (newScore != DisplayScore)
                {
                    DisplayScore = newScore;
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx($"proc:wave=2;freq={400 + p * 800};atk=0.01;sus=0;dec=0.05;detune=0.01;vol=0.12", 0.15f);
                }

                if (p >= 1f && !ScoreSlamPlayed)
                {
                    DisplayScore = TargetScore;
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=4;freq=100;slide=-50;atk=0.01;sus=0.1;dec=0.4;detune=0.03;lpf=800;vol=0.3|wave=5;freq=200;atk=0.01;sus=0.1;dec=0.3;lpf=500;vol=0.25", 0.15f);
                    ScoreSlamPlayed = true;
                }
            }
        }

        public void PlayHealFull()
        {
            _audioQueue.Add((0f, "proc:wave=2;freq=400;atk=0.02;sus=0.05;dec=0.15;detune=0.01;delay=0.05;delfb=0.15;vol=0.15"));
            _audioQueue.Add((0.1f, "proc:wave=2;freq=600;atk=0.02;sus=0.05;dec=0.15;detune=0.01;delay=0.05;delfb=0.15;vol=0.15"));
            _audioQueue.Add((0.3f, "proc:wave=2;freq=800;atk=0.02;sus=0.05;dec=0.25;detune=0.01;delay=0.05;delfb=0.15;vol=0.15"));
        }

        public void PlayHealPartial()
        {
            _audioQueue.Add((0f, "proc:wave=2;freq=400;atk=0.02;sus=0.05;dec=0.15;detune=0.01;vol=0.15"));
            _audioQueue.Add((0.1f, "proc:wave=2;freq=600;atk=0.02;sus=0.05;dec=0.2;detune=0.01;vol=0.15"));
        }

        public void PlayDefeatSequence()
        {
            _audioQueue.Add((0f, "proc:wave=4;freq=300;atk=0.05;sus=0.1;dec=0.3;detune=0.02;lpf=2000;vol=0.2"));
            _audioQueue.Add((0.3f, "proc:wave=4;freq=250;atk=0.05;sus=0.1;dec=0.3;detune=0.02;lpf=2000;vol=0.2"));
            _audioQueue.Add((0.9f, "proc:wave=4;freq=200;atk=0.05;sus=0.2;dec=0.6;detune=0.02;lpf=2000;vol=0.2"));
        }

        public void PlayVictorySequence()
        {
            _audioQueue.Add((0f, "proc:wave=0;freq=400;atk=0.02;sus=0.1;dec=0.2;detune=0.01;lpf=3000;vol=0.15"));
            _audioQueue.Add((0.2f, "proc:wave=0;freq=500;atk=0.02;sus=0.1;dec=0.2;detune=0.01;lpf=3000;vol=0.15"));
            _audioQueue.Add((0.6f, "proc:wave=0;freq=600;atk=0.02;sus=0.1;dec=0.2;detune=0.01;lpf=3000;vol=0.15"));
            _audioQueue.Add((1.2f, "proc:wave=0;freq=800;atk=0.02;sus=0.2;dec=0.6;detune=0.01;lpf=3000;vol=0.15"));
        }
    }
}