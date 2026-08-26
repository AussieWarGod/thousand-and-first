#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Source-level lock for W2's authority cut. Public roster fields remain reflected
	/// save ABI, but no production consumer may quietly turn them back into a second living roll.</summary>
	public class KingdomResidentAuthoritySourceTests
	{
		private static readonly string[] ProductionDirectories =
		{
			"Api", "Chronicle", "Core", "Debug", "Experience", "Growth", "Quests",
			"Raids", "Simulation", "Trade"
		};

		private static readonly HashSet<string> LegacyBoundaryFiles = new HashSet<string>(
			StringComparer.OrdinalIgnoreCase)
		{
			"Core/KingdomSystem.cs",
			"Core/KingdomSettlement.cs",
			"Simulation/City/KingdomResidents.cs"
		};

		[Test]
		public void LegacyParallelRosterFieldsHaveNoProductionConsumer()
		{
			List<string> offenders = new List<string>();
			foreach (string relative in ProductionSources())
			{
				if (LegacyBoundaryFiles.Contains(relative) || IsKingdomSystemSource(relative)) continue;
				string source = TestMain.ReadRepositoryText(relative);
				if (source.Contains("RosterNames") || source.Contains("RosterOrigins")
					|| source.Contains("RosterArrived")) offenders.Add(relative);
			}
			CollectionAssert.IsEmpty(offenders,
				"legacy roster columns are migration/projection ABI only: "
					+ string.Join(", ", offenders));
		}

		[Test]
		public void FlatResidentColumnsStayBehindTheBoundedRowService()
		{
			HashSet<string> allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			{
				"Simulation/City/KingdomCityBook.cs",
				"Simulation/City/KingdomResidents.cs"
			};
			string[] tokens =
			{
				".ResidentIds", ".ResidentNames", ".ResidentOrigins", ".ResidentArrived",
				".ResidentStandings", ".ResidentCauses", ".ResidentBoundZoneIds"
			};
			List<string> offenders = new List<string>();
			foreach (string relative in ProductionSources())
			{
				if (allowed.Contains(relative) || IsResidentRulesSource(relative)) continue;
				string source = TestMain.ReadRepositoryText(relative);
				for (int i = 0; i < tokens.Length; i++)
					if (source.Contains(tokens[i])) { offenders.Add(relative); break; }
			}
			CollectionAssert.IsEmpty(offenders,
				"resident columns bypass row service: " + string.Join(", ", offenders));
		}

		[Test]
		public void EveryLiveMutationAndOfficePathUsesResidentRows()
		{
			string growth = TestMain.ReadRepositoryText("Growth/KingdomGrowth.cs");
			StringAssert.Contains("KingdomResidents.RollRows(System, true)", growth);
			StringAssert.Contains("KingdomResidents.TryEnsureRow(system, settler", growth);
			StringAssert.Contains("KingdomResidents.TryDepart(System, leaver", growth);
			StringAssert.DoesNotContain("System.Population - System.WaterCrew", growth);

			string guests = TestMain.ReadRepositoryText("Experience/KingdomGuestbook.cs");
			StringAssert.Contains("KingdomResidents.TryEnsureRow(system, guest", guests);
			string lifecycle = KingdomGuestLifecycleLogicalSource.Read();
			StringAssert.Contains("KingdomResidents.OnRollCount(system)", lifecycle);

			string offices = TestMain.ReadRepositoryText("Experience/KingdomOffices.cs");
			StringAssert.Contains("KingdomResidents.TryMarkDead(system, Citizen", offices);
			StringAssert.Contains("KingdomResidents.TryHead(System, out headRow)", offices);
			string reports = TestMain.ReadRepositoryText("Core/KingdomReportsPeople.cs");
			StringAssert.Contains("KingdomResidents.TryRoll(System", reports);
			string archive = TestMain.ReadRepositoryText("Core/KingdomSealRules.Ground.cs");
			StringAssert.Contains("KingdomResidentRules.TryProject(state, out roll)", archive);
		}

		[Test]
		public void ReflectedCompatibilityFieldsAreExplicitlyObsolete()
		{
			string system = KingdomSystemLogicalSource.Read();
			string settlement = TestMain.ReadRepositoryText("Core/KingdomSettlement.cs");
			Assert.AreEqual(3, Count(system, "[Obsolete(\"Compatibility projection only;"));
			Assert.AreEqual(3, Count(settlement, "[Obsolete(\"Compatibility projection only;"));
			StringAssert.Contains("KingdomResidents.AdoptLegacyAuthority(this)", system);
		}

		[Test]
		public void BoundBodyResolutionUsesExactEngineIdentityWithoutRemoteZoneWalks()
		{
			string source = TestMain.ReadRepositoryText(
				"Simulation/City/KingdomResidents.cs");
			int resolver = source.IndexOf("internal static bool TryResolveBoundBody",
				StringComparison.Ordinal);
			int binding = source.IndexOf("public static bool Bind(", resolver,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(resolver, 0);
			Assert.Greater(binding, resolver);
			string exact = source.Substring(resolver, binding - resolver);
			StringAssert.Contains("GameObject.FindByID(Binding.ObjectId)", exact);
			StringAssert.Contains("Binding.Kind == KingdomBindingKind.Resident", exact);
			StringAssert.Contains("Binding.Kind == KingdomBindingKind.Transient", exact);
			StringAssert.Contains("exact.CurrentZone?.ZoneID, binding.ZoneId", exact);
			StringAssert.DoesNotContain("zone.GetObjects()", exact);

			int presence = source.IndexOf("private static KingdomBodyPresence PresenceOf(",
				StringComparison.Ordinal);
			int books = source.IndexOf("private static IEnumerable<KingdomCityBook> Books(",
				presence, StringComparison.Ordinal);
			Assert.Greater(books, presence);
			string presenceBody = source.Substring(presence, books - presence);
			StringAssert.Contains("FindExactBindingObject(binding)", presenceBody);
			StringAssert.DoesNotContain("GetObjects()", presenceBody);
			StringAssert.DoesNotContain("GetZone(", presenceBody);
		}

		private static IEnumerable<string> ProductionSources()
		{
			for (int i = 0; i < ProductionDirectories.Length; i++)
			{
				string root = Path.Combine(TestMain.RepositoryRoot, ProductionDirectories[i]);
				if (!Directory.Exists(root)) continue;
				foreach (string path in Directory.GetFiles(root, "*.cs",
					SearchOption.AllDirectories))
				{
					yield return path.Substring(TestMain.RepositoryRoot.Length + 1)
						.Replace(Path.DirectorySeparatorChar, '/');
				}
			}
		}

		private static int Count(string source, string token)
		{
			int count = 0;
			for (int at = 0; (at = source.IndexOf(token, at, StringComparison.Ordinal)) >= 0;
				at += token.Length) count++;
			return count;
		}

		private static bool IsResidentRulesSource(string relative)
		{
			return relative.StartsWith("Simulation/City/KingdomResidentRules",
				StringComparison.OrdinalIgnoreCase)
				&& relative.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
		}

		private static bool IsKingdomSystemSource(string relative)
		{
			return relative.StartsWith("Core/KingdomSystem", StringComparison.OrdinalIgnoreCase)
				&& relative.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
		}
	}
}
#endif
