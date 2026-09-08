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
		private const string DrawScopeFile = "Growth/KingdomCitySightDrawScope.cs";
		private const string SeamFile = "Growth/KingdomCitySightRenderSeam.cs";
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
			StringAssert.Contains("if (!honest[i]) live[i] = false;", part);
			StringAssert.DoesNotContain("Array.Copy(honest", part,
				"a whole-map write puts back cells the frame legitimately changed after the "
					+ "snapshot; the close is subtractive");
			StringAssert.DoesNotContain("live[i] = true", part,
				"the close may only shut cells the projection opened, never open one");
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
		/// acting on it would be acting on a frame that is over. At end of turn nothing has
		/// cleared, so the projection is closed, ahead of every gate the handler owns.
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

		/// <summary>
		/// Zone parts are dispatched before the cells and the objects standing on them, and the
		/// engine's second pass walks the AfterHandlers a pass-1 handler queued itself into, in
		/// the order they were queued. A zone part therefore cannot reach the END of that pass:
		/// Blackout is an object part, so it is always queued behind, and what it does there is
		/// remove the light Zone.AddVisibility gates distant cells on. The projection is taken
		/// after the whole dispatch has returned instead, from a postfix on BeforeRenderEvent.Send
		/// — the part queues nothing at all — and it stands down entirely where a later engine
		/// decision would be overwritten.
		/// </summary>
		[Test]
		public void TheSnapshotIsTakenAfterEveryNativeVisibilityContributor()
		{
			string part = Source(PartFile);
			StringAssert.Contains("if (E.Pass == 1)", part);
			StringAssert.DoesNotContain("E.AfterHandlers.Add", part,
				"a zone part queued into AfterHandlers takes its turn ahead of Blackout, which "
					+ "hangs on an object and is queued behind every zone part");
			StringAssert.DoesNotContain("E.Pass == 2", part,
				"the projection no longer has a seat inside the dispatch");
			ClassicAssert.AreEqual(1,
				Regex.Matches(part, Regex.Escape("ProjectCitySight()")).Count,
				"one seat for the projection, and the render seam owns it");

			string seam = Source(SeamFile);
			StringAssert.Contains(
				"[HarmonyPatch(typeof(BeforeRenderEvent), nameof(BeforeRenderEvent.Send))]", seam);
			StringAssert.Contains("private static void Postfix(Zone Z)", seam);
			StringAssert.Contains("ProjectCitySight()", seam);
			StringAssert.DoesNotContain("Prefix", seam,
				"a prefix would run before the dispatch, which is worse than the seat it replaced");
			StringAssert.Contains("Blackout", seam,
				"the seam names the native second-pass contributor it must come back behind");
			foreach (string forbidden in ForbiddenEverywhere)
				StringAssert.DoesNotContain(forbidden, seam);

			StringAssert.Contains("core.VisAllToggle) return;", part);
			ClassicAssert.Less(part.IndexOf("core.VisAllToggle) return;", StringComparison.Ordinal),
				part.IndexOf("ParentZone.VisAll()", StringComparison.Ordinal),
				"the wizard toggle opens the map itself immediately after this dispatch, so a "
					+ "projection that would be closed back over it is never taken");
			StringAssert.Contains("if (HonestVisibility != null) return;", part);
			ClassicAssert.Less(
				part.IndexOf("if (HonestVisibility != null) return;", StringComparison.Ordinal),
				part.IndexOf("HonestVisibility = (bool[])live.Clone()", StringComparison.Ordinal),
				"a second projection in one frame would read the opened map as the honest one");
		}

		/// <summary>
		/// The close may not depend on the engine finishing the frame. XRLCore.RenderBaseToBuffer
		/// has no finally: it calls Zone.Render and then walks the after-render callbacks in a bare
		/// loop, so a throwing render — or any callback registered ahead of this mod's — would
		/// otherwise leave the zone open until the end-of-turn backstop. A Harmony finalizer is the
		/// finally the engine does not write, and it returns void so it never eats the exception
		/// the renderer was already raising.
		/// </summary>
		[Test]
		public void TheProjectionIsClosedEvenWhenARenderCallbackThrows()
		{
			string scope = Source(DrawScopeFile);
			StringAssert.Contains(
				"[HarmonyPatch(typeof(XRLCore), nameof(XRLCore.RenderBaseToBuffer))]", scope);
			StringAssert.Contains("private static void Finalizer()", scope);
			StringAssert.Contains(
				"KingdomClaimedGroundLight.RestoreHonestVisibility();", scope);
			StringAssert.DoesNotContain("Exception Finalizer", scope,
				"a finalizer that returns an Exception rewrites what the renderer threw");
			StringAssert.DoesNotContain("catch", scope,
				"the draw scope closes the projection; it never swallows a render failure");
			StringAssert.DoesNotContain("Prefix", scope);
			StringAssert.DoesNotContain("Postfix", scope,
				"a postfix does not run on a thrown frame, which is the whole hazard");
			foreach (string forbidden in ForbiddenEverywhere)
				StringAssert.DoesNotContain(forbidden, scope);

			string part = Source(PartFile);
			StringAssert.Contains("KingdomCitySightDrawScope", part,
				"the part names the scope that guarantees its close");
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

		/// <summary>
		/// The ordering, run rather than read. A Blackout stands in the zone: the engine walks its
		/// own second pass, where Blackout removes the light Zone.AddVisibility gates every cell
		/// further off than a neighbour on. Taken from inside that pass, the honest snapshot holds
		/// cells the Blackout was about to darken, and the subtractive restore — which never
		/// closes a cell the snapshot held open — leaves them visible into the next turn. Taken
		/// after the dispatch, the snapshot is exactly the sight an unprojected frame would have
		/// left. The seat is read out of the shipped source, so moving the projection back into
		/// AfterHandlers fails here rather than passing quietly.
		/// </summary>
		[Test]
		public void TheHonestSnapshotSurvivesABlackoutStandingInTheZone()
		{
			bool queuedIntoTheSecondPass = Source(PartFile).Contains("E.AfterHandlers.Add");
			bool afterDispatch = Source(SeamFile).Contains(
				"[HarmonyPatch(typeof(BeforeRenderEvent), nameof(BeforeRenderEvent.Send))]");
			ClassicAssert.IsFalse(queuedIntoTheSecondPass,
				"the part must not queue itself ahead of Blackout");
			ClassicAssert.IsTrue(afterDispatch, "the projection is taken after the dispatch");

			bool[] honestFrame = RenderModelFrame(project: false, afterDispatch: true);
			CollectionAssert.AreEqual(honestFrame,
				RenderModelFrame(project: true, afterDispatch: afterDispatch),
				"city sight may show the frame whole and still leave behind exactly the sight the "
					+ "founder honestly had");
			CollectionAssert.AreNotEqual(honestFrame,
				RenderModelFrame(project: true, afterDispatch: false),
				"the model has to catch the seat this moved away from, or it pins nothing");
		}

		private const int ModelWidth = 10;
		private const int ModelPlayerX = 0;
		private const int ModelPlayerRadius = 8;
		private const int ModelBlackoutX = 6;
		private const int ModelBlackoutRadius = 2;

		/// <summary>One drawn frame of the engine's own order, reduced to the row that matters:
		/// clear both maps (D/XRL/Core/XRLCore.cs:2505-2506), pass 1 (zone parts, then objects),
		/// the second pass in queue order, the engine's own player reckoning (:2511-2512), the
		/// draw, then this mod's after-render restore. Returns the visibility map the frame leaves
		/// behind, which is what the turn after it reads.</summary>
		private static bool[] RenderModelFrame(bool project, bool afterDispatch)
		{
			int[] light = new int[ModelWidth];
			bool[] visible = new bool[ModelWidth];
			bool[] honest = null;
			// Pass 1, zone parts ahead of objects: the claimed-ground light, LightLevel.Light.
			for (int i = 0; i < ModelWidth; i++)
				light[i] = 200;
			if (project && !afterDispatch)
				honest = ModelProject(light, visible);
			// Blackout's second-pass turn: RemoveLight to LightLevel.Blackout, which is 0 and so
			// below the > 1 AddVisibility asks for (D/XRL/World/Parts/Blackout.cs:58-65).
			for (int i = 0; i < ModelWidth; i++)
				if ((i - ModelBlackoutX) * (i - ModelBlackoutX)
					<= ModelBlackoutRadius * ModelBlackoutRadius && light[i] < 210)
					light[i] = 0;
			if (project && afterDispatch)
				honest = ModelProject(light, visible);
			ModelAddVisibility(light, visible);
			if (honest != null)
				for (int i = 0; i < ModelWidth; i++)
					if (!honest[i])
						visible[i] = false;
			return visible;
		}

		/// <summary>The projection itself: the founder's own reckoning, the snapshot, then the
		/// zone opened whole (KingdomClaimedGroundLight.ProjectCitySight).</summary>
		private static bool[] ModelProject(int[] light, bool[] visible)
		{
			ModelAddVisibility(light, visible);
			bool[] honest = (bool[])visible.Clone();
			for (int i = 0; i < ModelWidth; i++)
				visible[i] = true;
			return honest;
		}

		/// <summary>Zone.AddVisibility on one row (D/XRL/World/Zone.cs:5084-5100): the founder's
		/// own cell always, a neighbour always, and anything further only where the light map
		/// still reads above LightLevel.None. It only ever opens cells; it never closes one.
		/// </summary>
		private static void ModelAddVisibility(int[] light, bool[] visible)
		{
			visible[ModelPlayerX] = true;
			for (int i = 0; i < ModelWidth; i++)
			{
				int distance = (i - ModelPlayerX) * (i - ModelPlayerX);
				if (distance <= ModelPlayerRadius * ModelPlayerRadius && !visible[i]
					&& (distance <= 1 || light[i] > 1))
					visible[i] = true;
			}
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
