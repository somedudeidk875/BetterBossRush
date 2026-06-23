using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using CalamityMod.Events;
using Microsoft.Xna.Framework;

// notes (will add more as the mod develops):
//  the mod considers tier 5 and above one in the same. for example, it will consider IEOR's tier 6 the same as tier 5. may change in the future
//  this was originally made to support the IEOR (with HWR) modpack. it should work with different modpacks so long as i add support for them (see below)
//  config files will come in the future, this is just to see how interested people are.
//  this is also my first mod so bugs are bound to happen
//  let me know what mods i should make compatible. note that when suggesting a mod it is best to suggest the mod that adds the bosses AND the mod that adds the bosses to the boss rush
//  also it would help if you would give the internal name of these mods when suggesting them, saves me trouble :)

// config file plans:
//  tiers that blitz occurs
//  a better way for modpack devs to add exemptions in as me adding them manually is not sustainable


namespace BetterBossRush
{
    public class BetterBossRush : Mod { }

    public class BetterBossRushSystem : ModSystem
    {
        // tracks state and tracking timers for specific active boss slots
        public class BlitzSlot
        {
            public int BossIndex = -1; 
            public bool IsSpawned = false;
            public int SpawnDelayTimer = 0;
            public bool HasConfirmedAlive = false;
            public int ConfirmGraceTimer = 0;

            // restores the tracking tracking slot back to its default unassigned state
            public void Reset()
            {
                BossIndex = -1;
                IsSpawned = false;
                SpawnDelayTimer = 0;
                HasConfirmedAlive = false;
                ConfirmGraceTimer = 0;
            }
        }
        
        //how long to wait for a boss to finish fully spawning before proceeding and giving up the slot (ticks)
        public const int SpawnConfirmGracePeriod = 600;

        // tracks the maximum number of bosses allowed alive at once
        public static BlitzSlot[] Slots = new BlitzSlot[5] { 
            new BlitzSlot(), new BlitzSlot(), new BlitzSlot(), new BlitzSlot(), new BlitzSlot() 
        };        
        // tracking index for the next boss entry waiting to be added to the arena slots
        public static int nextQueuedIndex = -1;
        
        // active challenge bracket tier that determines allowed limits in terms of concurrent blitz stuff
        public static int CurrentBlitzTier = 1; 
        
        // map containing the structural start indices for each tier
        public static Dictionary<int, int> TierStartIndices = new Dictionary<int, int>();
        
        // map containing the structural end indices for each tier
        public static Dictionary<int, int> TierEndIndices = new Dictionary<int, int>();

        // set tracking the IDs of certain bosses that may break the blitz due to things such as tping, certain mechanics, etc
        public static HashSet<int> ExemptionNPCIDs = new HashSet<int>();

        // Entities that the queue should completely skip over so they don't take up slots
        public static HashSet<int> SubEntitySkipIDs = new HashSet<int>();

        // keeps copy of calamity baseline for restored cycles
        private static Dictionary<int, List<int>> originalPermittedNPCs = new Dictionary<int, List<int>>();
        
        // prevents console text flooding
        private static int logTimer = 0;

        //self explanatory
        public static List<int> DyingBosses = new List<int>();

        private static void SetupExemptionList()
        {
            // clear prior records from the collection
            ExemptionNPCIDs.Clear();
            SubEntitySkipIDs.Clear();

            // adds wof to the checkpoint exemption list
            ExemptionNPCIDs.Add(NPCID.WallofFlesh);

            // should be able to find calamity since you need it for this mod but just in case
            if (ModLoader.TryGetMod("CalamityMod", out Mod calamity) && ModLoader.TryGetMod("InfernalEclipseAPI", out Mod infernalEclipseAPI))
            {
                if (calamity.TryFind("ProfanedGuardianCommander", out ModNPC profanedGuardianCommander))
                {
                    ExemptionNPCIDs.Add(profanedGuardianCommander.Type);
                }
                if (calamity.TryFind("Providence", out ModNPC providence))
                {
                    ExemptionNPCIDs.Add(providence.Type);
                }
                if (calamity.TryFind("CeaselessVoid", out ModNPC ceaselessVoid))
                {
                    ExemptionNPCIDs.Add(ceaselessVoid.Type);
                }
                // set the core as the actual boss
                if (calamity.TryFind("SlimeGodCore", out ModNPC slimeGodCore))
                {
                    ExemptionNPCIDs.Add(slimeGodCore.Type);
                }
                // tell the queue to skip the paladins so they don't take up slots
                if (calamity.TryFind("SlimeGod", out ModNPC ebonianPaladin))
                {
                    SubEntitySkipIDs.Add(ebonianPaladin.Type);
                }
                if (calamity.TryFind("SlimeGodRunSplit", out ModNPC crimulanPaladin))
                {
                    SubEntitySkipIDs.Add(crimulanPaladin.Type);
                }
            }

            if (ModLoader.TryGetMod("SOTS", out Mod sots))
            {
                if (sots.TryFind("PharaohsCurse", out ModNPC pharaohsCurse))
                {
                    ExemptionNPCIDs.Add(pharaohsCurse.Type);
                }
                if (sots.TryFind("SubspaceSerpentHead", out ModNPC subspaceSerpentHead))
                {
                    ExemptionNPCIDs.Add(subspaceSerpentHead.Type);
                }
            }
            
            if (ModLoader.TryGetMod("ContinentOfJourney", out Mod continentOfJourney))
            {
                if (Main.GameModeInfo.IsMasterMode && continentOfJourney.TryFind("TheMaterealizer", out ModNPC theMaterealizer))
                {
                    ExemptionNPCIDs.Add(theMaterealizer.Type);                    
                }
                if (Main.GameModeInfo.IsMasterMode && continentOfJourney.TryFind("TheOverwatcher", out ModNPC theOverwatcher))
                {
                    ExemptionNPCIDs.Add(theOverwatcher.Type);                    
                }                 
                if (continentOfJourney.TryFind("WorldsEndEverlastingFallingWhale", out ModNPC worldsEndEverlastingFallingWhale))
                {
                    ExemptionNPCIDs.Add(worldsEndEverlastingFallingWhale.Type);
                }                
            }

            if (ModLoader.TryGetMod("Clamity", out Mod clamity))
            {
                if (clamity.TryFind("WallOfBronze", out ModNPC wallOfBronze))
                {
                    ExemptionNPCIDs.Add(wallOfBronze.Type);                    
                }    
            }

            if (ModLoader.TryGetMod("NoxusBoss", out Mod noxusBoss))
            {
                if (noxusBoss.TryFind("AvatarOfEmptiness", out ModNPC avatarOfEmptiness))
                {
                    ExemptionNPCIDs.Add(avatarOfEmptiness.Type);                    
                }    
            }
            // to find the mod id, the easiest way is to go to client.log and look at the name used when loading the mod, you must use the exact name given
            // to find the entity id, the mod prints out the boss rush in client.log, you can find the exact id there
        }

        public override void OnWorldLoad()
        {
            // reset tracking pointers back to setup configuration values
            nextQueuedIndex = -1;
            CurrentBlitzTier = 1;
            
            // cleans active slots
            for (int i = 0; i < Slots.Length; i++) 
            {
                Slots[i].Reset();
            }

            // clear internal data caches
            originalPermittedNPCs.Clear();
            TierStartIndices.Clear();
            TierEndIndices.Clear();

            DyingBosses.Clear();
            SetupExemptionList();

            // again, just in case
            if (ModLoader.HasMod("CalamityMod"))
            {
                // traverse active boss collections to save foundational deletion rules
                for (int i = 0; i < BossRushEvent.Bosses.Count; i++)
                {
                    var boss = BossRushEvent.Bosses[i];
                    if (boss.HostileNPCsToNotDelete != null)
                    {
                        originalPermittedNPCs[i] = new List<int>(boss.HostileNPCsToNotDelete);
                    }
                }

                // preserve default sequence configurations before what comes next
                int originalStage = BossRushEvent.BossRushStage;
                int currentDetectedTier = -1;

                // loop through available entries to determine exact tier grouping milestones
                for (int i = 0; i < BossRushEvent.Bosses.Count; i++)
                {
                    BossRushEvent.BossRushStage = i;
                    int evaluatedTier = BossRushEvent.CurrentTier;

                    // execute tracking logic if a new tier group boundary is detected
                    if (evaluatedTier != currentDetectedTier)
                    {
                        if (currentDetectedTier != -1)
                        {
                            TierEndIndices[currentDetectedTier] = i - 1;
                        }
                        TierStartIndices[evaluatedTier] = i;
                        currentDetectedTier = evaluatedTier;
                    }
                }
                
                // finalize tracking indices for the terminal grouping sequence
                if (currentDetectedTier != -1)
                {
                    TierEndIndices[currentDetectedTier] = BossRushEvent.Bosses.Count - 1;
                }
                
                //will only progress to the next tier once all bosses from the current tier are dead
                foreach (KeyValuePair<int, int> tierEnd in TierEndIndices)
                {
                    int endBossIndex = tierEnd.Value;
                    if (endBossIndex >= 0 && endBossIndex < BossRushEvent.Bosses.Count)
                    {
                        int endBossEntityID = BossRushEvent.Bosses[endBossIndex].EntityID;
                        
                        ExemptionNPCIDs.Add(endBossEntityID);
                    }
                }

                // restore default operational stage pointer back to original reference values
                BossRushEvent.BossRushStage = originalStage; 

                LogBossRushList();
            }
        }

        // update all regestries
        private static void UpdateUniversalWhitelists()
        {
            if (!ModLoader.HasMod("CalamityMod") || !BossRushEvent.BossRushActive) 
            {
                return;
            }

            // create a temporary collection containing all concurrent participant definitions
            List<int> unifiedWhitelist = new List<int>();
            for (int i = 0; i < Slots.Length; i++)
            {
                int activeBossIndex = Slots[i].BossIndex;
                if (activeBossIndex != -1 && originalPermittedNPCs.ContainsKey(activeBossIndex))
                {
                    unifiedWhitelist.AddRange(originalPermittedNPCs[activeBossIndex]);
                }
            }

            //add bosses that are currently in their death animation to the whitelist so they are protected
            for (int i = 0; i < DyingBosses.Count; i++)
            {
                int dyingBossIndex = DyingBosses[i];
                if (originalPermittedNPCs.ContainsKey(dyingBossIndex))
                {
                        unifiedWhitelist.AddRange(originalPermittedNPCs[dyingBossIndex]);
                }
            
            }

            // many mods have this weird thing where certain bosses (notably ones from vanilla) will delete other currently active bosses. this prevents that
            for (int i = 0; i < BossRushEvent.Bosses.Count; i++)
            {
                if (originalPermittedNPCs.ContainsKey(i))
                {
                    var bossEntry = BossRushEvent.Bosses[i];
                    if (bossEntry.HostileNPCsToNotDelete != null)
                    {
                        bossEntry.HostileNPCsToNotDelete.Clear();
                        bossEntry.HostileNPCsToNotDelete.AddRange(originalPermittedNPCs[i]);
                        
                        // ensures all active crossstage ids are kept valid globally
                        foreach (int npcId in unifiedWhitelist)
                        {
                            if (!bossEntry.HostileNPCsToNotDelete.Contains(npcId))
                            {
                                bossEntry.HostileNPCsToNotDelete.Add(npcId);
                            }
                        }
                    }
                }
            }
        }

        // intercepts simulation logic steps before entity updates process
        public override void PreUpdateEntities()
        {
            if (ModLoader.HasMod("CalamityMod") && BossRushEvent.BossRushActive)
            {
                if (!IsTierEnabled(CurrentBlitzTier))
                {
                    int calTier = BossRushEvent.CurrentTier;
                    if (calTier > CurrentBlitzTier)
                    {
                        CurrentBlitzTier = calTier;
                    }
                }

                // apply safety overrides 
                if (IsTierEnabled(CurrentBlitzTier))
                {
                    BossRushEvent.BossRushSpawnCountdown = 180; 
                    UpdateUniversalWhitelists(); 
                }
            }
        }

        // executes tracking "conveyor" alignment loop algorithms after global logic updates
        public override void PostUpdateWorld()
        {
            // Terminate processing and clear runtime values if system parameters fail checks
            if (!ModLoader.HasMod("CalamityMod") || !BossRushEvent.BossRushActive)
            {
                nextQueuedIndex = -1;
                CurrentBlitzTier = 1;
                for (int i = 0; i < Slots.Length; i++) 
                {
                    Slots[i].Reset();
                }
                return;
            }

            int currentTierRuntime = CurrentBlitzTier;
            int tierStartIndex = 0;
            
            // replaces ternary logic to safely determine current tier tracking start index
            if (TierStartIndices.ContainsKey(currentTierRuntime))
            {
                tierStartIndex = TierStartIndices[currentTierRuntime];
            }
            else
            {
                tierStartIndex = 0;
            }

            int endCapIndex = 0;
            
            // ditto but end index
            if (TierEndIndices.ContainsKey(currentTierRuntime))
            {
                endCapIndex = TierEndIndices[currentTierRuntime];
            }
            else
            {
                endCapIndex = BossRushEvent.Bosses.Count - 1;
            }

            // protective whitelist tracking if handling active phase configurations
            if (IsTierEnabled(currentTierRuntime))
            {
                UpdateUniversalWhitelists(); 
            }

            // halt world management routines if calamity does its boss tier animation stuff
            bool animationPlaying = Main.projectile.Any(p => p.active && p.type == ModContent.ProjectileType<CalamityMod.Projectiles.Typeless.BossRushTierAnimation>());
            if (animationPlaying) 
            {
                return;
            }

            // handle allocation and tracking details 
            if (IsTierEnabled(currentTierRuntime))
            {
                if (nextQueuedIndex < tierStartIndex) 
                {
                    nextQueuedIndex = tierStartIndex;
                }

                
                // assigns how many slots each tier gets when spawning a boss
                int maxSlots = GetSlotsForTier(currentTierRuntime);

                // process active challenge slots and evaluate entity life 
                for (int i = 0; i < maxSlots; i++)
                {
                    if (Slots[i].BossIndex != -1)
                    {
                        if (Slots[i].IsSpawned)
                        {
                            if (IsBossAlive(Slots[i].BossIndex))
                            {
                                // boss has materialized, from now on a "not alive" reading is a real death
                                Slots[i].HasConfirmedAlive = true;
                            }
                            else if (Slots[i].HasConfirmedAlive)
                            {
                                // it was confirmed alive earlier and is no longer alive = genuinely dead
                                Slots[i].Reset();
                            }
                            else
                            {
                                // freshly spawned and not registered yet, this basically holds the slot for an incoming boss that may have a spawn animation
                                Slots[i].ConfirmGraceTimer--;
                                if (Slots[i].ConfirmGraceTimer <= 0)
                                {
                                    Slots[i].Reset();
                                }
                            }
                        }
                        else if (IsBossInDeathAnimation(Slots[i].BossIndex) && CurrentBlitzTier <= 2)
                        {
                            // move boss to the dying registry so it is still protected but frees the slot
                            DyingBosses.Add(Slots[i].BossIndex);
                            Slots[i].Reset();
                        }
                        else
                        {
                            Slots[i].SpawnDelayTimer--;
                            if (Slots[i].SpawnDelayTimer <= 0)
                            {
                                SpawnBossSafely(Slots[i].BossIndex);
                                Slots[i].IsSpawned = true;
                                Slots[i].HasConfirmedAlive = false;
                                Slots[i].ConfirmGraceTimer = SpawnConfirmGracePeriod;
                            }
                        }
                    }
                }

                for (int i = DyingBosses.Count - 1; i >= 0; i--)
                {
                    int dyingIndex = DyingBosses[i];
                    if (!IsBossAlive(dyingIndex))
                    {
                        DyingBosses.RemoveAt(i);
                    }
                }

                // determine volume stats across tracking components currently occupied
                int activeSlotsCount = Slots.Count(s => s.BossIndex != -1 && Array.IndexOf(Slots, s) < maxSlots);

                // detects bosses at the end of tier, should adapt to modlists
                bool gateBossActive = false;
                for (int i = 0; i < maxSlots; i++)
                {
                    if (Slots[i].BossIndex != -1 && IsGateIndex(Slots[i].BossIndex, endCapIndex))
                    {
                        gateBossActive = true;
                        break;
                    }
                }

                // maintain conveyor registers if arena bounds allow bosses to be allocated/check for "shared" fights 
                if (!gateBossActive && nextQueuedIndex <= endCapIndex)
                {
                    for (int i = 0; i < maxSlots; i++)
                    {
                        if (Slots[i].BossIndex == -1)
                        {
                            // if the queued boss is on skip list, skip it immediately. (manual override)
                            int targetNPCID = BossRushEvent.Bosses[nextQueuedIndex].EntityID;
                            if (SubEntitySkipIDs.Contains(targetNPCID))
                            {
                                nextQueuedIndex++;
                                i--; // offset the loop tracker so this slot isn't skipped for valid entries
                                continue;
                            }

                            if (IsGateIndex(nextQueuedIndex, endCapIndex))
                            {
                                // isolation block until all previous entities clear
                                if (activeSlotsCount == 0)
                                {
                                    Slots[i].BossIndex = nextQueuedIndex;
                                    Slots[i].IsSpawned = false;
                                    Slots[i].SpawnDelayTimer = 120; 
                                    nextQueuedIndex++;
                                    activeSlotsCount++;
                                }
                                break; 
                            }
                            else
                            {
                                Slots[i].BossIndex = nextQueuedIndex;
                                Slots[i].IsSpawned = false;
                                Slots[i].SpawnDelayTimer = 30 + (i * 45);
                                nextQueuedIndex++;
                                activeSlotsCount++;
                            }
                        }
                    }
                }

                // only advance tier if current tier is fully finished
                if (nextQueuedIndex > endCapIndex)
                {
                    bool arenaEmpty = true;
                    for (int i = 0; i < Slots.Length; i++)
                    {
                        if (Slots[i].BossIndex != -1)
                        {
                            arenaEmpty = false;
                            break;
                        }
                    }

                    if (arenaEmpty && DyingBosses.Count == 0)
                    {
                        string liveBosses = "";
                        for (int n = 0; n < Main.maxNPCs; n++)
                        {
                            NPC npc = Main.npc[n];
                            if (npc.active && npc.boss)
                            {
                                liveBosses += $"[type={npc.type} name={npc.TypeName} life={npc.life}/{npc.lifeMax}] ";
                            }
                        }
                        if (liveBosses == "")
                        {
                            liveBosses = "(none)";
                        }
                        Mod.Logger.Info($"[BBR ADVANCE] tier {CurrentBlitzTier} -> {CurrentBlitzTier + 1} | tierStart={tierStartIndex} endCap={endCapIndex} queuePtr={nextQueuedIndex} | live boss-flagged NPCs: {liveBosses}");

                        CurrentBlitzTier++;
                    }
                }

                // determine the highest active index value
                int highestActiveIndex = -1;
                for (int i = 0; i < maxSlots; i++)
                {
                    if (Slots[i].BossIndex != -1 && Slots[i].IsSpawned && Slots[i].BossIndex > highestActiveIndex)
                    {
                        highestActiveIndex = Slots[i].BossIndex;
                    }
                }

                // align default stage variables to preserve stability parameters across updates
                if (highestActiveIndex != -1)
                {
                    BossRushEvent.BossRushStage = highestActiveIndex;
                }
                else
                {
                    BossRushEvent.BossRushStage = Math.Min(endCapIndex, Math.Max(tierStartIndex, nextQueuedIndex - 1));
                }
            }
            else
            {
                nextQueuedIndex = -1;
            }

            // logs
            logTimer++;
            if (logTimer >= 60)
            {
                logTimer = 0;
                LogDiagnostics();
            }
        }

        // assesses if an evaluation position maps to an exempted boss or gate boss
        public static bool IsGateIndex(int index, int endCapIndex)
        {
            if (index == endCapIndex) 
            {
                return true; 
            }
            if (index < 0 || index >= BossRushEvent.Bosses.Count) 
            {
                return false;
            }

            int npcID = BossRushEvent.Bosses[index].EntityID;
            return ExemptionNPCIDs.Contains(npcID);
        }

        // scans simulation parts to verify presence of targeted boss identifiers
        private bool IsBossAlive(int bossIndex)
        {
            if (bossIndex < 0 || bossIndex >= BossRushEvent.Bosses.Count) 
            {
                return false;
            }
            int mainID = BossRushEvent.Bosses[bossIndex].EntityID;


            // check if the main boss entity is still active. added because the code below wasn't enough
            if (NPC.AnyNPCs(mainID))
            {
                return true;
            }

            // this basically detects multi part bosses. this works even with modded npcs like anahita, desert scourge, etc (at least it should)
            if (mainID == NPCID.EaterofWorldsHead) 
            {
                return NPC.AnyNPCs(NPCID.EaterofWorldsHead) || NPC.AnyNPCs(NPCID.EaterofWorldsBody) || NPC.AnyNPCs(NPCID.EaterofWorldsTail);
            }
            if (mainID == NPCID.Spazmatism) 
            {
                return NPC.AnyNPCs(NPCID.Spazmatism) || NPC.AnyNPCs(NPCID.Retinazer);
            }
            if (mainID == NPCID.TheDestroyer) 
            {
                return NPC.AnyNPCs(NPCID.TheDestroyer) || NPC.AnyNPCs(NPCID.TheDestroyerBody) || NPC.AnyNPCs(NPCID.TheDestroyerTail);
            }


            // protects bosses with multi-entity components being cleared prematurely
            if (originalPermittedNPCs.ContainsKey(bossIndex))
            {
                foreach (int subID in originalPermittedNPCs[bossIndex])
                {
                    if (NPC.AnyNPCs(subID))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // spawns the boss in a way that doesnt result in different conditions than usual. the goal of this mod is to preserve the original boss fights as much as possible while also making it quicker and efficient 
        private void SpawnBossSafely(int index)
        {
            if (index < 0 || index >= BossRushEvent.Bosses.Count) 
            {
                return;
            }
            var bossEntry = BossRushEvent.Bosses[index];
            int targetNPCID = bossEntry.EntityID;

            string bossName = "";
            
            // look up names of the boss
            if (targetNPCID < NPCID.Count)
            {
                bossName = NPCID.Search.GetName(targetNPCID);
            }
            else
            {
                bossName = "Modded Target";
            }
            //debug
            //Main.NewText($"[Better Boss Rush] Spawning Blitz Target: {bossName} (Index {index})", 100, 180, 255);

            // DIAGNOSTIC: record which boss is being spawned, the Calamity tier that boss's index
            // belongs to, and the blitz tier we currently think we're on. If calTierOfBoss is ever
            // GREATER than blitzTier, a boss from a later tier is being spawned before the current
            // tier finished - that is the premature-progression bug, caught at the source.
            int stageBackupForLog = BossRushEvent.BossRushStage;
            BossRushEvent.BossRushStage = index;
            int calTierOfBoss = BossRushEvent.CurrentTier;
            BossRushEvent.BossRushStage = stageBackupForLog;
            Mod.Logger.Info($"[BBR SPAWN] {bossName} | index={index} | calTierOfBoss={calTierOfBoss} | blitzTier={CurrentBlitzTier} | queuePtr={nextQueuedIndex}");

            try
            {
                // align environmental environment requirements before generating targets
                if (bossEntry.ToChangeTimeTo != BossRushEvent.TimeChangeContext.None)
                {
                    Main.dayTime = (bossEntry.ToChangeTimeTo == BossRushEvent.TimeChangeContext.Day);
                    
                    if (Main.dayTime)
                    {
                        Main.time = 27000.0;
                    }
                    else
                    {
                        Main.time = 16200.0;
                    }
                }

                // process audio signatures if target handles audio triggers traditionally
                if (!bossEntry.UsesSpecialSound)
                {
                    Terraria.Audio.SoundEngine.PlaySound(BossRushEvent.BossSummonSound, Main.player[BossRushEvent.ClosestPlayerToWorldCenter].Center);
                }

                BossRushEvent.BossRushStage = index;
                bossEntry.SpawnContext.Invoke(targetNPCID);
            }
            catch (Exception ex)
            {
                Mod.Logger.Error($"[Better Boss Rush] Blitz Spawn exception at index {index}: {ex.Message}");
            }
        }

        // more logging shenanigans
        private void LogDiagnostics()
        {
            string slotData = "";
            for (int i = 0; i < Slots.Length; i++) 
            {
                slotData += $"[Slot {i}: Idx={Slots[i].BossIndex}, Spawned={Slots[i].IsSpawned}, Delay={Slots[i].SpawnDelayTimer}] ";
            }
            Mod.Logger.Info($"[BBR Diagnostics] InternalTier={CurrentBlitzTier}, CalStage={BossRushEvent.BossRushStage}, QueuePtr={nextQueuedIndex}\n{slotData}");
        }

        // ditto
        private void LogBossRushList()
        {
            if (!ModLoader.HasMod("CalamityMod")) 
            {
                return;
            }

            Mod.Logger.Info("=================== BETTER BOSS RUSH: ACTIVE ROSTER ROLL CALL ===================");
            
            for (int i = 0; i < BossRushEvent.Bosses.Count; i++)
            {
                var bossEntry = BossRushEvent.Bosses[i];
                int npcID = bossEntry.EntityID;
                string bossName;

                if (npcID < NPCID.Count)
                {
                    bossName = NPCID.Search.GetName(npcID);
                }
                else
                {
                    var modNPC = NPCLoader.GetNPC(npcID);
                    if (modNPC != null)
                    {
                        bossName = modNPC.Name;
                    }
                    else
                    {
                        bossName = $"Unknown Modded ID ({npcID})";
                    }
                }
                
                int originalStageBackup = BossRushEvent.BossRushStage;
                BossRushEvent.BossRushStage = i;
                int associatedTier = BossRushEvent.CurrentTier;
                BossRushEvent.BossRushStage = originalStageBackup;

                Mod.Logger.Info($"Index: {i} | Tier: {associatedTier} | Name: {bossName} | Entity ID: {npcID}");
            }

            Mod.Logger.Info("=================================================================================");
        }
        // checks if a boss is currently playing a death animation by scanning all of its active segments/parts
        private bool IsBossInDeathAnimation(int bossIndex)
        {
            if (bossIndex < 0 || bossIndex >= BossRushEvent.Bosses.Count) 
            {
                return false;
            }

            int mainID = BossRushEvent.Bosses[bossIndex].EntityID;
            List<int> associatedIDs = new List<int>();
            associatedIDs.Add(mainID);

            // pull all segments, parts, or subentities related to this boss fight
            if (originalPermittedNPCs.ContainsKey(bossIndex))
            {
                foreach (int subID in originalPermittedNPCs[bossIndex])
                {
                    if (!associatedIDs.Contains(subID))
                    {
                        associatedIDs.Add(subID);
                    }
                }
            }

            bool anyPartsActive = false;

            // scan every active npc in the world, shouldnt cause perfomance issues
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active)
                {
                    // if the active npc belongs to this boss (head, body segment, tail, or twin)
                    if (associatedIDs.Contains(npc.type))
                    {
                        anyPartsActive = true;
                        bool partIsDying = false;

                        // check the exact invincibility and life threshold rules 
                        if (npc.dontTakeDamage)
                        {
                            if (npc.life <= 1)
                            {
                                partIsDying = true;
                            }
                        }

                        if (npc.life <= 0)
                        {
                            partIsDying = true;
                        }

                        // if even one single active segment or part is still fully fighting, the boss as a whole is not ready to free up its slot yet
                        if (!partIsDying)
                        {
                            return false;
                        }
                    }
                }
            }

            // returns true only if we found active pieces and every single one of them was in a death animation state
            return anyPartsActive;
        }
        public static bool IsTierEnabled(int tier)
        {
            var config = ModContent.GetInstance<BetterBossRushConfig>();
            switch (tier)
            {
                case 1: return config.Tier1Blitz;
                case 2: return config.Tier2Blitz;
                case 3: return config.Tier3Blitz;
                case 4: return config.Tier4Blitz;
                case 5: return config.Tier5Blitz;
                default: return false;
            }
        }
        
        //fetches from config
        public static int GetSlotsForTier(int tier)
        {
            var config = ModContent.GetInstance<BetterBossRushConfig>();
            switch (tier)
            {
                case 1: return config.SlotsTier1;
                case 2: return config.SlotsTier2;
                case 3: return config.SlotsTier3;
                case 4: return config.SlotsTier4;
                case 5: return config.SlotsTier5;
                default: return 2; // Default fallback
            }
        }
    }

    // some bosses have different ai that requires some level of acknowledgement in this mod to make the boss rush flow. this class handles that
    public class BossRushBlitzNPC : GlobalNPC
    {
        public static int originalStageBackup = -1;

        // executes operations immediately prior to normal npc ai loops
        public override bool PreAI(NPC npc)
        {
            if (ModLoader.HasMod("CalamityMod") && BossRushEvent.BossRushActive)
            {
                int activeSlotIndex = -1;
                for(int i = 0; i < BetterBossRushSystem.Slots.Length; i++) 
                {
                    int bIdx = BetterBossRushSystem.Slots[i].BossIndex;
                    if(bIdx != -1) 
                    {
                        var bossDef = BossRushEvent.Bosses[bIdx];
                        if(bossDef.EntityID == npc.type || (bossDef.HostileNPCsToNotDelete != null && bossDef.HostileNPCsToNotDelete.Contains(npc.type))) 
                        {
                            activeSlotIndex = bIdx;
                            break;
                        }
                    }
                }

                // check if the boss is in the dying registry to keep it protected
                if (activeSlotIndex == -1)
                {
                    for (int i = 0; i < BetterBossRushSystem.DyingBosses.Count; i++)
                    {
                        int bIdx = BetterBossRushSystem.DyingBosses[i];
                        var bossDef = BossRushEvent.Bosses[bIdx];
                        
                        if (bossDef.EntityID == npc.type)
                        {
                            activeSlotIndex = bIdx;
                            break;
                        }
                        if (bossDef.HostileNPCsToNotDelete != null)
                        {
                            if (bossDef.HostileNPCsToNotDelete.Contains(npc.type))
                            {
                                activeSlotIndex = bIdx;
                                break;
                            }
                        }
                    }
                }

                if (activeSlotIndex != -1)
                {
                    npc.timeLeft = NPC.activeTime; 
                    
                    originalStageBackup = BossRushEvent.BossRushStage;
                    BossRushEvent.BossRushStage = activeSlotIndex;
                }
            }
            return base.PreAI(npc);
        }

        // this reverts back to the standard ai tracking once the ai is finished
        public override void PostAI(NPC npc)
        {
            if (originalStageBackup != -1)
            {
                BossRushEvent.BossRushStage = originalStageBackup;
                originalStageBackup = -1;
            }
            base.PostAI(npc);
        }
    }
}
