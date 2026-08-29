using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomGreatArchiveRulesTests
	{
		[Test]
		public void DisplayTierBoundMatchesResearchRegistry()
		{
			Assert.AreEqual(4, KingdomGreatArchiveRules.MaxTier);
		}

		[Test]
		public void ReadModelHasNoResearchCommandOrProgressSurface()
		{
			string[] banned = { "effort", "progress", "accrued", "subject", "queue",
				"budget", "timer", "command", "spend" };
			foreach (Type type in new[] { typeof(KingdomGreatArchiveCityFacts),
				typeof(KingdomGreatArchiveNodeFacts),
				typeof(KingdomGreatArchiveRequirementFacts),
				typeof(KingdomGreatArchiveAlternativeFacts),
				typeof(KingdomGreatArchiveRow), typeof(KingdomGreatArchiveMap) })
				foreach (System.Reflection.FieldInfo field in type.GetFields())
					foreach (string token in banned)
						StringAssert.DoesNotContain(token,
							field.Name.ToLowerInvariant(), type.Name + "." + field.Name);
		}

		[Test]
		public void UnionIsDeterministicAndKeepsCitySpecificHoldings()
		{
			List<KingdomGreatArchiveCityFacts> cities = new List<KingdomGreatArchiveCityFacts> {
				City("city-b", "Bey Lah", "pressure"),
				City("city-a", "Akrish", "notes", "pressure")
			};
			List<KingdomGreatArchiveNodeFacts> nodes = new List<KingdomGreatArchiveNodeFacts> {
				Node("pressure", "pressure lore", "water", 2, true, "notes"),
				Node("notes", "keeper's notation", "letters", 1, true)
			};
			Assert.IsTrue(KingdomGreatArchiveRules.TryBuild(cities, nodes,
				out KingdomGreatArchiveMap map, out string failure), failure);
			CollectionAssert.AreEqual(new[] { "Akrish", "Bey Lah" }, map.CityNames);
			Assert.AreEqual(2, map.Rows.Count);
			Assert.AreEqual("notes", map.Rows[0].Key);
			Assert.AreEqual("pressure", map.Rows[1].Key);
			CollectionAssert.AreEqual(new[] { "Akrish", "Bey Lah" },
				map.Rows[1].HoldingCityNames);
			CollectionAssert.AreEqual(new[] { "keeper's notation" },
				map.Rows[1].RequirementClauses);
		}

		[Test]
		public void UndiscoveredUnheldRowsAndDependenciesStayInvisible()
		{
			List<KingdomGreatArchiveCityFacts> cities = new List<KingdomGreatArchiveCityFacts> {
				City("city-a", "Akrish")
			};
			List<KingdomGreatArchiveNodeFacts> nodes = new List<KingdomGreatArchiveNodeFacts> {
				Node("heard", "heard road", "branch", 2, true, "hidden"),
				Node("hidden", "secret road", "branch", 1, false)
			};
			Assert.IsTrue(KingdomGreatArchiveRules.TryBuild(cities, nodes,
				out KingdomGreatArchiveMap map, out string failure), failure);
			Assert.AreEqual(1, map.Rows.Count);
			Assert.AreEqual("heard", map.Rows[0].Key);
			Assert.IsEmpty(map.Rows[0].RequirementClauses);
		}

		[Test]
		public void AlternativesStayOrClausesAndHiddenEdgesDoNotLeak()
		{
			List<KingdomGreatArchiveCityFacts> cities = new List<KingdomGreatArchiveCityFacts> {
				City("city-a", "Akrish")
			};
			KingdomGreatArchiveNodeFacts heard = Node("heard", "heard road", "branch", 2,
				true);
			heard.Requirements.Add(Requirement(
				Alternative("hidden", "secret road"), Alternative(null, "a glass furnace")));
			List<KingdomGreatArchiveNodeFacts> nodes = new List<KingdomGreatArchiveNodeFacts> {
				heard, Node("hidden", "secret road", "branch", 1, false)
			};
			Assert.IsTrue(KingdomGreatArchiveRules.TryBuild(cities, nodes,
				out KingdomGreatArchiveMap map, out string failure), failure);
			CollectionAssert.AreEqual(new[] { "a glass furnace" },
				map.Rows[0].RequirementClauses);
			nodes[1].Discovered = true;
			Assert.IsTrue(KingdomGreatArchiveRules.TryBuild(cities, nodes,
				out map, out failure), failure);
			CollectionAssert.AreEqual(new[] { "secret road or a glass furnace" },
				map.Rows[1].RequirementClauses);
		}

		[Test]
		public void UnknownHeldNodeAndDuplicateAuthorityFailClosed()
		{
			List<KingdomGreatArchiveNodeFacts> nodes = new List<KingdomGreatArchiveNodeFacts> {
				Node("notes", "notes", "letters", 1, true)
			};
			Assert.IsFalse(KingdomGreatArchiveRules.TryBuild(new[] {
				City("city-a", "Akrish", "missing") }, nodes, out _, out _));
			Assert.IsFalse(KingdomGreatArchiveRules.TryBuild(new[] {
				City("city-a", "Akrish"), City("city-a", "Other")
			}, nodes, out _, out _));
			Assert.IsFalse(KingdomGreatArchiveRules.TryBuild(new[] {
				City("city-a", "Akrish")
			}, new[] { nodes[0], nodes[0] }, out _, out _));
			KingdomGreatArchiveNodeFacts broken = Node("broken", "broken", "letters",
				2, true, "missing");
			Assert.IsFalse(KingdomGreatArchiveRules.TryBuild(new[] {
				City("city-a", "Akrish")
			}, new[] { broken }, out _, out _));
		}

		private static KingdomGreatArchiveCityFacts City(string id, string name,
			params string[] held)
		{
			return new KingdomGreatArchiveCityFacts {
				SettlementId = id, DisplayName = name,
				HeldNodeKeys = new List<string>(held)
			};
		}

		private static KingdomGreatArchiveNodeFacts Node(string key, string name,
			string branch, int tier, bool discovered, params string[] required)
		{
			KingdomGreatArchiveNodeFacts node = new KingdomGreatArchiveNodeFacts {
				Key = key, DisplayName = name, Branch = branch, Tier = tier,
				Discovered = discovered
			};
			for (int i = 0; i < required.Length; i++)
				node.Requirements.Add(Requirement(Alternative(required[i], required[i])));
			return node;
		}

		private static KingdomGreatArchiveRequirementFacts Requirement(
			params KingdomGreatArchiveAlternativeFacts[] alternatives)
		{
			return new KingdomGreatArchiveRequirementFacts {
				Alternatives = new List<KingdomGreatArchiveAlternativeFacts>(alternatives)
			};
		}

		private static KingdomGreatArchiveAlternativeFacts Alternative(string key,
			string name)
		{
			return new KingdomGreatArchiveAlternativeFacts {
				NodeKey = key, DisplayName = name
			};
		}
	}
}
