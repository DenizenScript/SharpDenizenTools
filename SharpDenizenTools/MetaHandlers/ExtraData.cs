using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SharpDenizenTools.MetaHandlers
{
    /// <summary>Helper class for processing extra minecraft-related data.</summary>
    public class ExtraData
    {
        /// <summary>Source link for the extra-data FDS document.</summary>
        public static string EXTRA_DATA_SOURCE = "https://meta.denizenscript.com/data/minecraft.fds";

        /// <summary>The current extra data object (if loaded).</summary>
        public static ExtraData Data;

        /// <summary>The processed data section.</summary>
        public FDSSection DataSection;

        /// <summary>The raw relevant data sets.</summary>
        public HashSet<string> Blocks, Items, Particles, Effects, Sounds, Entities, Enchantments, Biomes, Attributes, Gamerules, PotionEffects, Potions, Statistics;

        /// <summary>Loads an <see cref="ExtraData"/> instance.</summary>
        /// <param name="cachePath">Optional file path for data caching.</param>
        public static ExtraData Load(string cachePath = null)
        {
            ExtraData result = new ExtraData();
            try
            {
                string content = null;
                if (cachePath != null)
                {
                    if (File.Exists(cachePath) && DateTime.UtcNow.Subtract(File.GetLastWriteTimeUtc(cachePath)).TotalDays < 15)
                    {
                        content = File.ReadAllText(cachePath);
                    }
                }
                if (content == null)
                {
                    HttpClient webClient = new HttpClient
                    {
                        Timeout = new TimeSpan(0, 2, 0)
                    };
                    webClient.DefaultRequestHeaders.UserAgent.ParseAdd("DenizenMetaScanner/1.0");
                    content = webClient.GetStringAsync(EXTRA_DATA_SOURCE).Result;
                    if (content != null && cachePath != null)
                    {
                        File.WriteAllText(cachePath, content);
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
        }

        private HashSet<string> GetDataSet(string type)
        {
            return new HashSet<string>((DataSection.GetStringList(type.ToLowerFast()) ?? new List<string>()).Select(s => s.ToLowerFast()));
        }
    }
}
