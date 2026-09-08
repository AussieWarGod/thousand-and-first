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
	/// are doing. It is an eye, never a rule. The honest visibility map is taken before the zone is
	/// opened up and put back inside the same frame, so every predicate that reads
	/// <c>Cell.IsVisible()</c> &mdash; reify ordering, death witness, hostile perception, rest,
	/// autoexplore, targeting, Look &mdash; still runs on ordinary line of sight. Light stays at
	/// 200, which is none of the six tiers the Invisibility mutation reveals at (Darkvision 10,
	/// Dimvision 15, Interpolight 210, Radar 228, LitRadar 232, Omniscient 255), so invisible
	/// creatures stay invisible.
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
			// The engine cleared the visibility map immediately before this dispatch
			// (D/XRL/Core/XRLCore.cs:2505-2507), so a projection still outstanding here belongs to
			// a frame that is already over: the map it was taken from has been wiped, and writing
			// that stale snapshot back would union two frames of sight rather than restore one.
			// Drop it instead.
			DiscardOutstandingProjection();
			// Presentation only, and only where the founder actually is: a claimed zone the founder
			// is not standing in pays nothing at all. The option is read here as well as at the
			// activation that attaches the part, so switching it off goes dark on the next frame
			// instead of waiting for the visit that removes the part. The founder is checked for
			// existence first because this runs on the render dispatch: the engine sends
			// BeforeRenderEvent before it touches the player itself (D/XRL/Core/XRLCore.cs:2507),
			// and Zone.HasObject dereferences what it is handed (D/XRL/World/Zone.cs:3365-3368),
			// so a frame drawn with no player must be a frame this part does nothing on rather
			// than a null reference thrown out of the renderer.
			if (ParentZone != null
				&& ThousandAndFirst.KingdomClaimedGround.Enabled
				&& The.Player != null
				&& ParentZone.HasObject(The.Player))
			{
				ParentZone.AddLight(LightLevel.Light);
				ProjectCitySight();
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

		/// <summary>Open the claimed zone for the frame about to be drawn, and only for it.</summary>
		private void ProjectCitySight()
		{
			if (!CitySightEnabled) return;
			// The load-bearing line. GameManager.bDraw is the engine's debug render-step tracer
			// (public static int bDraw = 0 at D/GameManager.cs:270, reset at XRLCore.cs:3502, read
			// only by debug step gates), so ordinary play never reaches 11 and this return is a
			// hazard guard rather than a routine skip. On such a frame the engine abandons the draw
			// before it renders and before it runs the after-render callbacks
			// (D/XRL/Core/XRLCore.cs:2520-2522, ahead of Render at :2524 and the callback loop at
			// :2525), so a projection taken here would never be put back, and the between-frames
			// hostile check that rest and autoexplore lean on adds visibility WITHOUT clearing
			// first (D/XRL/World/GameObject.cs:11586-11588). A whole turn would then run on an
			// opened map: rest broken by a hostile three rooms away, autoexplore pathing into
			// unwalked interiors.
			if (GameManager.bDraw == 11) return;
			Cell cell = The.Player.CurrentCell;
			if (cell == null) return;
			// The map is read before the sweep, because Zone.AddVisibility dereferences it on its
			// first line (SetVisibility -> VisibilityMap[x + y * Width], D/XRL/World/Zone.cs:5086
			// and 4463-4470), so a guard placed after it would guard nothing.
			bool[] live = ParentZone.VisibilityMap;
			if (live == null) return;
			// Exactly the reckoning the engine is about to make for itself
			// (D/XRL/Core/XRLCore.cs:2511-2512), taken early so the honest answer can be kept.
			ParentZone.AddVisibility(cell.X, cell.Y, The.Player.GetVisibilityRadius());
			HonestVisibility = (bool[])live.Clone();
			ProjectedZone = ParentZone;
			ParentZone.VisAll();
			EnsureRestoreRegistered();
		}

		/// <summary>Put the honest map back. Called from the engine's own after-render pass, in the
		/// same frame the projection was taken, and again at end of turn as a backstop so no turn
		/// can begin on an opened map. Never touches the explored map: remembered floor is one-way,
		/// and the projection's activation-time reveal is what owns it.</summary>
		internal static void RestoreHonestVisibility()
		{
			bool[] honest = HonestVisibility;
			Zone zone = ProjectedZone;
			HonestVisibility = null;
			ProjectedZone = null;
			if (honest == null || zone == null) return;
			bool[] live = zone.VisibilityMap;
			if (live == null || live.Length != honest.Length) return;
			Array.Copy(honest, live, honest.Length);
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
		/// in; with nothing outstanding it is a no-op.</summary>
		private static void OnAfterRender(XRLCore Renderer, ScreenBuffer Buffer)
		{
			RestoreHonestVisibility();
		}
	}
}
