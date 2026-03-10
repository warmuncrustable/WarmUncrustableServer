using Newtonsoft.Json;

namespace Servers
{
    public class PlayerRecord
    {
        public string Name { get; set; } = "";
    }

    public static class PlayerDatabase
    {
        private static Dictionary<ulong, PlayerRecord> Players = new();
        private static string FilePath = "players.json";

        public static void Load()
        {
            if (!File.Exists(FilePath))
                return;

            string json = File.ReadAllText(FilePath);

            Players = JsonConvert.DeserializeObject<Dictionary<ulong, PlayerRecord>>(json)
                ?? new Dictionary<ulong, PlayerRecord>();
        }

        public static void Save()
        {
            string json = JsonConvert.SerializeObject(Players, Formatting.Indented);
            File.WriteAllText(FilePath, json);
        }

        public static PlayerRecord GetOrCreate(ulong externalId)
        {
            if (!Players.TryGetValue(externalId, out var record))
            {
                record = new PlayerRecord();
                Players[externalId] = record;
                Save();
            }

            return record;
        }

        public static void SetName(ulong externalId, string name)
        {
            var record = GetOrCreate(externalId);
            record.Name = name;
            Save();
        }
    }
}
