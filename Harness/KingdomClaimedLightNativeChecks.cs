using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;
using XRL.World.ZoneParts;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Machine witness for the claimed-ground light (<c>Growth/KingdomClaimedGroundLight*.cs</c>),
	/// standing in for the attended night walk this feature otherwise needs.
	/// <para>
	/// OBSERVATION ONLY. Nothing here calls <c>KingdomClaimedGround.ReconcileZone</c>,
	/// <c>RemoveZone</c>, <c>Zone.AddLight</c>, <c>LightAll</c> or <c>ExploreAll</c>. The claim is
	/// made by a real founding, the attachment by the engine's own <c>Zone.Activated()</c> - the
	/// same entry <c>Qud/API/JournalAPI.cs:82</c> uses - and the light by the real per-frame
	/// <c>BeforeRenderEvent</c> dispatch the runtime already performs while the persona's
	/// <c>yield-frames</c> hands the engine back its own render loop.
	/// </para>
	/// <para>
	/// WHAT THE LIGHT ASSERTION IS. <c>Zone.AddLight</c> with <c>LightLevel.Light</c> mixes every cell
	/// (<c>D/XRL/World/Zone.cs:5045</c>) and <c>MixLight</c> only ever raises
	/// (<c>D/XRL/World/Zone.cs:4927</c>), so the part's postcondition is "no cell of the seat's
	/// claimed zone reads below <c>LightLevel.Light</c>". That is what is asserted, through the
	/// engine's own <c>Zone.GetLight</c> (<c>D/XRL/World/Zone.cs:7252</c>) and
	/// <c>Cell.IsLit</c> (<c>D/XRL/World/Cell.cs:3485</c>). Ambient daylight can reach the same
	/// tier on its own (<c>D/XRL/World/Parts/Daylight.cs</c>), so coverage is asserted and the
	/// part's own contribution is REPORTED as the bracketed deficit its dispatch closed - never
	/// silently claimed.
	/// </para>
	/// </summary>
	internal static class KingdomClaimedLightNativeChecks
	{
		private const int BracketedFrames = 3;
		private static Frame Retained;

		internal static bool Vacant { get { return Retained == null; } }

		internal static string Run(string Verb, XRLGame Game, Zone Zone, out bool Complete)
		{
			Complete = false;
			if (Verb == KingdomClaimedLightNativeProvider.SetupVerb)
			{
				Require(Retained == null, "a claimed-light attempt is already retained");
				Retained = new Frame(Game, Zone);
				Retained.Start();
			}
			else
			{
				Require(Retained != null, "claimed-light setup is absent");
				Retained.Check();
			}
			Complete = Retained.Done;
			return (Complete ? "native-claimed-light cases=1 passed=1 failed=0"
				: "native-claimed-light phase=" + Retained.Phase)
				+ "; synthetic-founding=true; ordinary-acceptance=false; save-load=untested"
				+ Retained.Evidence;
		}

		internal static string Fail(Exception Error)
		{
			if (Retained != null) Retained.Armed = false;
			return "native-claimed-light cases=1 passed=0 failed=1; evidence retained: "
				+ KingdomScenarioRules.Bounded(Error.GetType().Name + ": " + Error.Message)
				+ " observer-fault=" + Retained?.Fault + Retained?.Evidence;
		}

		/// <summary>Void bracket around the part's real render dispatch. Never mutates the zone.</summary>
		internal static void Observe(bool Before, KingdomClaimedGroundLight Part)
		{
			Frame frame = Retained;
			if (frame == null || !frame.Armed) return;
			try { frame.Bracket(Before, Part); }
			catch (Exception error)
			{
				if (frame.Fault == null) frame.Fault = KingdomScenarioRules.Bounded(
					error.GetType().Name + ": " + error.Message);
			}
		}

		private static void Require(bool Value, string Failure)
		{
			KingdomClaimedLightNativeProvider.Require(Value, Failure);
		}

		private sealed class Frame
		{
			private readonly XRLGame Game;
			private readonly Zone Zone;
			private readonly string ZoneId;
			private KingdomSystem System;
			private KingdomClaimedGroundLight Attached;
			private string SettlementId;
			private int Brackets, Dispatches, Deficit, Foreign;
			internal bool Armed, Done;
			internal int Phase;
			internal string Fault;
			internal readonly StringBuilder Evidence = new StringBuilder();

			internal Frame(XRLGame Game, Zone Zone)
			{
				this.Game = Game;
				this.Zone = Zone;
				ZoneId = Zone.ZoneID;
			}

			/// <summary>Unclaimed control, real founding, real activation, exact attachment.</summary>
			internal void Start()
			{
				Require(Count() == 0, "the fresh marsh zone already carries a claimed-light part");
				Armed = true;
				// The negative control is this very zone while nobody claims it: the semantic
				// activation guard takes the RemoveZone branch, so a real activation must leave
				// no part behind and the part must never be dispatched.
				Zone.Activated();
				Require(Fault == null, "unclaimed activation faulted the observer: " + Fault);
				Require(Count() == 0 && Dispatches == 0,
					"an unclaimed activation attached or dispatched the claimed-ground light");
				System = KingdomNativeCampFounding.Found(Game, Zone, Require);
				Require(System.ClaimedZones != null && System.ClaimedZones.Contains(ZoneId),
					"the real founding did not claim the zone it stands on");
				SettlementId = System.SettlementIdForOwnedZone(ZoneId);
				Require(!string.IsNullOrEmpty(SettlementId),
					"the claimed zone answers to no single settlement");
				Zone.Activated();
				Require(Fault == null, "claimed activation faulted the observer: " + Fault);
				Attached = Zone.GetPart<KingdomClaimedGroundLight>();
				Require(Count() == 1 && Attached != null,
					"a claimed activation did not attach exactly one claimed-ground light");
				Require(Attached.Version == 1 && string.Equals(Attached.SettlementId, SettlementId,
					StringComparison.Ordinal), "the attached light carries the wrong stamp");
				Foreign = ForeignParts();
				Require(Foreign == 0,
					"a live zone outside ClaimedZones already carries the claimed-ground light");
				Phase = 1;
				Evidence.Append("\nunclaimed-control=no-part; claimed zone=").Append(ZoneId)
					.Append(" settlement=").Append(SettlementId)
					.Append("; cells=").Append(Zone.Width * Zone.Height);
			}

			/// <summary>After real rendered frames: coverage, cardinality, and the negative sweep.</summary>
			internal void Check()
			{
				Require(Phase == 1 && !Done, "claimed-light check is not repeatable");
				Require(Fault == null, "the render observer faulted: " + Fault);
				Require(!KingdomScenarioAdvance.Pending, "turns are still owed");
				Require(!KingdomScenarioFrames.Pending, "frames are still owed");
				Require(Brackets > 0,
					"the claimed-ground light never ran its real BeforeRenderEvent dispatch");
				Require(Dispatches == Brackets,
					"a claimed-ground light other than the attached one was dispatched");
				Require(ReferenceEquals(Zone.GetPart<KingdomClaimedGroundLight>(), Attached)
					&& Count() == 1, "the attached claimed-ground light was replaced or duplicated");
				Require(string.Equals(Attached.SettlementId, SettlementId, StringComparison.Ordinal),
					"the attached light's settlement stamp moved");
				int dark = 0;
				for (int y = 0; y < Zone.Height; y++)
					for (int x = 0; x < Zone.Width; x++)
					{
						if ((int)Zone.GetLight(x, y) < (int)LightLevel.Light) dark++;
						else if (!Zone.GetCell(x, y).IsLit()) dark++;
					}
				Require(dark == 0, "the claimed zone reports " + dark
					+ " cell(s) below LightLevel.Light after a real rendered frame");
				Require(ForeignParts() == 0,
					"a live zone outside ClaimedZones acquired the claimed-ground light");
				Armed = false;
				Done = true;
				Phase = 2;
				Evidence.Append("\nengine-frames=").Append(KingdomScenarioFrames.Observations)
					.Append("; render-dispatches=").Append(Brackets)
					.Append("; cells-the-part-raised-at-its-own-dispatch=").Append(Deficit)
					.Append("; every-cell-at-or-above-Light=true; foreign-claimed-light-parts=0");
			}

			/// <summary>Records the deficit the part's own dispatch closed on the first frames.</summary>
			internal void Bracket(bool Before, KingdomClaimedGroundLight Part)
			{
				if (!Before)
				{
					Dispatches++;
					if (Attached != null && ReferenceEquals(Part, Attached)) Brackets++;
					return;
				}
				if (Attached == null || !ReferenceEquals(Part, Attached)) return;
				if (Brackets >= BracketedFrames) return;
				int below = 0;
				for (int y = 0; y < Zone.Height; y++)
					for (int x = 0; x < Zone.Width; x++)
						if ((int)Zone.GetLight(x, y) < (int)LightLevel.Light) below++;
				if (below > Deficit) Deficit = below;
			}

			private int Count()
			{
				List<IZonePart> parts = Zone.Parts;
				int found = 0;
				if (parts != null)
					for (int i = 0; i < parts.Count; i++)
						if (parts[i] is KingdomClaimedGroundLight) found++;
				return found;
			}

			/// <summary>Live zones only. No claim is thawed merely to look at it.</summary>
			private int ForeignParts()
			{
				int found = 0;
				ZoneManager manager = The.ZoneManager;
				if (manager?.CachedZones == null) return 0;
				foreach (KeyValuePair<string, Zone> row in manager.CachedZones)
				{
					if (row.Value == null || (System?.ClaimedZones?.Contains(row.Key) ?? false))
						continue;
					if (row.Value.GetPart<KingdomClaimedGroundLight>() != null) found++;
				}
				return found;
			}
		}
	}
}
