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
		private const string SightOptionId = "r_TAF_OptionCitySight";

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
			foreach (string forbidden in ForbiddenEverywhere)
				StringAssert.DoesNotContain(forbidden, part);
		}

		/// <summary>
		/// City sight is a projection of ONE drawn frame. The contract is the shape that keeps it
		/// an eye rather than a rule: nothing is taken on a frame the engine will not draw, the
		/// honest map is snapshotted before the zone is opened, the restore is registered exactly
		/// once, and the explored map is never written here.
		/// </summary>
		[Test]
		public void CitySightProjectsOneDrawnFrameAndPutsTheHonestMapBackInsideIt()
		{
			string part = Source(PartFile);
			StringAssert.Contains("public const string CitySightOptionId = \"" + SightOptionId + "\"",
				part);
			StringAssert.Contains("Options.GetOption(CitySightOptionId, \"Yes\") != \"No\"", part);
			StringAssert.Contains("if (GameManager.bDraw == 11) return;", part);
			ClassicAssert.Less(part.IndexOf("GameManager.bDraw", StringComparison.Ordinal),
				part.IndexOf("ParentZone.VisAll()", StringComparison.Ordinal),
				"a frame the engine abandons before Render never runs the after-render restore, so "
					+ "the projection must not be taken on it at all");
			ClassicAssert.AreEqual(1,
				Regex.Matches(part, Regex.Escape("ParentZone.VisAll()")).Count,
				"the zone is opened in exactly one place");
			ClassicAssert.Less(part.IndexOf("ParentZone.AddLight(LightLevel.Light)",
					StringComparison.Ordinal),
				part.IndexOf("ParentZone.VisAll()", StringComparison.Ordinal),
				"AddVisibility gates on light, so the ground is lit before the honest map is read");
			ClassicAssert.Less(
				part.IndexOf("HonestVisibility = (bool[])live.Clone()", StringComparison.Ordinal),
				part.IndexOf("ParentZone.VisAll()", StringComparison.Ordinal),
				"the snapshot is the honest map, so it is taken before the zone is opened");
			StringAssert.Contains(
				"ParentZone.AddVisibility(cell.X, cell.Y, The.Player.GetVisibilityRadius())", part);
			ClassicAssert.AreEqual(3,
				Regex.Matches(part, Regex.Escape("[NonSerialized]")).Count,
				"every scrap of projection state is frame state, never save state");
			ClassicAssert.AreEqual(1,
				Regex.Matches(part, Regex.Escape("XRLCore.RegisterAfterRenderCallback")).Count,
				"the engine offers no unregister, so exactly one callback is ever added");
			StringAssert.Contains("if (RestoreRegistered) return;", part);
			StringAssert.Contains("Array.Copy(honest, live, honest.Length)", part);
			StringAssert.Contains("zone.VisibilityMap", part);
			StringAssert.DoesNotContain("ExploreAll", part);
			StringAssert.DoesNotContain("ExploredMap", part);
			foreach (string level in new[] { "Darkvision 10", "Dimvision 15", "Interpolight 210",
				"Radar 228", "LitRadar 232", "Omniscient 255" })
				StringAssert.Contains(level, part,
					"every tier the Invisibility mutation reveals at is named, so the reader can "
						+ "check that 200 is none of them");
		}

		/// <summary>
		/// Two backstops, and they are deliberately different operations. At the head of the next
		/// frame the engine has already cleared the map, so an outstanding snapshot is DROPPED —
		/// writing it back would union two frames of sight. At end of turn nothing has cleared, so
		/// the honest map is written back, ahead of every gate the handler owns.
		/// </summary>
		[Test]
		public void AnOutstandingProjectionIsDroppedAtRenderAndRestoredBeforeAnyTurnBegins()
		{
			string part = Source(PartFile);
			ClassicAssert.Less(
				part.IndexOf("DiscardOutstandingProjection();", StringComparison.Ordinal),
				part.IndexOf("ParentZone.AddLight(LightLevel.Light)", StringComparison.Ordinal),
				"a stale projection is resolved before this frame's own is taken");
			StringAssert.Contains("if (honest == null || zone == null) return;", part);
			StringAssert.Contains("if (live == null || live.Length != honest.Length) return;", part);

			string events = Source(EventsFile);
			int restore = events.IndexOf(
				"XRL.World.ZoneParts.KingdomClaimedGroundLight.RestoreHonestVisibility()",
				StringComparison.Ordinal);
			int endTurn = events.IndexOf("public override bool HandleEvent(EndTurnEvent E)",
				StringComparison.Ordinal);
			int wake = events.IndexOf("KingdomMaster.ObserveAutomaticWake", StringComparison.Ordinal);
			ClassicAssert.Greater(restore, endTurn,
				"the backstop belongs to the end-of-turn dispatch");
			ClassicAssert.Less(restore, wake,
				"no turn may begin on an opened map, whatever the master option or ownership say");
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

			string sight = Regex.Match(options,
				"<option\\s+ID=\"" + SightOptionId + "\"[^>]+>").Value;
			StringAssert.Contains("Type=\"Checkbox\"", sight);
			StringAssert.Contains("Default=\"Yes\"", sight);
			StringAssert.Contains(SightOptionId, Source(Path.Combine("docs", "API.md")));
			StringAssert.Contains(SightOptionId, Source("PLAYTESTING.md"));
		}

		/// <summary>What neither file may ever do. <c>SetExplored</c> stays here even though city
		/// sight opens the visibility map: remembered floor is one-way and is owned by the
		/// projection's single activation-time reveal, so nothing on the render path writes it.
		/// <c>GetZone(</c> stays because no claim is ever thawed to be lit, and the two whole-map
		/// tiers stay because the light is lamplight, not second sight.</summary>
		private static readonly string[] ForbiddenEverywhere = new string[]
		{
			"GetZone(", "LightAll", "LightLevel.Omniscient", "SetExplored"
		};

		/// <summary>Additionally forbidden on the activation path. Opening the map is a property of
		/// the drawn frame alone; the part that attaches the light must never touch sight at all.
		/// </summary>
		private static readonly string[] Forbidden = new string[]
		{
			"GetZone(", "LightAll", "LightLevel.Omniscient", "SetExplored",
			"AddVisibility", "VisAll"
		};

		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}
	}
}
#endif
