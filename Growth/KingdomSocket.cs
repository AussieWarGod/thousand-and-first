using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using ThousandAndFirst;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name. This part is
// only ever added in code (see r_KingdomImprovement's own header for why), but it lives here
// anyway alongside every other part this mod ships: a part whose namespace depends on how it
// happened to be attached is a trap waiting for the first blueprint that names it.
namespace XRL.World.Parts
{
	/// <summary>
	/// A cleared plot: everything the settlement raised on this ground has come down, and the
	/// rect, its lane, and the ground itself stand ready for whatever the founder chooses next.
	/// See BUILDING-CATALOGUE-BRIEF.md's 2026-08-21 addendum, "the plot as socket".
	/// <para>
	/// Carries no geometry of its own. The rect a later stake needs rides on the same
	/// <c>KingdomPlots.PlotX1Property</c> family every laid plot already carries &mdash;
	/// <c>KingdomSocket</c> stamps them with <c>KingdomPlots.StampRect</c> the moment this part is
	/// attached &mdash; so <c>KingdomPlots.ReadPlots</c>, the lane rule, and the road budget all
	/// count a socket exactly as they count a standing plot, with no change to any of the three.
	/// <see cref="LastDesignKey"/> is purely descriptive: nothing anywhere reads it to decide
	/// anything, only to tell the founder what stood here.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomSocket : IPart
	{
		/// <summary>Registry key of the design that last stood here, if it is still known.
		/// Null when nothing was ever recorded, or when the design has since left the catalogue.</summary>
		public string LastDesignKey;

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetShortDescriptionEvent.ID;
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.Append("\n{{rules|This ground stands cleared, staked out, and ready to be built on again.}}");
			return base.HandleEvent(E);
		}
	}
}

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>
	/// The engine-coupled half of the plot as a socket: leaving a re-buildable slot behind a
	/// strike, changing what a standing plot is as one strike-and-rebuild ceremony, building fresh
	/// on ground a strike already cleared, and re-dressing a standing building in any registered
	/// skin. Every founder-facing entry point here does its own eligibility check and its own
	/// messaging, surfacing only a decline through <c>Failure</c>, matching the
	/// <c>KingdomMaterials</c>/<c>KingdomAdopt</c> idiom throughout this mod. Every decision that
	/// does not need a real object or a real cell &mdash; the (type, size) classification, the
	/// footprint check, the combined cost, every refusal's wording &mdash; is delegated to the
	/// engine-free <see cref="KingdomSocketRules"/>.
	/// <para>
	/// This never builds a second demolition or a second construction pipeline. Striking still
	/// runs through <see cref="KingdomMaterials.OrderStrike"/> unmodified, on its own crew-days
	/// schedule with its own salvage math; raising a design onto a rect still runs through
	/// <see cref="KingdomPlots.Stake"/> unmodified, staged exactly as an ordinary commission
	/// stages. The only things this file adds are: the one combined figure disclosed before either
	/// of those is asked to do anything (<see cref="KingdomSocketRules.DescribeConversion"/>); the
	/// hook a strike's own completion calls so a plot's ground survives it (<see cref="OnCleared"/>,
	/// wired into <c>KingdomMaterials.WorkStrike</c>); and the ordinary re-stake of ground a strike
	/// already vacated, onto the exact rect it vacated, bypassing the fresh-ground search a
	/// first-time commission runs because there is nothing left here to search for.
	/// </para>
	/// </summary>
	public static class KingdomSocket
	{
		/// <summary>Blueprint the socket marker stands as. Inherits vanilla <c>Sign</c> the same
		/// way every other inert stake in this mod does (<c>r_KingdomClearanceStake</c>,
		/// <c>r_KingdomNotice</c>) and carries only this file's own part &mdash; never
		/// <c>r_KingdomClearance</c>, which would make <c>KingdomMaterials.OnSettlementPass</c>
		/// mistake a socket for a live clearance order. See MODDING.md / ObjectBlueprints.xml for
		/// the declaration; this file only ever creates it, never defines it.</summary>
		public const string SocketBlueprint = "r_KingdomSocket";

		/// <summary>Registry key of the design a condemned building is being converted into,
		/// staged on the building itself the moment the founder orders the ceremony and read back
		/// by <see cref="OnCleared"/> once the crew finishes taking it down &mdash; which may be
		/// days, or a save and reload, later. A property rather than a part field for the reason
		/// every other staged choice in this mod (skins, plans) is one: STANDARDS.md &sect;1
		/// forbids appending a serialized field to a part that already ships, and the building
		/// this rides on ships today with no opinion about being converted.</summary>
		public const string PendingConvertKeyProperty = "KingdomConvertKey";

		/// <summary>The skin key chosen alongside <see cref="PendingConvertKeyProperty"/>, or
		/// absent for the new design's own unmodified look.</summary>
		public const string PendingConvertSkinProperty = "KingdomConvertSkin";

		// ==================================================================================
		// Convert: "change what this plot is" as one ceremony
		// ==================================================================================

		/// <summary>What one <see cref="Validate"/> pass resolved, so <see cref="AssessConvert"/>
		/// and <see cref="ExecuteConvert"/> never have to re-derive it separately and can never
		/// disagree about which design, which spec, or which rect they are talking about.</summary>
		private struct ConvertContext
		{
			public KingdomRules.BuildEntry OldEntry;
			public KingdomPlotRules.PlotSpec OldSpec;
			public KingdomRules.BuildEntry NewEntry;
			public KingdomPlotRules.PlotSpec NewSpec;
			public KingdomPlotRules.PlotRect Rect;
		}

		/// <summary>
		/// Every eligibility check a conversion has to pass, run in the order a founder should
		/// read the refusals in: whose ground it is, whether the settlement actually raised it,
		/// whether it is free to be touched at all, then what the new design itself asks for.
		/// Read-only &mdash; spends nothing, strikes nothing, stakes nothing.
		/// </summary>
		private static bool Validate(KingdomSystem System, Zone Z, GameObject Building, string NewKey, out ConvertContext Context, out string Failure)
		{
			Context = default(ConvertContext);
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "A plot is changed on the kingdom's own ground, not in other people's streets.";
				return false;
			}
			if (Building == null || !GameObject.Validate(Building) || Building.CurrentZone == null || Building.CurrentZone.ZoneID != Z.ZoneID)
			{
				Failure = "There is nothing there to change.";
				return false;
			}
			if (Building.GetIntProperty("KingdomBuilt") != 1)
			{
				Failure = "The settlement converts what it raised. That is not one of its buildings.";
				return false;
			}
			if (Building.GetIntProperty(KingdomAdopt.AdoptedProperty) == 1)
			{
				Failure = KingdomSocketRules.RefuseAdopted(Building.ShortDisplayName);
				return false;
			}
			if (Building.GetIntProperty(KingdomMaterials.StrikeEffortProperty) > 0)
			{
				Failure = KingdomSocketRules.RefuseCondemned(Building.ShortDisplayName);
				return false;
			}
			r_KingdomImprovement improvement = Building.GetPart<r_KingdomImprovement>();
			if (improvement != null && (improvement.Working || improvement.Held))
			{
				Failure = KingdomSocketRules.RefuseImproving(Building.ShortDisplayName);
				return false;
			}
			string oldKey = Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
			if (!KingdomData.TryGetBuilding(oldKey, out KingdomRules.BuildEntry oldEntry) || !KingdomPlots.TryGetSpec(oldKey, out KingdomPlotRules.PlotSpec oldSpec))
			{
				Failure = KingdomSocketRules.RefuseNotAPlot(Building.ShortDisplayName);
				return false;
			}
			if (!KingdomPlots.TryReadRect(Building, out KingdomPlotRules.PlotRect rect))
			{
				Failure = KingdomSocketRules.RefuseNotAPlot(Building.ShortDisplayName);
				return false;
			}
			if (oldKey == NewKey)
			{
				Failure = KingdomSocketRules.RefuseAlreadyThat(Building.ShortDisplayName);
				return false;
			}
			if (!KingdomData.TryGetBuilding(NewKey, out KingdomRules.BuildEntry newEntry))
			{
				Failure = "No such design.";
				return false;
			}
			if (!KingdomPlots.TryGetSpec(NewKey, out KingdomPlotRules.PlotSpec newSpec))
			{
				Failure = KingdomSocketRules.RefuseNotAPlot(newEntry.Name);
				return false;
			}
			if (!KingdomRules.StyleAllows(newEntry.Styles, System.Style))
			{
				Failure = "The " + newEntry.Name + " is not built in this city's own style.";
				return false;
			}
			if (!KingdomPlotRules.Allows(System.Stage, newSpec.Size))
			{
				Failure = KingdomPlotRules.RefuseStage(newSpec.Size, System.SeatName, System.Stage);
				return false;
			}
			if (!KingdomPlotRules.TryDimensions(newSpec.Size, out int needWidth, out int needHeight))
			{
				Failure = "No such design.";
				return false;
			}
			if (!KingdomSocketRules.FootprintFits(rect.Width, rect.Height, needWidth, needHeight))
			{
				Failure = KingdomSocketRules.RefuseTooSmall(newEntry.Name, rect.Width, rect.Height, needWidth, needHeight);
				return false;
			}
			if (KingdomPlotRules.IsUnderground(Z.Z) && newSpec.RequiresSky)
			{
				Failure = KingdomPlotRules.RefuseSky(newEntry.Name);
				return false;
			}
			if (!KingdomZoning.Permits(System, Z.ZoneID, newEntry, out string zoningFailure))
			{
				Failure = zoningFailure;
				return false;
			}
			Context.OldEntry = oldEntry;
			Context.OldSpec = oldSpec;
			Context.NewEntry = newEntry;
			Context.NewSpec = newSpec;
			Context.Rect = rect;
			return true;
		}

		/// <summary>
		/// Reads what converting <paramref name="Building"/> into <paramref name="NewKey"/> would
		/// take: whether it is even allowed, which of Addendum 2's two verbs it is, and the one
		/// combined figure <see cref="KingdomSocketRules.DescribeConversion"/> composes from it.
		/// Spends nothing. Safe to call for a confirmation popup, and called again by
		/// <see cref="ExecuteConvert"/> itself before anything actually moves, because nothing
		/// here trusts that the world held still between the two calls.
		/// </summary>
		public static bool AssessConvert(KingdomSystem System, Zone Z, GameObject Building, string NewKey, out KingdomSocketRules.ChangeKind Kind, out KingdomSocketRules.ConversionQuote Quote, out string Failure)
		{
			Kind = default(KingdomSocketRules.ChangeKind);
			Quote = default(KingdomSocketRules.ConversionQuote);
			if (!Validate(System, Z, Building, NewKey, out ConvertContext context, out Failure))
			{
				return false;
			}
			Kind = KingdomSocketRules.ClassifyChange(context.OldEntry.Category, context.OldSpec.Size, context.NewEntry.Category, context.NewSpec.Size);
			Quote = KingdomSocketRules.AssessConversion(
				KingdomMaterials.CostFor(context.OldEntry.Key), context.OldEntry.CostDrams,
				KingdomMaterials.CostFor(context.NewEntry.Key), context.NewEntry.CostDrams);
			return true;
		}

		/// <summary>
		/// Orders the ceremony: pays the new design's full water and material cost right now,
		/// exactly as an ordinary commission would (<see cref="KingdomMaterials.CanPay"/> /
		/// <c>Pay</c>, <c>KingdomGrowth.ConsumeStoredWater</c>, unmodified), then hands the strike
		/// itself to <see cref="KingdomMaterials.OrderStrike"/>, also unmodified. Nothing is built
		/// yet &mdash; the crew has to take the old work down first, on its own ordinary schedule
		/// &mdash; but the whole price is spent and disclosed before any of that begins, which is
		/// what "before anything moves" means here. <see cref="OnCleared"/> is what actually
		/// raises the new design, once the strike finishes.
		/// </summary>
		public static bool ExecuteConvert(KingdomSystem System, Zone Z, GameObject Building, string NewKey, string NewSkinKey, out string Failure)
		{
			if (!Validate(System, Z, Building, NewKey, out ConvertContext context, out Failure))
			{
				return false;
			}
			if (KingdomGrowth.CountStoredWater(Z) < context.NewEntry.CostDrams)
			{
				Failure = "The work would cost {{C|" + context.NewEntry.CostDrams + " drams}} from the stores, and the stores cannot bear it.";
				return false;
			}
			if (!KingdomMaterials.CanPay(Z, context.NewEntry.Key, out string materialFailure))
			{
				Failure = materialFailure;
				return false;
			}
			if (!KingdomMaterials.Pay(Z, context.NewEntry.Key))
			{
				Failure = "The stockpiles could not cover the work after all.";
				return false;
			}
			KingdomGrowth.ConsumeStoredWater(Z, context.NewEntry.CostDrams);
			if (!KingdomMaterials.OrderStrike(System, Z, Building, out string strikeFailure))
			{
				// Should not happen: every precondition OrderStrike itself checks (founded, this
				// ground, a valid KingdomBuilt object) was already true above. If the world
				// changed under us anyway, there is no refund path here either, for the same
				// reason KingdomCommission has none once a scaffold has actually been paid for.
				Failure = strikeFailure ?? "The crew could not be set to it.";
				return false;
			}
			Building.SetStringProperty(PendingConvertKeyProperty, context.NewEntry.Key);
			if (!string.IsNullOrEmpty(NewSkinKey))
			{
				Building.SetStringProperty(PendingConvertSkinProperty, NewSkinKey);
			}
			KingdomSocketRules.ChangeKind kind = KingdomSocketRules.ClassifyChange(context.OldEntry.Category, context.OldSpec.Size, context.NewEntry.Category, context.NewSpec.Size);
			string verb = KingdomSocketRules.VerbFor(kind);
			string name = Building.ShortDisplayName;
			KingdomChronicle.Record(System, "the founder ordered the " + name + " of " + System.KingdomDisplayName + " " + verb + " into " + XRL.Language.Grammar.A(context.NewEntry.Name));
			System.Ledger.Note("{{G|The " + name + " is to become " + XRL.Language.Grammar.A(context.NewEntry.Name) + ". The crew is set to strike it.}}");
			MessageQueue.AddPlayerMessage("{{G|The " + name + " is ordered " + verb + " into " + XRL.Language.Grammar.A(context.NewEntry.Name) + ".}}");
			KingdomLog.Log("socket: convert ordered " + context.OldEntry.Key + " -> " + context.NewEntry.Key + " at " + System.SeatName);
			return true;
		}

		// ==================================================================================
		// The strike's own completion: leave a socket, or finish a conversion
		// ==================================================================================

		/// <summary>
		/// Called from <c>KingdomMaterials.WorkStrike</c> the instant a strike finishes, while
		/// <paramref name="Building"/> still stands and still carries its own stamped rect &mdash;
		/// see MODDING.md / the wiring note in <c>KingdomMaterials.cs</c> for exactly where.
		/// Does nothing, and returns false, for a building that never stood on a plot at all: every
		/// single-cell design in this mod is untouched by any of this.
		/// <para>
		/// For a plot design, this always sweeps the plot's own walls, floor, door, and
		/// furnishings off the rect (everything <c>KingdomPlots.Furnish</c> and
		/// <c>KingdomPlots.Apply</c> stamped with this same plot's own id) before doing anything
		/// else, because a struck plot whose shell is left standing is not a re-buildable slot; it
		/// is dead ground the survey will refuse forever. Then:
		/// </para>
		/// <list type="bullet">
		/// <item>if <see cref="ExecuteConvert"/> staged a conversion here, the new design is staked
		/// directly onto the same rect (<see cref="KingdomPlots.Stake"/>, bypassing the fresh-site
		/// search &mdash; there is nothing left on this ground to search around) and one combined
		/// line is chronicled for the whole affair; the caller is told to suppress its own ordinary
		/// "struck" messaging in this case (return value <c>true</c>);</item>
		/// <item>otherwise, or if the restake could not land (a design withdrawn mid-strike, a torn
		/// down zone), the rect is left a plain <see cref="r_KingdomSocket"/> marker and the caller
		/// proceeds exactly as an ordinary strike always has (return value <c>false</c>).</item>
		/// </list>
		/// </summary>
		/// <returns>True when this call fully told the conversion's story and the caller's own
		/// "struck" chronicle/message should not also fire; false for every ordinary strike,
		/// where the caller's own messaging still applies unchanged.</returns>
		public static bool OnCleared(KingdomSystem System, Zone Z, GameObject Building)
		{
			if (System == null || Z == null || Building == null || !GameObject.Validate(Building))
			{
				return false;
			}
			if (!KingdomPlots.TryReadRect(Building, out KingdomPlotRules.PlotRect rect))
			{
				return false;
			}
			string oldName = Building.ShortDisplayName;
			string oldKey = Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
			string plotId = Building.GetStringProperty(KingdomPlots.PlotIdProperty);
			SweepPlotParts(Z, rect, plotId);
			string pendingKey = Building.GetStringProperty(PendingConvertKeyProperty);
			if (!string.IsNullOrEmpty(pendingKey)
				&& KingdomData.TryGetBuilding(pendingKey, out KingdomRules.BuildEntry newEntry)
				&& KingdomPlots.TryGetSpec(pendingKey, out KingdomPlotRules.PlotSpec newSpec))
			{
				string skinKey = Building.GetStringProperty(PendingConvertSkinProperty);
				GameObject works = RestakeOnRect(System, Z, rect, newEntry, newSpec, skinKey);
				if (works != null)
				{
					KingdomChronicle.Record(System, "the " + oldName + " of " + System.KingdomDisplayName + " came down, and " + XRL.Language.Grammar.A(newEntry.Name) + " rose on its ground");
					MessageQueue.AddPlayerMessage("{{W|The " + oldName + " comes down,}} {{G|and the ground is already rising into " + XRL.Language.Grammar.A(newEntry.Name) + ".}}");
					KingdomLog.Log("socket: converted " + (oldKey ?? "?") + " -> " + pendingKey + " on rect " + rect.X1 + "," + rect.Y1 + " to " + rect.X2 + "," + rect.Y2);
					return true;
				}
				KingdomLog.Log("socket: convert of " + (oldKey ?? "?") + " into " + pendingKey + " could not restake; leaving the ground a socket instead");
			}
			LeaveSocket(Z, rect, oldKey);
			return false;
		}

		/// <summary>Removes every object this plot raised over its own rect &mdash; walls, floor,
		/// door, contents &mdash; leaving bare cells. Scoped to <paramref name="PlotId"/> so a
		/// neighbouring plot's own lane, which never overlaps this rect by construction (
		/// <c>KingdomPlotRules.CrowdsExisting</c>), is never touched even if IDs collide across
		/// zones somehow. Only ever objects the settlement itself created and marked
		/// (<c>KingdomPlots.PlotPartProperty</c>) &mdash; the protection law's own exemption,
		/// exercised here the same way striking already exercises it on the building itself.</summary>
		private static void SweepPlotParts(Zone Z, KingdomPlotRules.PlotRect Rect, string PlotId)
		{
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
			{
				for (int x = Rect.X1; x <= Rect.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null)
					{
						continue;
					}
					List<GameObject> standing = new List<GameObject>(cell.GetObjects());
					for (int i = 0; i < standing.Count; i++)
					{
						GameObject item = standing[i];
						if (item == null || !GameObject.Validate(item) || item.GetIntProperty(KingdomPlots.PlotPartProperty) != 1)
						{
							continue;
						}
						if (!string.IsNullOrEmpty(PlotId) && item.GetStringProperty(KingdomPlots.PlotIdProperty) != PlotId)
						{
							continue;
						}
						item.Obliterate();
					}
				}
			}
		}

		/// <summary>Leaves a socket marker at the rect's centre, stamped with the rect itself so
		/// every later siting pass counts it exactly as it counts a standing plot.</summary>
		private static void LeaveSocket(Zone Z, KingdomPlotRules.PlotRect Rect, string OldKey)
		{
			Cell cell = Z.GetCell(Rect.CenterX, Rect.CenterY);
			if (cell == null)
			{
				return;
			}
			GameObject marker = GameObject.Create(SocketBlueprint);
			if (marker == null)
			{
				return;
			}
			r_KingdomSocket part = marker.GetPart<r_KingdomSocket>();
			if (part == null)
			{
				marker.Obliterate();
				return;
			}
			part.LastDesignKey = OldKey;
			KingdomPlots.StampRect(marker, Rect);
			cell.AddObject(marker);
		}

		/// <summary>Stakes a design directly onto an already-vacated rect, bypassing the fresh
		/// ground search a first-time commission runs (<c>KingdomPlots.TryFindRect</c>): there is
		/// nothing left on this ground to search around, and keeping the rect is the whole point.
		/// Reuses <c>KingdomPlots.Stake</c> unmodified &mdash; the same function an ordinary
		/// commission calls once it has chosen its own ground &mdash; so door orientation, wall
		/// material, raise time, and every stage of the raising are decided exactly as they always
		/// are for any other plot.</summary>
		private static GameObject RestakeOnRect(KingdomSystem System, Zone Z, KingdomPlotRules.PlotRect Rect, KingdomRules.BuildEntry Entry, KingdomPlotRules.PlotSpec Spec, string SkinKey)
		{
			KingdomPlots.GroundGrid grid = new KingdomPlots.GroundGrid(Z);
			bool carved = KingdomPlotRules.IsUnderground(Z.Z);
			return KingdomPlots.Stake(System, Z, Rect, Entry, Spec, grid, SkinKey, carved);
		}

		// ==================================================================================
		// Building fresh on ground a strike already cleared
		// ==================================================================================

		/// <summary>
		/// Stakes a design on ground a strike already left a socket on. Runs every check an
		/// ordinary commission runs &mdash; style, stage, footprint, sky, zoning, water, material
		/// &mdash; minus the two that make no sense for ground the settlement already claimed
		/// (the plan cap and the road budget: this is not new plotted area, it is the same rect
		/// coming back into use) and pays the design's own full cost, with no strike effort added
		/// on top &mdash; nothing stands here to take down.
		/// </summary>
		public static bool BuildOnSocket(KingdomSystem System, Zone Z, GameObject Marker, string Key, string SkinKey, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "A plot is raised on the kingdom's own ground, not in other people's streets.";
				return false;
			}
			if (Marker == null || !GameObject.Validate(Marker) || Marker.CurrentZone == null || Marker.CurrentZone.ZoneID != Z.ZoneID || Marker.GetPart<r_KingdomSocket>() == null)
			{
				Failure = "There is no cleared plot there to build on.";
				return false;
			}
			if (!KingdomPlots.TryReadRect(Marker, out KingdomPlotRules.PlotRect rect))
			{
				Failure = "That ground cannot be read.";
				return false;
			}
			if (!KingdomData.TryGetBuilding(Key, out KingdomRules.BuildEntry entry))
			{
				Failure = "No such design.";
				return false;
			}
			if (!KingdomPlots.TryGetSpec(Key, out KingdomPlotRules.PlotSpec spec))
			{
				Failure = KingdomSocketRules.RefuseNotAPlot(entry.Name);
				return false;
			}
			if (!KingdomRules.StyleAllows(entry.Styles, System.Style))
			{
				Failure = "The " + entry.Name + " is not built in this city's own style.";
				return false;
			}
			if (!KingdomPlotRules.Allows(System.Stage, spec.Size))
			{
				Failure = KingdomPlotRules.RefuseStage(spec.Size, System.SeatName, System.Stage);
				return false;
			}
			if (!KingdomPlotRules.TryDimensions(spec.Size, out int needWidth, out int needHeight))
			{
				Failure = "No such design.";
				return false;
			}
			if (!KingdomSocketRules.FootprintFits(rect.Width, rect.Height, needWidth, needHeight))
			{
				Failure = KingdomSocketRules.RefuseTooSmall(entry.Name, rect.Width, rect.Height, needWidth, needHeight);
				return false;
			}
			if (KingdomPlotRules.IsUnderground(Z.Z) && spec.RequiresSky)
			{
				Failure = KingdomPlotRules.RefuseSky(entry.Name);
				return false;
			}
			if (!KingdomZoning.Permits(System, Z.ZoneID, entry, out string zoningFailure))
			{
				Failure = zoningFailure;
				return false;
			}
			if (KingdomGrowth.CountStoredWater(Z) < entry.CostDrams)
			{
				Failure = "The work would cost {{C|" + entry.CostDrams + " drams}} from the stores, and the stores cannot bear it.";
				return false;
			}
			if (!KingdomMaterials.CanPay(Z, entry.Key, out string materialFailure))
			{
				Failure = materialFailure;
				return false;
			}
			if (!KingdomMaterials.Pay(Z, entry.Key))
			{
				Failure = "The stockpiles could not cover the work after all.";
				return false;
			}
			KingdomGrowth.ConsumeStoredWater(Z, entry.CostDrams);
			string lastKey = Marker.GetPart<r_KingdomSocket>()?.LastDesignKey;
			Marker.Obliterate();
			GameObject works = RestakeOnRect(System, Z, rect, entry, spec, SkinKey);
			if (works == null)
			{
				Failure = "The ground could not be staked.";
				return false;
			}
			KingdomChronicle.Record(System, "the cleared ground at " + System.KingdomDisplayName + " was staked again for " + XRL.Language.Grammar.A(entry.Name));
			MessageQueue.AddPlayerMessage("{{G|The cleared plot is staked for " + XRL.Language.Grammar.A(entry.Name) + ".}}");
			KingdomLog.Log("socket: built " + entry.Key + " on former " + (lastKey ?? "?") + " socket at " + rect.X1 + "," + rect.Y1);
			return true;
		}

		// ==================================================================================
		// Re-dress: any registered skin, on any standing building, trivially
		// ==================================================================================

		/// <summary>
		/// Applies a registered skin to a standing building. Reads the building's own design
		/// LIVE from the current catalogue rather than from anything cached at the moment it was
		/// raised, which is what lets a skin a mod added after the building went up be offered
		/// here (Addendum 1: "including one a mod added later"). Structural: never changes what
		/// the building is, what it costs to run, or what it produces &mdash; only
		/// <c>Render</c>, through <c>KingdomDesign.ApplyRenderOverrides</c>, unmodified.
		/// </summary>
		public static bool Redress(KingdomSystem System, Zone Z, GameObject Building, string SkinKey, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "A building is re-dressed on the kingdom's own ground, not in other people's streets.";
				return false;
			}
			if (Building == null || !GameObject.Validate(Building) || Building.CurrentZone == null || Building.CurrentZone.ZoneID != Z.ZoneID)
			{
				Failure = "There is nothing there to re-dress.";
				return false;
			}
			if (Building.GetIntProperty("KingdomBuilt") != 1)
			{
				Failure = "The settlement re-dresses what it stands behind. That is not one of its buildings.";
				return false;
			}
			string key = Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
			if (string.IsNullOrEmpty(key))
			{
				key = Building.GetStringProperty(KingdomAdopt.AdoptedKeyProperty);
			}
			if (!KingdomData.TryGetBuilding(key, out KingdomRules.BuildEntry entry))
			{
				Failure = KingdomSocketRules.RefuseUnknownDesign(Building.ShortDisplayName);
				return false;
			}
			if (string.IsNullOrEmpty(SkinKey))
			{
				Failure = "Choose a look to re-dress it in.";
				return false;
			}
			KingdomDesignRules.SkinEntry skin = KingdomDesignRules.FindSkin(entry.Skins, SkinKey);
			if (skin == null)
			{
				Failure = KingdomSocketRules.RefuseUnknownSkin(SkinKey, Building.ShortDisplayName);
				return false;
			}
			KingdomMaterialTally cost = KingdomSocketRules.RedressCost(KingdomMaterials.CostFor(entry.Key));
			if (!cost.IsEmpty())
			{
				KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Z);
				if (!stock.Spend(cost))
				{
					string missing = KingdomMaterialRules.Missing(stock.Tally, cost).Describe();
					Failure = "Re-dressing the " + Building.ShortDisplayName + " wants {{C|" + cost.Describe() + "}}, and the stockpiles are short "
						+ ((missing == null) ? "of it" : ("{{C|" + missing + "}}")) + ".";
					return false;
				}
			}
			KingdomDesign.ApplyRenderOverrides(Building, skin.ColorString, skin.DetailColor, skin.RenderString, skin.Tile);
			KingdomChronicle.Record(System, "the " + Building.ShortDisplayName + " at " + System.KingdomDisplayName + " was given a new coat, dressed as " + SkinKey);
			MessageQueue.AddPlayerMessage("{{G|The " + Building.ShortDisplayName + " is re-dressed.}}");
			KingdomLog.Log("socket: redress " + Building.ShortDisplayName + " (" + entry.Key + ") as " + SkinKey);
			return true;
		}

		// ==================================================================================
		// Charter entry points
		// ==================================================================================

		private static void CollectNearby(Cell Anchor, List<GameObject> Into, Func<GameObject, bool> Predicate)
		{
			if (Anchor == null)
			{
				return;
			}
			foreach (GameObject item in Anchor.GetObjects())
			{
				if (Predicate(item) && !Into.Contains(item))
				{
					Into.Add(item);
				}
			}
		}

		/// <summary>
		/// The Charter's "change what a plot is" action. Standing beside a work the settlement
		/// raised offers a conversion; standing beside a cleared socket offers to build fresh on
		/// it. Both list only designs a plot may actually be raised as, and a live conversion's
		/// list is annotated with Addendum 2's own verb (<c>change</c>/<c>re-type</c>) for each
		/// choice before the founder commits to one.
		/// </summary>
		public static void OpenConvert(KingdomSystem System, GameObject Founder)
		{
			if (System == null || Founder == null)
			{
				return;
			}
			Zone zone = Founder.CurrentZone;
			Cell cell = Founder.CurrentCell;
			if (zone == null || cell == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("A plot is changed on the kingdom's own ground.");
				return;
			}
			List<GameObject> buildings = new List<GameObject>();
			List<GameObject> sockets = new List<GameObject>();
			Func<GameObject, bool> isBuilding = o => o.GetIntProperty("KingdomBuilt") == 1 && KingdomPlots.TryReadRect(o, out _);
			Func<GameObject, bool> isSocket = o => o.GetPart<r_KingdomSocket>() != null;
			CollectNearby(cell, buildings, isBuilding);
			CollectNearby(cell, sockets, isSocket);
			foreach (Cell adjacent in cell.GetLocalAdjacentCells())
			{
				CollectNearby(adjacent, buildings, isBuilding);
				CollectNearby(adjacent, sockets, isSocket);
			}
			if (buildings.Count == 0 && sockets.Count == 0)
			{
				Popup.Show("Stand beside a plot " + System.SeatName + " raised, or ground it cleared, to change what stands there.");
				return;
			}
			List<string> options = new List<string>();
			List<GameObject> targets = new List<GameObject>();
			for (int i = 0; i < buildings.Count; i++)
			{
				options.Add(buildings[i].ShortDisplayName);
				targets.Add(buildings[i]);
			}
			int socketsStart = targets.Count;
			for (int i = 0; i < sockets.Count; i++)
			{
				options.Add("{{K|a cleared plot}}");
				targets.Add(sockets[i]);
			}
			int picked = Popup.PickOption(Title: "Change what a plot is, at " + System.SeatName, Options: options.ToArray(), AllowEscape: true);
			if (picked < 0)
			{
				return;
			}
			GameObject target = targets[picked];
			bool onSocket = picked >= socketsStart;
			string currentCategory = null;
			KingdomPlotRules.PlotSize currentSize = KingdomPlotRules.PlotSize.None;
			if (!onSocket)
			{
				string oldKey = target.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
				if (KingdomData.TryGetBuilding(oldKey, out KingdomRules.BuildEntry oldEntry) && KingdomPlots.TryGetSpec(oldKey, out KingdomPlotRules.PlotSpec oldSpec))
				{
					currentCategory = oldEntry.Category;
					currentSize = oldSpec.Size;
				}
			}
			List<KingdomRules.BuildEntry> available = new List<KingdomRules.BuildEntry>();
			foreach (KingdomRules.BuildEntry entry in KingdomData.Buildings)
			{
				if (!KingdomPlots.TryGetSpec(entry.Key, out KingdomPlotRules.PlotSpec spec) || spec.Size == KingdomPlotRules.PlotSize.None)
				{
					continue;
				}
				if (!KingdomRules.StyleAllows(entry.Styles, System.Style) || System.Stage < entry.MinStage)
				{
					continue;
				}
				available.Add(entry);
			}
			if (available.Count == 0)
			{
				Popup.Show("No plot design is known here.");
				return;
			}
			string[] designOptions = new string[available.Count];
			for (int i = 0; i < available.Count; i++)
			{
				string tag = "";
				if (!onSocket && KingdomPlots.TryGetSpec(available[i].Key, out KingdomPlotRules.PlotSpec size))
				{
					tag = " {{C|[" + KingdomSocketRules.VerbFor(KingdomSocketRules.ClassifyChange(currentCategory, currentSize, available[i].Category, size.Size)) + "]}}";
				}
				designOptions[i] = available[i].DisplayName + " {{C|[" + available[i].CostDrams + " drams]}}" + tag;
			}
			int designPicked = Popup.PickOption(Title: onSocket ? "Build on the cleared plot" : ("Change the " + target.ShortDisplayName + " into"), Options: designOptions, AllowEscape: true);
			if (designPicked < 0)
			{
				return;
			}
			KingdomRules.BuildEntry chosen = available[designPicked];
			string skinKey = KingdomDesign.ChooseSkin(chosen, System.Style)?.Key;
			if (onSocket)
			{
				if (!BuildOnSocket(System, zone, target, chosen.Key, skinKey, out string socketFailure))
				{
					Popup.Show(socketFailure);
				}
				return;
			}
			if (!AssessConvert(System, zone, target, chosen.Key, out KingdomSocketRules.ChangeKind kind, out KingdomSocketRules.ConversionQuote quote, out string assessFailure))
			{
				Popup.Show(assessFailure);
				return;
			}
			string question = KingdomSocketRules.DescribeConversion(target.ShortDisplayName, chosen.Name, kind, quote) + "\n\nOrder it?";
			if (Popup.ShowYesNo(question) != DialogResult.Yes)
			{
				return;
			}
			if (!ExecuteConvert(System, zone, target, chosen.Key, skinKey, out string executeFailure))
			{
				Popup.Show(executeFailure);
			}
		}

		/// <summary>The Charter's "give a building a new look" action.</summary>
		public static void OpenRedress(KingdomSystem System, GameObject Founder)
		{
			if (System == null || Founder == null)
			{
				return;
			}
			Zone zone = Founder.CurrentZone;
			Cell cell = Founder.CurrentCell;
			if (zone == null || cell == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("A building is re-dressed on the kingdom's own ground.");
				return;
			}
			List<GameObject> candidates = new List<GameObject>();
			Func<GameObject, bool> isBuilding = o => o.GetIntProperty("KingdomBuilt") == 1;
			CollectNearby(cell, candidates, isBuilding);
			foreach (Cell adjacent in cell.GetLocalAdjacentCells())
			{
				CollectNearby(adjacent, candidates, isBuilding);
			}
			if (candidates.Count == 0)
			{
				Popup.Show("Stand beside something " + System.SeatName + " stands behind to give it a new look.");
				return;
			}
			string[] options = new string[candidates.Count];
			for (int i = 0; i < candidates.Count; i++)
			{
				options[i] = candidates[i].ShortDisplayName;
			}
			int picked = Popup.PickOption(Title: "Give a building a new look, at " + System.SeatName, Options: options, AllowEscape: true);
			if (picked < 0)
			{
				return;
			}
			GameObject target = candidates[picked];
			string key = target.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
			if (string.IsNullOrEmpty(key))
			{
				key = target.GetStringProperty(KingdomAdopt.AdoptedKeyProperty);
			}
			if (!KingdomData.TryGetBuilding(key, out KingdomRules.BuildEntry entry) || entry.Skins == null || entry.Skins.Count == 0)
			{
				Popup.Show("There is no look known for the " + target.ShortDisplayName + " besides its own.");
				return;
			}
			string[] skinOptions = new string[entry.Skins.Count];
			for (int i = 0; i < entry.Skins.Count; i++)
			{
				skinOptions[i] = KingdomDesignRules.DescribeSkinOption(entry.Skins[i], false);
			}
			int skinPicked = Popup.PickOption(Title: "Dress the " + target.ShortDisplayName + " as", Options: skinOptions, AllowEscape: true);
			if (skinPicked < 0)
			{
				return;
			}
			if (!Redress(System, zone, target, entry.Skins[skinPicked].Key, out string failure))
			{
				Popup.Show(failure);
			}
		}
	}
}
