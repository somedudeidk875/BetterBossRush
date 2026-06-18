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

            // restores the tracking tracking slot back to its default unassigned state
            public void Reset()
            {
                BossIndex = -1;
                IsSpawned = false;
                SpawnDelayTimer = 0;
            }
        }

        // tracks the maximum number of bosses allowed alive at once
        public static BlitzSlot[] Slots = new BlitzSlot[3] { new BlitzSlot(), new BlitzSlot(), new BlitzSlot() };
        
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

        private static void SetupExemptionList()
        {
            // clear prior records from the collection
            ExemptionNPCIDs.Clear();
            SubEntitySkipIDs.Clear();

            // adds wof to the checkpoint exemption list
            ExemptionNPCIDs.Add(NPCID.WallofFlesh);

            // should be able to find calamity since you need it for this mod but just in case
            if (ModLoader.TryGetMod("CalamityMod", out Mod calamity))
            {
                if (calamity.TryFind("ProfanedGuardianCommander", out ModNPC profanedGuardianCommander))
                {
                    ExemptionNPCIDs.Add(profanedGuardianCommander.Type);
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
            }

            //to find the mod id, the easiest way is to go to client.log and look at the name used when loading the mod, you must use the exact name given
            //to find the entity id, the mod prints out the boss rush in client.log, you can find the exact id there
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
                int calTier = BossRushEvent.CurrentTier;
                if (calTier > 2) 
                {
                    CurrentBlitzTier = calTier;
                }

                // apply safety overrides 
                if (CurrentBlitzTier == 1 || CurrentBlitzTier == 2)
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
            if (currentTierRuntime == 1 || currentTierRuntime == 2)
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
            if (currentTierRuntime == 1 || currentTierRuntime == 2)
            {
                if (nextQueuedIndex < tierStartIndex) 
                {
                    nextQueuedIndex = tierStartIndex;
                }

                int maxSlots = 0;
                
                // assigns how many slots each tier gets when spawning a boss
                if (currentTierRuntime == 1)
                {
                    maxSlots = 3;
                }
                else
                {
                    maxSlots = 2;
                }

                // process active challenge slots and evaluate entity life 
                for (int i = 0; i < maxSlots; i++)
                {
                    if (Slots[i].BossIndex != -1)
                    {
                        if (Slots[i].IsSpawned)
                        {
                            // starts cleanup in slot if target entity is dead
                            if (!IsBossAlive(Slots[i].BossIndex))
                            {
                                if (Slots[i].BossIndex == endCapIndex)
                                {
                                    CurrentBlitzTier++; 
                                }
                                Slots[i].Reset();
                            }
                        }
                        else
                        {
                            Slots[i].SpawnDelayTimer--;
                            if (Slots[i].SpawnDelayTimer <= 0)
                            {
                                SpawnBossSafely(Slots[i].BossIndex);
                                Slots[i].IsSpawned = true;
                            }
                        }
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
