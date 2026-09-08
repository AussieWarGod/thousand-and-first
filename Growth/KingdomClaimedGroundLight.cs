using System;
using ConsoleLib.Console;
using XRL.Core;
using XRL.UI;
using XRL.World;

namespace XRL.World.ZoneParts
{
	/// <summary>
	/// The founder's own ground, read at a glance. While the founder stands in a claimed zone this
	/// raises the whole zone to <see cref="LightLevel.Light"/> once per rendered frame &mdash; the
	/// same tier a torch answers with, so nothing hidden is revealed. Deliberately not the
	/// omniscient tier: the engine treats that one as psychic sight
	/// (D/XRL/World/Parts/Mutation/Invisibility.cs:65), and a lit city is lamplight, not second
	/// sight. Mod-owned rather than a vanilla part, so a save loaded without this mod is dark
	/// ground again instead of ground somebody else's part lights forever.
	///
	/// City sight rides on top of that, under its own checkbox: for the drawn frame only, the
	/// claimed zone is shown whole, so citizens behind their own walls are drawn doing what they
	/// are doing. It is an eye, never a rule. The honest visibility map is read immediately before
	/// the engine draws the zone, from
	/// <see cref="ThousandAndFirst.KingdomCitySightRenderSeam"/> &mdash; a flag armed at the head
	/// of the drawn frame and spent on the engine's own <c>Zone.Render</c> call
	/// (D/XRL/Core/XRLCore.cs:2524), which the engine reaches only once the whole render dispatch,
	/// its own second pass, the founder's visibility reckoning and the wizard whole-map toggle
	/// have all run (:2507-2518). The restore only ever closes cells the projection
	/// itself opened, so every predicate that reads <c>Cell.IsVisible()</c> &mdash; reify
	/// ordering, death witness, hostile perception, rest, autoexplore, targeting, Look &mdash;
	/// still runs on ordinary line of sight, and no sight another hand granted or took away is
	/// written over. Light stays at 200, which is none of the six tiers the Invisibility mutation
	/// reveals at (Darkvision 10, Dimvision 15, Interpolight 210, Radar 228, LitRadar 232,
	/// Omniscient 255), so invisible creatures stay invisible.
	/// </summary>
	[Serializable]
	public sealed class KingdomClaimedGroundLight : IZonePart
	{
		/// <summary>The city-sight checkbox. Separate from the claimed-ground light so the ground
		/// can stay lit while the sight through walls is switched off.</summary>
		public const string CitySightOptionId = "r_TAF_OptionCitySight";

		public int Version = 1;
		public string SettlementId = "";

		/// <summary>The honest visibility map for the frame being drawn, taken before the zone is
		/// opened up. Never serialized and never persisted: it is one frame of presentation state,
		/// and a save that carried it would be a save that remembers a projection.</summary>
		[NonSerialized]
		private static bool[] HonestVisibility;

		/// <summary>The exact zone <see cref="HonestVisibility"/> was taken from, so the restore
		/// can never write one zone's map into another's.</summary>
		[NonSerialized]
		private static Zone ProjectedZone;

		/// <summary>The engine keeps after-render callbacks forever and offers no unregister
		/// (D/XRL/Core/XRLCore.cs:652-655), so exactly one is ever added.</summary>
		[NonSerialized]
		private static bool RestoreRegistered;

		/// <summary>Read per frame, like the light's own gate: switching city sight off closes the
		/// walls again on the next frame rather than on the next visit.</summary>
		public static bool CitySightEnabled
		{
			get { return Options.GetOption(CitySightOptionId, "Yes") != "No"; }
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == BeforeRenderEvent.ID;
		}

		public override bool HandleEvent(BeforeRenderEvent E)
		{
			if (E.Pass == 1)
			{
				// The engine cleared the visibility map immediately before this dispatch
				// (D/XRL/Core/XRLCore.cs:2505-2507), so a projection still outstanding here belongs
				// to a frame that is already over: the map it was taken from has been wiped, and
				// writing that stale snapshot back would union two frames of sight rather than
				// restore one. Drop it instead.
				DiscardOutstandingProjection();
				// Presentation only, and only where the founder actually is: a claimed zone the
				// founder is not standing in pays nothing at all. The option is read here as well
				// as at the activation that attaches the part, so switching it off goes dark on the
				// next frame instead of waiting for the visit that removes the part. The founder is
				// checked for existence first because this runs on the render dispatch: the engine
				// sends BeforeRenderEvent before it touches the player itself
				// (D/XRL/Core/XRLCore.cs:2507), and Zone.HasObject dereferences what it is handed
				// (D/XRL/World/Zone.cs:3365-3368), so a frame drawn with no player must be a frame
				// this part does nothing on rather than a null reference thrown out of the
				// renderer.
				if (ParentZone != null
					&& ThousandAndFirst.KingdomClaimedGround.Enabled
					&& The.Player != null
					&& ParentZone.HasObject(The.Player))
					ParentZone.AddLight(LightLevel.Light);
			}
			return base.HandleEvent(E);
		}

		public override void Write(Zone Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomClaimedGroundLight));
		}

		public override void Read(Zone Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomClaimedGroundLight));
			SettlementId = SettlementId ?? "";
		}

		/// <summary>Open the claimed zone for the frame about to be drawn, and only for it. Reached
		/// from <see cref="ThousandAndFirst.KingdomCitySightRenderSeam"/> on the engine's own
		/// <c>Zone.Render</c> call, so the map read here is the one every native contributor
		/// &mdash; light and visibility alike &mdash; has already finished writing. The gates the
		/// pass-1 light stands on are asked again here rather than inherited: this is a fresh
		/// entry from outside the dispatch, and a checkbox or a founder that changed since is a
		/// projection that must not be taken.</summary>
		internal void ProjectCitySight()
		{
			if (!ThousandAndFirst.KingdomClaimedGround.Enabled) return;
			if (!CitySightEnabled) return;
			// One projection per frame, whatever the dispatch does. A second would read the already
			// opened map as the honest one and leave the zone open for good.
			if (HonestVisibility != null) return;
			// GameManager.bDraw is the engine's debug render-step tracer (public static int
			// bDraw = 0 at D/GameManager.cs:270, reset at XRLCore.cs:3502, read only by debug step
			// gates), so ordinary play never reaches 11. On such a frame the engine abandons the
			// draw before it renders and before it runs the after-render callbacks
			// (D/XRL/Core/XRLCore.cs:2520-2522, ahead of Render at :2524 and the callback loop at
			// :2525), so a projection taken there would never be put back, and the between-frames
			// hostile check that rest and autoexplore lean on adds visibility WITHOUT clearing
			// first (D/XRL/World/GameObject.cs:11586-11588). A whole turn would then run on an
			// opened map: rest broken by a hostile three rooms away, autoexplore pathing into
			// unwalked interiors. The seam this is reached from now sits at :2524, BEHIND that
			// abandon, so the engine takes the decision first; the guard stays because the seam's
			// flag can also be spent by a nested draw of the armed zone, which the engine's own
			// return never reached.
			if (GameManager.bDraw == 11) return;
			// Wizard whole-map sight opens the zone for itself at D/XRL/Core/XRLCore.cs:2514-2518,
			// which the engine reaches BEFORE the Render this projection hangs off (:2524). The
			// map is already open and already the wizard's decision by the time this runs: a
			// snapshot taken over it would read all-true and close nothing, but standing aside
			// says so plainly and keeps the projection out of a sight it did not grant.
			XRLCore core = XRLCore.Core;
			if (core != null && core.VisAllToggle) return;
			if (ParentZone == null || The.Player == null) return;
			// Only where the founder actually is, and asked the same O(1) way as the light asks it
			// (Zone.HasObject is Object.CurrentZone == this, D/XRL/World/Zone.cs:3365-3368).
			if (!ParentZone.HasObject(The.Player)) return;
			Cell cell = The.Player.CurrentCell;
			if (cell == null) return;
			// The map is read before the sweep, because Zone.AddVisibility dereferences it on its
			// first line (SetVisibility -> VisibilityMap[x + y * Width], D/XRL/World/Zone.cs:5086
			// and 4463-4470), so a guard placed after it would guard nothing.
			bool[] live = ParentZone.VisibilityMap;
			if (live == null) return;
			// The founder's own reckoning, repeated. The engine makes it for itself at
			// D/XRL/Core/XRLCore.cs:2511-2512, which it reaches before the Render this projection
			// hangs off (:2524), so on that path this adds nothing new: AddVisibility only ever
			// OPENS cells and never closes one (D/XRL/World/Zone.cs:5084-5100), and the same
			// centre and radius twice is the same set. It is kept because the snapshot must not
			// depend on which branch reached the draw &mdash; the world-map branch
			// (D/XRL/Core/XRLCore.cs:2469-2479) draws with no such reckoning at all &mdash; and
			// because it is the line that states what the honest map is. It reads the light map on
			// its way: a cell further off than a neighbour is only opened where
			// GetLight(i, j) > 1, which is why this runs at the draw and not inside the dispatch.
			// Blackout REMOVES light from the engine's own second pass
			// (D/XRL/World/Parts/Blackout.cs:47-67), and it hangs on an object, so it is queued
			// behind every zone part (D/XRL/World/Zone.cs:7632-7677): a snapshot taken from a zone
			// part's turn in that pass would answer with light a Blackout was about to take away,
			// and the subtractive restore would keep those cells open into the turn that follows.
			ParentZone.AddVisibility(cell.X, cell.Y, The.Player.GetVisibilityRadius());
			HonestVisibility = (bool[])live.Clone();
			ProjectedZone = ParentZone;
			ParentZone.VisAll();
			EnsureRestoreRegistered();
		}

		/// <summary>Close what the projection opened. Called from the draw scope that wraps the
		/// render (<see cref="ThousandAndFirst.KingdomCitySightDrawScope"/>) so no thrown frame can
		/// leave the zone open, from the engine's own after-render pass so the honest map is back
		/// before anything later in the same frame reads it, and again at end of turn as a backstop
		/// so no turn can begin on an opened map. It is subtractive on purpose: a cell honest sight
		/// already held is left exactly as the frame left it, so blindness or sight another hand
		/// granted after the snapshot survives and the restore can never hand out sight of its own.
		/// Never touches the explored map: remembered floor is one-way, and the projection's
		/// activation-time reveal is what owns it.</summary>
		internal static void RestoreHonestVisibility()
		{
			bool[] honest = HonestVisibility;
			Zone zone = ProjectedZone;
			HonestVisibility = null;
			ProjectedZone = null;
			if (honest == null || zone == null) return;
			bool[] live = zone.VisibilityMap;
			if (live == null || live.Length != honest.Length) return;
			for (int i = 0; i < honest.Length; i++)
				if (!honest[i]) live[i] = false;
		}

		/// <summary>A projection whose frame is already over. The map it belonged to has been
		/// cleared, so the snapshot is dropped rather than written anywhere.</summary>
		private static void DiscardOutstandingProjection()
		{
			HonestVisibility = null;
			ProjectedZone = null;
		}

		private static void EnsureRestoreRegistered()
		{
			if (RestoreRegistered) return;
			RestoreRegistered = true;
			XRLCore.RegisterAfterRenderCallback(OnAfterRender);
		}

		/// <summary>Runs for every frame the engine draws, including frames this part took no part
		/// in; with nothing outstanding it is a no-op. This is the ordinary seat, not the
		/// guaranteed one: the engine's callback loop has no finally and stops at the first
		/// callback that throws (D/XRL/Core/XRLCore.cs:2524-2528), which is what
		/// <see cref="ThousandAndFirst.KingdomCitySightDrawScope"/> exists to cover.</summary>
		private static void OnAfterRender(XRLCore Renderer, ScreenBuffer Buffer)
		{
			RestoreHonestVisibility();
		}
	}
}
