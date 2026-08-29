using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.UI;
using XRL.Wish;
using XRL.World;

namespace ThousandAndFirst
{
	public partial class KingdomWishes
	{
		private sealed class LabRegistrationExpectation
		{
			internal readonly string CatalogueGrant;
			internal readonly string RuntimeClass;
			internal readonly bool AllowStatic;

			internal LabRegistrationExpectation(string CatalogueGrant,
				string RuntimeClass, bool AllowStatic)
			{
				this.CatalogueGrant = CatalogueGrant;
				this.RuntimeClass = RuntimeClass;
				this.AllowStatic = AllowStatic;
			}
		}

		// Build 2.0.211.51 anchors: GameObject.cs:1992-2051 omits registered
		// part-event rows when AllowStaticRegistration() is true; IPart.cs:167-170
		// defaults it to false. NephalChord is the catalogue's reviewed family name,
		// expanded here to every concrete pinned-build chord that can be copied.
		private static readonly LabRegistrationExpectation[] LabRegistrationExpectations =
		{
			new LabRegistrationExpectation("ActiveLightSource", "ActiveLightSource", false),
			new LabRegistrationExpectation("DrunkOnHit", "DrunkOnHit", false),
			new LabRegistrationExpectation("GiantHands", "GiantHands", true),
			new LabRegistrationExpectation("LifeDrainOnHit", "LifeDrainOnHit", true),
			new LabRegistrationExpectation("NephalChord", "AgolgotChord", false),
			new LabRegistrationExpectation("NephalChord", "BethsaidaChord", false),
			new LabRegistrationExpectation("NephalChord", "QasChord", false),
			new LabRegistrationExpectation("NephalChord", "QonChord", false),
			new LabRegistrationExpectation("NephalChord", "RermadonChord", false),
			new LabRegistrationExpectation("NephalChord", "ShugruithChord", false),
			new LabRegistrationExpectation("ReflectDamage", "ReflectDamage", false),
			new LabRegistrationExpectation("SapChargeOnHit", "SapChargeOnHit", true),
			new LabRegistrationExpectation("SapOnPenetration", "SapOnPenetration", true),
			new LabRegistrationExpectation("SporePuffer", "SporePuffer", false),
			new LabRegistrationExpectation("StickOnHit", "StickOnHit", false),
			new LabRegistrationExpectation("Swarmer", "Swarmer", true),
			new LabRegistrationExpectation("TemperatureVenting", "TemperatureVenting", false)
		};

		/// <summary>Read-only native ABI trace. It creates detached part instances only;
		/// it does not attach them, touch a zone, mutate a save, or sign an evidence receipt.</summary>
		[WishCommand("kingdom:labregistration", null)]
		public static void LabRegistrationEvidenceWish()
		{
			SortedSet<string> loaded = new SortedSet<string>(StringComparer.Ordinal);
			List<LabProcedure> procedures = KingdomProcedures.All;
			for (int i = 0; i < procedures.Count; i++)
				if (procedures[i] != null && procedures[i].Source == LabSource.Part
					&& !string.IsNullOrEmpty(procedures[i].Grants))
					loaded.Add(procedures[i].Grants);

			SortedSet<string> expected = new SortedSet<string>(StringComparer.Ordinal);
			SortedSet<string> serializedFamilies = new SortedSet<string>(StringComparer.Ordinal);
			SortedSet<string> staticFamilies = new SortedSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < LabRegistrationExpectations.Length; i++)
			{
				LabRegistrationExpectation row = LabRegistrationExpectations[i];
				expected.Add(row.CatalogueGrant);
				(row.AllowStatic ? staticFamilies : serializedFamilies).Add(row.CatalogueGrant);
			}

			StringBuilder report = new StringBuilder();
			report.Append("UNSIGNED native lab registration trace\nQud ")
				.Append(XRLGame.CoreVersion).Append("; engine assembly ")
				.Append(typeof(IPart).Assembly.GetName().Version).Append('\n');
			bool passed = loaded.SetEquals(expected) && serializedFamilies.Count == 7
				&& staticFamilies.Count == 5;
			if (!loaded.SetEquals(expected))
				report.Append("CATALOGUE DRIFT loaded=").Append(string.Join(",", loaded))
					.Append(" expected=").Append(string.Join(",", expected)).Append('\n');

			for (int i = 0; i < LabRegistrationExpectations.Length; i++)
			{
				LabRegistrationExpectation row = LabRegistrationExpectations[i];
				Type type = ModManager.ResolveType("XRL.World.Parts." + row.RuntimeClass)
					?? ModManager.ResolveType("XRL.World.Parts.Mutation." + row.RuntimeClass);
				IPart part = null;
				try
				{
					if (type != null && !type.IsAbstract && typeof(IPart).IsAssignableFrom(type))
						part = Activator.CreateInstance(type) as IPart;
				}
				catch (Exception error)
				{
					report.Append(row.CatalogueGrant).Append('/').Append(row.RuntimeClass)
						.Append(" constructor failed: ").Append(error.GetType().Name).Append('\n');
				}
				bool actual = part != null && part.AllowStaticRegistration();
				bool rowPassed = part != null && actual == row.AllowStatic;
				passed &= rowPassed;
				string line = row.CatalogueGrant + "/" + row.RuntimeClass
					+ " cache-key=" + (type?.AssemblyQualifiedName ?? "missing")
					+ " allow-static=" + (actual ? "1" : "0")
					+ " save-row=" + (actual ? "omitted-by-engine" : "serialized-when-registered")
					+ " " + (rowPassed ? "PASS" : "FAIL");
				report.Append(line).Append('\n');
				KingdomLog.Log("[TAF lab-registration] " + line);
			}
			report.Append("families with serialized registration rows=")
				.Append(serializedFamilies.Count).Append("; static families=")
				.Append(staticFamilies.Count).Append("; RESULT=")
				.Append(passed ? "PASS" : "FAIL")
				.Append("\nNo evidence receipt was written; capture this output in the pinned native build.");
			Popup.Show(report.ToString());
		}
	}
}
