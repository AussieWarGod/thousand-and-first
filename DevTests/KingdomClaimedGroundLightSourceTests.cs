#if TAF_TESTS
using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The claimed-ground light. It is presentation on the engine's own render dispatch, so the
	/// contract that matters is what it is NOT: not omniscience, not whole-map sight, not a thaw.
	/// </summary>
	[TestFixture]
	public sealed class KingdomClaimedGroundLightSourceTests
	{
		private const string PartFile = "Growth/KingdomClaimedGroundLight.cs";
		private const string ProjectionFile = "Growth/KingdomClaimedGroundLight.Projection.cs";
		private const string EventsFile = "Core/KingdomSystem.z20.Events.cs";
		private const string OptionId = "r_TAF_OptionClaimedGroundLight";

		[Test]
		public void ZonePartLightsOnlyTheRenderedFrameTheFounderStandsIn()
		{
			string part = Source(PartFile);
			StringAssert.Contains("namespace XRL.World.ZoneParts", part);
			StringAssert.Contains("[Serializable]", part);
			StringAssert.Contains("class KingdomClaimedGroundLight : IZonePart", part);
			StringAssert.Contains("ID == BeforeRenderEvent.ID", part);
			StringAssert.Contains("HandleEvent(BeforeRenderEvent E)", part);
			StringAssert.Contains("The.Player != null", part);
			StringAssert.Contains("ParentZone.HasObject(The.Player)", part);
			StringAssert.Contains("ParentZone.AddLight(LightLevel.Light)", part);
			ClassicAssert.Less(part.IndexOf("The.Player != null", StringComparison.Ordinal),
				part.IndexOf("ParentZone.HasObject(The.Player)", StringComparison.Ordinal),
				"the render dispatch runs before the engine touches the player, and "
					+ "Zone.HasObject dereferences what it is handed");
			StringAssert.Contains("Writer.WriteNamedFields(this, typeof(KingdomClaimedGroundLight))",
				part);
			StringAssert.Contains("Reader.ReadNamedFields(this, typeof(KingdomClaimedGroundLight))",
				part);
			foreach (string forbidden in Forbidden)
				StringAssert.DoesNotContain(forbidden, part);
		}

		[Test]
		public void ProjectionAttachesOnlyOnClaimedGroundAndExploresExactlyOnce()
		{
			string projection = Source(ProjectionFile);
			StringAssert.Contains("public const string OptionId = \"" + OptionId + "\"", projection);
			StringAssert.Contains("Options.GetOption(OptionId, \"Yes\") != \"No\"", projection);
			StringAssert.Contains("System.ClaimedZones.Contains(Zone.ZoneID)", projection);
			StringAssert.Contains("Zone.AddPart(light)", projection);
			StringAssert.Contains("Zone.RemovePart(light)", projection);
			ClassicAssert.AreEqual(1, Regex.Matches(projection, Regex.Escape("Zone.ExploreAll()")).Count,
				"the floor is remembered once per activation, in one place");
			foreach (string forbidden in Forbidden)
				StringAssert.DoesNotContain(forbidden, projection);
			StringAssert.DoesNotContain("UnexploreAll", projection);
		}

		[Test]
		public void AStoppedRealmLosesTheLightRatherThanKeepingItUnrevoked()
		{
			string projection = Source(ProjectionFile);
			StringAssert.Contains(
				"if (!Enabled || !KingdomMaster.NewWorkAllowed(System)", projection,
				"the master gate must revoke on the same branch as the option and the claim, "
					+ "not return and leave a standing light behind");
			ClassicAssert.AreEqual(2,
				Regex.Matches(projection, Regex.Escape("RemoveZone(Zone);")).Count,
				"every refusal in ReconcileZone revokes: the combined guard and ambiguous ground");
			ClassicAssert.AreEqual(1,
				Regex.Matches(projection, Regex.Escape("KingdomMaster.NewWorkAllowed")).Count,
				"one master reading, inside that guard");
		}

		[Test]
		public void ActivationReconcilesAfterTheWardAndRevokesOnGroundNoLongerClaimed()
		{
			string events = Source(EventsFile);
			int removal = events.IndexOf("KingdomClaimedGround.RemoveZone(E.Zone)",
				StringComparison.Ordinal);
			int custody = events.IndexOf("AttendFormerClaimCustody(E.Zone)",
				StringComparison.Ordinal);
			int ward = events.LastIndexOf("KingdomAssentingMoot.ReconcileZone(this, E.Zone)",
				StringComparison.Ordinal);
			int reconcile = events.IndexOf("KingdomClaimedGround.ReconcileZone(this, E.Zone)",
				StringComparison.Ordinal);
			int dispatcher = events.IndexOf("KingdomSemanticDispatcher.OnZoneActivated",
				StringComparison.Ordinal);
			ClassicAssert.Greater(removal, 0, "a lost claim must revoke the light on its own branch");
			ClassicAssert.Less(removal, custody, "revoke before the former-claim custody attendance");
			ClassicAssert.Greater(reconcile, ward, "the ward projects first");
			ClassicAssert.Less(reconcile, dispatcher, "and before the attended settlement pass");
		}

		[Test]
		public void ThePartIsRegisteredForRemovalAndTheOptionIsShippedOnAndDocumented()
		{
			ClassicAssert.IsTrue(KingdomRemovalCoverage.IsCustomZonePart("KingdomClaimedGroundLight"),
				"an unregistered zone part has no removal disposition");
			string options = Source(Path.Combine("RuntimeData", "Options.xml"));
			string row = Regex.Match(options,
				"<option\\s+ID=\"" + OptionId + "\"[^>]+>").Value;
			StringAssert.Contains("Type=\"Checkbox\"", row);
			StringAssert.Contains("Default=\"Yes\"", row);
			StringAssert.Contains(OptionId, Source(Path.Combine("docs", "API.md")));
			StringAssert.Contains(OptionId, Source("PLAYTESTING.md"));
		}

		private static readonly string[] Forbidden = new string[]
		{
			"AddVisibility", "GetZone(", "LightAll", "LightLevel.Omniscient", "SetExplored",
			"VisAll"
		};

		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}
	}
}
#endif
