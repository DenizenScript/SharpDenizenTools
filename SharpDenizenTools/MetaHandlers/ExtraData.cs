using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using SharpDenizenTools.ScriptAnalysis;

namespace SharpDenizenTools.MetaHandlers
{
    /// <summary>Helper class for processing extra minecraft-related data.</summary>
    public class ExtraData
    {
        /// <summary>Source link for the extra-data FDS document.</summary>
        public static string EXTRA_DATA_SOURCE = "https://meta.denizenscript.com/data/minecraft.fds";

        /// <summary>The current extra data object (if loaded).</summary>
        public static ExtraData Data;

        /// <summary>Cache path for the extra data file.</summary>
        public static string CachePath;

        /// <summary>If true, the cache file MUST be loaded (if it exists).</summary>
        public static bool ForceCache;

        /// <summary>The processed data section.</summary>
        public FDSSection DataSection;

        /// <summary>The raw relevant data sets.</summary>
        public HashSet<string> Blocks, Items, Particles, Effects, Sounds, Entities, Enchantments, Biomes, Attributes, Gamerules, PotionEffects, Potions, Statistics;

        /// <summary>A combination set of all enum keywords.</summary>
        public HashSet<string> All = new();

        /// <summary>Relevant data sets as arrays.</summary>
        public string[] ItemArray, BlockArray, EntityArray;

        /// <summary>Loads an <see cref="ExtraData"/> instance.</summary>
        public static ExtraData Load()
        {
            ExtraData result = new();
            try
            {
                string content = null;
                if (CachePath != null)
                {
                    if (File.Exists(CachePath) && (ForceCache || DateTime.UtcNow.Subtract(File.GetLastWriteTimeUtc(CachePath)).TotalDays < 15))
                    {
                        content = File.ReadAllText(CachePath);
                    }
                }
                if (content == null)
                {
                    using HttpClient webClient = new()
                    {
                        Timeout = new TimeSpan(0, 2, 0)
                    };
                    webClient.DefaultRequestHeaders.UserAgent.ParseAdd("DenizenMetaScanner/1.0");
                    content = webClient.GetStringAsync(EXTRA_DATA_SOURCE).Result;
                    if (content != null && CachePath != null)
                    {
                        File.WriteAllText(CachePath, content);
                    }
                }
                result.DataSection = new FDSSection(content);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Extra data loading failed: {ex}");
                if (result.DataSection == null)
                {
                    result.DataSection = new FDSSection();
                }
            }
            result.ProcAllFromFDS();
            Data = result;
            return result;
        }

        private void ProcAllFromFDS()
        {
            Blocks = GetDataSet("Blocks");
            Items = GetDataSet("Items");
            Particles = GetDataSet("Particles");
            Effects = GetDataSet("Effects");
            Sounds = GetDataSet("Sounds");
            Entities = GetDataSet("Entities");
            Enchantments = GetDataSet("Enchantments");
            Biomes = GetDataSet("Biomes");
            Attributes = GetDataSet("Attributes");
            Gamerules = GetDataSet("Gamerules");
            PotionEffects = GetDataSet("PotionEffects");
            Potions = GetDataSet("Potions");
            Statistics = GetDataSet("Statistics");
            ItemArray = Items.ToArray();
            EntityArray = Entities.ToArray();
            BlockArray = Blocks.ToArray();
        }

        private HashSet<string> GetDataSet(string type)
        {
            HashSet<string> result = new((DataSection.GetStringList(type.ToLowerFast()) ?? new List<string>()).Select(s => s.ToLowerFast()));
            All.UnionWith(result);
            return result;
        }

        private static readonly Random random = new();

        private static string Select(params string[] options)
        {
            if (options.Length == 0)
            {
                return "(error!)";
            }
            return options[random.Next(options.Length)];
        }

        /// <summary>Suggests a random example value for the given type.</summary>
        public string SuggestExampleFor(string type)
        {
            if (type.StartsWithFast('\'') && type.EndsWithFast('\''))
            {
                return type[1..^1];
            }
            if (random.NextDouble() > 0.7)
            {
                return type;
            }
            return type switch
            {
                "entity" => random.NextDouble() > 0.5 ? Select(SpecialEntityMatchables.ToArray()) : Select(EntityArray),
                "projectile" => Select("projectile", "arrow", "snowball"),
                "vehicle" => Select("vehicle", "minecart", "horse"),
                "item" => Select(ItemArray),
                "block" => Select(BlockArray),
                "material" => random.NextDouble() > 0.5 ? Select(BlockArray) : Select(ItemArray),
                "area" => Select("area", "cuboid", "polygon"),
                "inventory" => Select(InventoryMatchers.ToArray()),
                "world" => Select("world", "world_nether", "world_the_end", "space", "survivalland"),
                _ => type,
            };
        }

        /// <summary>Known always-valid entity labels.</summary>
        public static HashSet<string> SpecialEntityMatchables = new()
        {
            "entity", "npc", "player", "living", "vehicle", "fish", "projectile", "hanging", "monster", "mob", "animal"
        };

        /// <summary>Type matcher for EntityTag.</summary>
        public int MatchEntity(string word, bool precise)
        {
            if (word.StartsWith("entity_flagged:") || word.StartsWith("player_flagged:") || word.StartsWith("npc_flagged:")
                || SpecialEntityMatchables.Contains(word)
                || Entities.Contains(word))
            {
                return 10;
            }
            if (precise)
            {
                if (AdvancedMatcher.IsAdvancedMatchable(word))
                {
                    AdvancedMatcher.MatchHelper matcher = AdvancedMatcher.CreateMatcher(word);
                    return Entities.Any(e => matcher.DoesMatch(e)) ? 7 : 0;
                }
                return 0;
            }
            if (AdvancedMatcher.IsAdvancedMatchable(word))
            {
                return 5;
            }
            if (Blocks.Contains(word) || Items.Contains(word))
            {
                return 0;
            }
            return 1;
        }

        /// <summary>Known always-valid item labels.</summary>
        public static HashSet<string> ItemCouldMatchPrefixes = new()
        {
            "item_flagged", "vanilla_tagged", "item_enchanted", "material_flagged", "raw_exact"
        };

        /// <summary>Type matcher for ItemTag.</summary>
        public int MatchItem(string word, bool precise)
        {
            if (word == "block")
            {
                return 0;
            }
            if (ItemCouldMatchPrefixes.Contains(word.Before(':'))
                || word == "item" || word == "potion"
                || Items.Contains(word))
            {
                return 10;
            }
            if (precise)
            {
                if (AdvancedMatcher.IsAdvancedMatchable(word))
                {
                    AdvancedMatcher.MatchHelper matcher = AdvancedMatcher.CreateMatcher(word);
                    return Items.Any(e => matcher.DoesMatch(e)) ? 7 : 0;
                }
                return 0;
            }
            if (AdvancedMatcher.IsAdvancedMatchable(word))
            {
                return 5;
            }
            if (Blocks.Contains(word) || Entities.Contains(word))
            {
                return 0;
            }
            return 1;
        }

        /// <summary>Known always-valid inventory labels.</summary>
        public static HashSet<string> InventoryMatchers = new()
        {
            "inventory", "notable", "note",
            "npc", "player", "crafting", "enderchest", "workbench", "entity", "location", "generic",
            // This should maybe be in the data file.
            "chest", "dispenser", "dropper", "furnace", "workbench", "crafting", "enchanting", "brewing", "player",
            "creative", "merchant", "ender_chest", "anvil", "smithing", "beacon", "hopper", "shulker_box", "barrel", "blast_furnace",
            "lectern", "smoker", "loom", "cartography", "grindstone", "stonecutter", "composter"
        };

        /// <summary>Type matcher for InventoryTag.</summary>
        public int MatchInventory(string word, bool precise)
        {
            if (InventoryMatchers.Contains(word)
                || word.StartsWith("inventory_flagged:"))
            {
                return 10;
            }
            if (precise)
            {
                if (AdvancedMatcher.IsAdvancedMatchable(word))
                {
                    AdvancedMatcher.MatchHelper matcher = AdvancedMatcher.CreateMatcher(word);
                    return InventoryMatchers.Any(e => matcher.DoesMatch(e)) ? 7 : 0;
                }
                return 0;
            }
            if (AdvancedMatcher.IsAdvancedMatchable(word))
            {
                return 5;
            }
            if (Blocks.Contains(word) || Items.Contains(word) || Entities.Contains(word))
            {
                return 0;
            }
            return 1;
        }

        /// <summary>Type matcher for blocks.</summary>
        public int MatchBlock(string word, bool precise)
        {
            if (word == "item")
            {
                return 0;
            }
            if (word == "material" || word == "block"
                || word.StartsWith("vanilla_tagged:") || word.StartsWith("material_flagged:")
                || Blocks.Contains(word))
            {
                return 10;
            }
            if (precise)
            {
                if (AdvancedMatcher.IsAdvancedMatchable(word))
                {
                    AdvancedMatcher.MatchHelper matcher = AdvancedMatcher.CreateMatcher(word);
                    return Blocks.Any(e => matcher.DoesMatch(e)) ? 7 : 0;
                }
                return 0;
            }
            if (AdvancedMatcher.IsAdvancedMatchable(word))
            {
                return 5;
            }
            if (Items.Contains(word) || Entities.Contains(word))
            {
                return 0;
            }
            return 1;
        }

        /// <summary>Type matcher for MaterialTag.</summary>
        public int MatchMaterial(string word, bool precise)
        {
            return Math.Max(MatchBlock(word, precise), MatchItem(word, precise));
        }

        /// <summary>Type matcher for areas.</summary>
        public int MatchArea(string word, bool precise)
        {
            if (word == "area" || word == "cuboid" || word == "polygon" || word == "ellipsoid"
                || word.StartsWith("area_flagged:") || word.StartsWith("biome:"))
            {
                return 10;
            }
            if (precise)
            {
                if (AdvancedMatcher.IsAdvancedMatchable(word))
                {
                    return 2;
                }
                return 0;
            }
            if (AdvancedMatcher.IsAdvancedMatchable(word))
            {
                return 5;
            }
            if (Items.Contains(word) || Blocks.Contains(word) || Entities.Contains(word))
            {
                return 0;
            }
            return 1;
        }


        /// <summary>Type matcher for WorldTag.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static")]
        public int MatchWorld(string word, bool precise)
        {
            return 1; // TODO: ?
        }

        /// <summary>Validator type data for event matching.</summary>
        public Dictionary<string, Func<string, bool, int>> KnownValidatorTypes;

        /// <summary>Constructs an instance of <see cref="ExtraData"/>.</summary>
        public ExtraData()
        {
            KnownValidatorTypes = new Dictionary<string, Func<string, bool, int>>()
            {
                { "entity", MatchEntity },
                { "projectile", MatchEntity },
                { "hanging", MatchEntity },
                { "vehicle", MatchEntity },
                { "item", MatchItem },
                { "inventory", MatchInventory },
                { "block", MatchBlock },
                { "material", MatchMaterial },
                { "area", MatchArea },
                { "world", MatchWorld }
            };
        }
    }
}
