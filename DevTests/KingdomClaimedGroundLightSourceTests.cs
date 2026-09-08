#if TAF_TESTS
using System;
using System.Collections.Generic;
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
				"the engine's own AddVisibility gates on light, so pass 1 lights the ground before "
					+ "the frame's honest map is read at the draw");
			ClassicAssert.Less(
				part.IndexOf("HonestVisibility = (bool[])live.Clone()", StringComparison.Ordinal),
				part.IndexOf("ParentZone.VisAll()", StringComparison.Ordinal),
				"the snapshot is the honest map, so it is taken before the zone is opened");
			StringAssert.DoesNotContain("ParentZone.AddVisibility", part,
				"the engine already made the founder's reckoning at XRLCore.cs:2511-2512, before the "
					+ "draw this seat hangs off; repeating the same centre and radius cannot open one "
					+ "further cell and pays a whole-zone line-of-sight sweep on the render thread "
					+ "every frame. The snapshot only reads what the frame already decided");
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
		/// engine's second pass walks the handler list a pass-1 handler queued itself into, in
		/// the order they were queued. A zone part therefore cannot reach the END of that pass:
		/// Blackout is an object part, so it is always queued behind, and what it does there is
		/// remove the light Zone.AddVisibility gates distant cells on. The projection is taken at
		/// the engine's own draw instead — a flag armed by a prefix on XRLCore.RenderBaseToBuffer
		/// and spent by a prefix on Zone.Render, which the engine reaches only after the whole
		/// dispatch, its own second pass, the founder's reckoning and the wizard toggle have run —
		/// and it stands down entirely where a later engine decision would be overwritten. The
		/// render dispatch itself is deliberately NOT patched: re-hosting it crashed the game.
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
				"[HarmonyPatch(typeof(XRLCore), nameof(XRLCore.RenderBaseToBuffer))]", seam,
				"the frame is armed from the method that owns the whole draw");
			StringAssert.Contains(
				"[HarmonyPatch(typeof(Zone), nameof(Zone.Render), new Type[] { typeof(ScreenBuffer) })]",
				seam,
				"and spent on the one-argument overload RenderBaseToBuffer calls, never the "
					+ "sub-rectangle overload");
			StringAssert.Contains("private static void Prefix(XRLCore __instance)", seam);
			StringAssert.Contains("private static void Prefix(Zone __instance)", seam);
			StringAssert.DoesNotContain("private static bool Prefix", seam,
				"a prefix that can skip the engine's own draw is not a seam, it is a rewrite");
			StringAssert.Contains("if (!KingdomCitySightRenderSeam.SpendOn(__instance)) return;",
				seam,
				"the engine's other Zone.Render call sites arm nothing, so they project nothing");
			ClassicAssert.AreEqual(2, Regex.Matches(seam, Regex.Escape("ArmedZone = null;")).Count,
				"the arming is single shot: spent at the draw, and dropped by the draw scope");
			StringAssert.Contains("ProjectCitySight()", seam);
			StringAssert.Contains("Blackout", seam,
				"the seam names the native second-pass contributor it must come back behind");
			StringAssert.Contains("catch (Exception error)", seam,
				"a presentation projection may not carry a fault out of a prefix into the engine's "
					+ "own frame; every other Harmony body in this mod is wrapped the same way");
			foreach (string forbidden in ForbiddenEverywhere)
				StringAssert.DoesNotContain(forbidden, seam);

			StringAssert.Contains("core.VisAllToggle) return;", part);
			ClassicAssert.Less(part.IndexOf("core.VisAllToggle) return;", StringComparison.Ordinal),
				part.IndexOf("ParentZone.VisAll()", StringComparison.Ordinal),
				"the wizard toggle has already opened the map by the time the draw is reached, so "
					+ "a projection that would be closed back over it is never taken");
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
			StringAssert.Contains("KingdomCitySightRenderSeam.Disarm();", scope,
				"a frame the engine abandons before the draw must not leave the seam armed for "
					+ "whatever renders that zone next");
			ClassicAssert.Less(
				scope.IndexOf("KingdomCitySightRenderSeam.Disarm();", StringComparison.Ordinal),
				scope.IndexOf("KingdomClaimedGroundLight.RestoreHonestVisibility();",
					StringComparison.Ordinal),
				"the arming is dropped first, so even a restore that threw cannot leave it "
					+ "standing");
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
			// The one surface a player reads may not deny what docs/API.md discloses. Cell.Render
			// sets XRLCore.CludgeTargetRendered for a drawn sidebar target (D/XRL/World/Cell.cs:642),
			// and the engine's lost-sight drop (D/XRL/Core/XRLCore.cs:2533) gates on that flag before
			// it asks IsVisible(), so a creature already locked keeps its lock through a wall.
			ClassicAssert.IsFalse(Regex.IsMatch(sight, "targeting[^\"]*still use"),
				"the option text may not list targeting among what ordinary sight still governs");
			StringAssert.Contains("keeps its lock while it is drawn through a wall", sight,
				"and it must say what does happen instead");
			StringAssert.Contains(SightOptionId, Source(Path.Combine("docs", "API.md")));
			StringAssert.Contains(SightOptionId, Source("PLAYTESTING.md"));
		}

		/// <summary>
		/// The ordering, run rather than read. A Blackout stands in the zone: the engine walks its
		/// own second pass, where Blackout removes the light Zone.AddVisibility gates every cell
		/// further off than a neighbour on. Taken from inside that pass, the honest snapshot holds
		/// cells the Blackout was about to darken, and the subtractive restore — which never
		/// closes a cell the snapshot held open — leaves them visible into the next turn. Taken at
		/// the engine's own draw, the snapshot is exactly the sight an unprojected frame would
		/// have left. The seat is read out of the shipped source, so moving the projection back
		/// inside the dispatch fails here rather than passing quietly.
		/// </summary>
		[Test]
		public void TheHonestSnapshotSurvivesABlackoutStandingInTheZone()
		{
			bool queuedIntoTheSecondPass = Source(PartFile).Contains("E.AfterHandlers.Add");
			bool atTheDraw = Source(SeamFile).Contains(
				"[HarmonyPatch(typeof(Zone), nameof(Zone.Render), new Type[] { typeof(ScreenBuffer) })]");
			ClassicAssert.IsFalse(queuedIntoTheSecondPass,
				"the part must not queue itself ahead of Blackout");
			ClassicAssert.IsTrue(atTheDraw,
				"the projection is taken at the engine's own draw");

			bool[] honestFrame = RenderModelFrame(project: false, atTheDraw: true);
			CollectionAssert.AreEqual(honestFrame,
				RenderModelFrame(project: true, atTheDraw: atTheDraw),
				"city sight may show the frame whole and still leave behind exactly the sight the "
					+ "founder honestly had");
			CollectionAssert.AreNotEqual(honestFrame,
				RenderModelFrame(project: true, atTheDraw: false),
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
		/// draw at :2524, then this mod's after-render restore. Returns the visibility map the
		/// frame leaves behind, which is what the turn after it reads.</summary>
		private static bool[] RenderModelFrame(bool project, bool atTheDraw)
		{
			int[] light = new int[ModelWidth];
			bool[] visible = new bool[ModelWidth];
			bool[] honest = null;
			// Pass 1, zone parts ahead of objects: the claimed-ground light, LightLevel.Light.
			for (int i = 0; i < ModelWidth; i++)
				light[i] = 200;
			// A seat inside the dispatch has no engine reckoning behind it yet, so it has to make
			// one for itself &mdash; over pass-1 light, before Blackout has taken any away.
			if (project && !atTheDraw)
				honest = ModelProject(light, visible, reckonForItself: true);
			// Blackout's second-pass turn: RemoveLight to LightLevel.Blackout, which is 0 and so
			// below the > 1 AddVisibility asks for (D/XRL/World/Parts/Blackout.cs:58-65).
			for (int i = 0; i < ModelWidth; i++)
				if ((i - ModelBlackoutX) * (i - ModelBlackoutX)
					<= ModelBlackoutRadius * ModelBlackoutRadius && light[i] < 210)
					light[i] = 0;
			// The engine's own player reckoning, which it makes BEFORE the draw the seam sits on.
			ModelAddVisibility(light, visible);
			// The shipped seat: the reckoning above is the honest map, and the projection only
			// reads it (KingdomClaimedGroundLight.ProjectCitySight makes no AddVisibility call).
			if (project && atTheDraw)
				honest = ModelProject(light, visible, reckonForItself: false);
			if (honest != null)
				for (int i = 0; i < ModelWidth; i++)
					if (!honest[i])
						visible[i] = false;
			return visible;
		}

		/// <summary>The projection itself: the snapshot, then the zone opened whole
		/// (KingdomClaimedGroundLight.ProjectCitySight). <paramref name="reckonForItself"/> is the
		/// seat this mod moved away from: from inside the dispatch the engine's own reckoning has
		/// not happened yet, so a projection there has to compute one over pre-Blackout light, and
		/// the subtractive close then leaves the darkened cells open. The shipped seat passes
		/// false, because the honest map is already sitting there when it reads.</summary>
		private static bool[] ModelProject(int[] light, bool[] visible, bool reckonForItself)
		{
			if (reckonForItself) ModelAddVisibility(light, visible);
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

		/// <summary>
		/// The crashing patch target, swept where the law actually runs. The seat that killed the
		/// game was a Harmony patch on the render dispatch's static entry: Harmony re-hosted the
		/// method and the re-hosted copy threw NullReferenceException out of itself on the first
		/// drawn frame. "Neither the patch target nor that list may appear in a shipped source
		/// again" is a repo-wide law, so it is enforced repo-wide rather than over the three files
		/// this fixture happens to name. The scope is Tools/stage.sh's own EXCLUDE_DIRS, read out of
		/// the script, so one list governs both what ships and what is swept.
		/// </summary>
		[Test]
		public void TheCrashingPatchTargetAppearsInNoStagedSource()
		{
			List<string> staged = StagedSources();
			ClassicAssert.Greater(staged.Count, 100,
				"the staged tree is too small to be real; the sweep would pass vacuously");
			foreach (string path in staged)
			{
				string source = File.ReadAllText(path);
				StringAssert.DoesNotContain("BeforeRenderEvent.Send", source, path);
				StringAssert.DoesNotContain("AfterHandlers", source, path);
			}
		}

		/// <summary>Every C# source a cold install would carry: the repository tree minus the
		/// development-only trees Tools/stage.sh prunes, plus build output, which is not in the
		/// tree stage.sh walks either.</summary>
		private static List<string> StagedSources()
		{
			Match declared = Regex.Match(Source("Tools/stage.sh"),
				@"(?m)^EXCLUDE_DIRS=\(([^)]*)\)");
			ClassicAssert.IsTrue(declared.Success, "Tools/stage.sh no longer declares EXCLUDE_DIRS");
			HashSet<string> pruned = new HashSet<string>(declared.Groups[1].Value.Split(
				new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries),
				StringComparer.Ordinal);
			string root = TestMain.RepositoryRoot + Path.DirectorySeparatorChar;
			List<string> result = new List<string>();
			foreach (string path in Directory.EnumerateFiles(TestMain.RepositoryRoot, "*.cs",
				SearchOption.AllDirectories))
			{
				string relative = path.Substring(root.Length);
				string[] segments = relative.Split(Path.DirectorySeparatorChar);
				if (pruned.Contains(segments[0])) continue;
				bool built = false;
				foreach (string segment in segments)
					if (segment == "obj" || segment == "bin") built = true;
				if (built) continue;
				result.Add(path);
			}
			return result;
		}

		/// <summary>What neither file may ever do. <c>SetExplored</c> stays here even though city
		/// sight opens the visibility map: remembered floor is one-way and is owned by the
		/// projection's single activation-time reveal, so nothing on the render path writes it.
		/// <c>GetZone(</c> stays because no claim is ever thawed to be lit, and the two whole-map
		/// tiers stay because the light is lamplight, not second sight. The last two are the seat
		/// that crashed the game: patching the render dispatch's static entry made Harmony re-host
		/// it, and the re-hosted copy threw NullReferenceException out of itself on the first
		/// drawn frame in three of four unattended launches (Send_Patch1, the native dump naming
		/// the walk over its own second-pass handler list). Neither the patch target nor that list
		/// may appear in a shipped source again &mdash; a law this fixture enforces over every
		/// staged source in
		/// <see cref="TheCrashingPatchTargetAppearsInNoStagedSource"/>, not only over the three
		/// files named here.</summary>
		private static readonly string[] ForbiddenEverywhere = new string[]
		{
			"GetZone(", "LightAll", "LightLevel.Omniscient", "SetExplored",
			"BeforeRenderEvent.Send", "AfterHandlers"
		};

		/// <summary>Additionally forbidden on the activation path. Opening the map is a property of
		/// the drawn frame alone; the part that attaches the light must never touch sight at all.
		/// </summary>
		private static readonly string[] Forbidden = new string[]
		{
			"GetZone(", "LightAll", "LightLevel.Omniscient", "SetExplored",
			"BeforeRenderEvent.Send", "AfterHandlers", "AddVisibility", "VisAll"
		};

		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}
	}
}
#endif
