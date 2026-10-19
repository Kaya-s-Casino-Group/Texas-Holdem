using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using PokerEngine;
using UnityEditor;
using UnityEngine;

namespace Poker.SingleBattle.Tests
{
    /// <summary>Owns runtime-only XML copies. Never mutates an authored asset or an attached rule snapshot.</summary>
    internal sealed class BattleTestContent : IDisposable
    {
        public readonly XDocument Document;
        private readonly SingleBattleSceneAdapter host;
        private readonly List<TextAsset> owned = new List<TextAsset>();
        private readonly string encounter;
        public BattleTestContent(SingleBattleSceneAdapter host, string xml, string encounter)
        { this.host = host; this.encounter = encounter; Document = XDocument.Parse(xml); }
        public static BattleTestContent Create(SingleBattleSceneAdapter host, int players, int enemies)
        {
            var doc = XDocument.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(Editor.SingleBattleSetup.SceneBattleContentPath).text);
            doc.Root.Element("BattleRules").ReplaceWith(new XElement("BattleRules", new XElement("Rules",
                new XAttribute("id", "test"), new XAttribute("roundLimit", 8), new XAttribute("scoringRef", "single-battle-score"))));
            doc.Root.Element("Players").ReplaceWith(new XElement("Players", Enumerable.Range(0, players).Select(i => new XElement("Player",
                new XAttribute("id", "hero-" + i), new XAttribute("maxHealth", 40)))));
            doc.Root.Element("Enemies").ReplaceWith(new XElement("Enemies", Enumerable.Range(0, enemies).Select(i => new XElement("Enemy",
                new XAttribute("id", "foe-" + i), new XAttribute("maxHealth", 75), new XAttribute("damage", 7)))));
            var encounter = new XElement("Encounter", new XAttribute("id", "test"), new XAttribute("rulesRef", "test"));
            for (int i = 0; i < players; i++) encounter.Add(new XElement("Player", new XAttribute("id", i == 0 ? "player" : "player-" + i), new XAttribute("ref", "hero-" + i)));
            for (int i = 0; i < enemies; i++) encounter.Add(new XElement("Enemy", new XAttribute("id", i == 0 ? "enemy" : "enemy-" + i), new XAttribute("ref", "foe-" + i)));
            doc.Root.Element("Encounters").ReplaceWith(new XElement("Encounters", encounter));
            var result = new BattleTestContent(host, doc.ToString(), "test"); result.Apply(); return result;
        }
        public XElement Actor(bool enemy = false, int index = 0)
        {
            var slot = Document.Root.Element("Encounters").Elements("Encounter").Single(e => (string)e.Attribute("id") == encounter)
                .Elements(enemy ? "Enemy" : "Player").ElementAt(index);
            return Document.Root.Element(enemy ? "Enemies" : "Players").Elements().Single(e => (string)e.Attribute("id") == (string)slot.Attribute("ref"));
        }
        private static string AttributeName(string field) => field == "playerHealth" || field == "enemyHealth" ? "maxHealth" :
            field == "enemyDamage" ? "damage" : field == "playRefillTiming" ? "playRefill" : field == "discardRefillTiming" ? "discardRefill" : field;
        public int Get(string field)
        {
            string value = (string)Actor(field.StartsWith("enemy", StringComparison.Ordinal)).Attribute(AttributeName(field));
            if (field.EndsWith("RefillTiming", StringComparison.Ordinal)) return (int)Enum.Parse(typeof(CardDuelRefillTiming), value ?? (field == "playRefillTiming" ? "NextPlayerTurn" : "AfterAction"));
            if (value == null) return field == "maxEnergy" || field == "initialEnergy" ? 3 : 1;
            return int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        public void Set(string field, int value)
        {
            object serialized = field.EndsWith("RefillTiming", StringComparison.Ordinal) ? ((CardDuelRefillTiming)value).ToString() : (object)value;
            Actor(field.StartsWith("enemy", StringComparison.Ordinal)).SetAttributeValue(AttributeName(field), serialized); Apply();
        }
        public void SetReward(int? amount) { Actor().SetAttributeValue("discardBlockPerCard", amount); Apply(); }
        public void SetCardPoints(int value)
        {
            foreach (var rank in Document.Descendants("Rank")) rank.SetAttributeValue("points", value);
            Apply();
        }
        public void Apply()
        {
            var asset = new TextAsset(Document.ToString()) { name = "runtime-test-content" }; owned.Add(asset);
            host.ConfigureContent(asset, encounter);
        }
        public void Dispose() { foreach (var asset in owned) UnityEngine.Object.DestroyImmediate(asset); }
    }
}
