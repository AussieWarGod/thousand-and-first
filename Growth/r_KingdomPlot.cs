using System;
using System.Collections.Generic;

using XRL;
using XRL.Messages;
using XRL.World;

using ThousandAndFirst;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently. Only the part moves; the
// settlement-side resolver below stays where the rest of the mod's code lives.
namespace XRL.World.Parts
{
	/// <summary>
	/// What one field carries: the crop the founder committed to it and the tick that crop next
	/// comes ripe. Every design in the food lane that GROWS anything wears this part &mdash; the
	/// kitchen garden, the garden rows, the field, the ploughed fields, the grange and the home
	/// farm &mdash; so the seed gate, the cycle and the rows are one mechanism rather than one
	/// per rung.
	/// <para>
	/// <b>Four fields and no more, deliberately.</b> Parts serialize by positional reflection, so
	/// appending to one is a save-compatibility hazard for every object that already carries it.
	/// Everything this wave needed to remember beyond these four &mdash; the sowing date, the
	/// rows, the seed, the cycle ordinal, the last want announced &mdash; lives in object int and
	/// string properties (<c>KingdomCrops</c>'s <c>*Property</c> constants), which are a
	/// dictionary the engine already serializes and which no layout depends on.
	/// </para>
	/// <para>
	/// <b>The cycle is a stamp, never a countdown.</b> Vanilla's own <c>Harvestable.RegenTimer</c>
	/// is dead code in every shipped blueprint precisely because its clock is turn-delivered and
	/// stops when the zone suspends. This part compares an absolute tick it stored, so missing
	/// ticks costs nothing and a season away resolves in one reckoning.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomPlot : IPart
	{
		public KingdomCropRules.PlotStage Stage;

		/// <summary>
		/// The tick this field's crop next comes ripe at. Meaningful only while
		/// <see cref="Stage"/> is Growing or Ripe; a Dormant field has no crop in it and no clock
		/// running.
		/// </summary>
		public long NextStageTick;

		/// <summary>
		/// What this field grows, decided by the seed that was committed to it and cached so it
		/// cannot silently change crop mid-cycle. Null on uncommitted ground.
		/// </summary>
		public string CropBlueprint;

		/// <summary>Set once a ripe field has already told the founder it has nowhere to put its
		/// harvest, so the wait is announced once rather than on every visit. Kept beside
		/// <c>KingdomCrops.SaidProperty</c> rather than folded into it because this one predates
		/// the property bag and is read by saves that already carry it.</summary>
		public bool NoLarderAnnounced;

		/// <summary>
		/// A crop also comes ripe while the founder is standing there watching it.
		/// <para>
		/// The settlement pass resolves absence, which is the hard half, but it only runs on zone
		/// activation &mdash; so a founder who sows a field and then stays put would see nothing
		/// happen for as long as they stayed. This is the cheap other half, and it has two due
		/// ticks rather than one: the crop comes RIPE on its own tick (a recolour and a line, no
		/// survey), and the settlement's hands GATHER it a day later
		/// (<c>KingdomCropRules.GatherDelayTicks</c>). The day between them is the founder's: a
		/// ripe row carries vanilla <c>Harvestable</c>, and whatever they gather themselves is not
		/// there for the settlement to count.
		/// </para>
		/// </summary>
		public override bool WantTurnTick()
		{
			return true;
		}

		public override void TurnTick(long TimeTick, int Amount)
		{
			KingdomSystem master = The.Game?.GetSystem<KingdomSystem>();
			if (!KingdomMaster.AutomaticWorkAllowed(master)) return;
			if (Stage == KingdomCropRules.PlotStage.Dormant)
			{
				return;
			}
			if (NextStageTick <= master.MasterOptionTick)
			{
				// A field remains planted/ripe, but disabled time never becomes free growth or
				// gathering. The next stage begins strictly in the future from this first wake.
				if (Stage == KingdomCropRules.PlotStage.Growing)
				{
					if (!KingdomMasterRules.TryFutureDeadline(TimeTick,
						KingdomCropRules.GrowTicks, out long future)) return;
					NextStageTick = future;
				}
				else NextStageTick = TimeTick;
				return;
			}
			if (Stage == KingdomCropRules.PlotStage.Growing)
			{
				if (TimeTick < NextStageTick)
				{
					return;
				}
				Zone growing = ParentObject?.CurrentZone;
				if (growing == null)
				{
					return;
				}
				KingdomCrops.SetRipe(KingdomCrops.RowsOf(growing, ParentObject), Ripe: true);
				ApplyStage(KingdomCropRules.PlotStage.Ripe);
				MessageQueue.AddPlayerMessage("{{G|The " + ParentObject.ShortDisplayName + " stands ripe.}}");
				return;
			}
			if (!KingdomCropRules.MayGather(NextStageTick, TimeTick))
			{
				return;
			}
			Zone zone = ParentObject?.CurrentZone;
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			if (zone == null || system == null || !system.Founded || !system.ClaimedZones.Contains(zone.ZoneID))
			{
				return;
			}
			// Surveying is the expensive part, so it happens only on the tick the settlement's own
			// hands are actually due in the field - once per cycle, not once per turn.
			KingdomSystem.Guard("plot tick", delegate
			{
				KingdomPlot.OnSettlementPass(system, zone, KingdomSurvey.Take(zone, system));
			});
		}

		/// <summary>
		/// Recolors the field for its new stage. Presentation only: the blueprint declares its own
		/// tile throughout and only the accent colors move &mdash; exactly the scheme vanilla
		/// <c>Harvestable</c> uses for its own ripe/unripe swap, borrowed here for the ground
		/// itself while the rows standing on it use the real part.
		/// </summary>
		public void ApplyStage(KingdomCropRules.PlotStage NewStage)
		{
			Stage = NewStage;
			XRL.World.Parts.Render render = ParentObject?.Render;
			if (render == null)
			{
				return;
			}
			switch (NewStage)
			{
			case KingdomCropRules.PlotStage.Growing:
				render.ColorString = "&g";
				render.DetailColor = "K";
				break;
			case KingdomCropRules.PlotStage.Ripe:
				render.ColorString = "&G";
				render.DetailColor = "g";
				break;
			default:
				render.ColorString = "&K";
				render.DetailColor = "K";
				break;
			}
		}

		/// <summary>
		/// Vanilla's own irrigation, answered on our clock.
		/// <para>
		/// <c>Hydraulic Irrigator</c> ships a <c>RadiusEventSender Event="AccelerateRipening"
		/// Radius="10" ChargeUse="5"</c>, and <c>Harvestable</c> answers that event by calling
		/// <c>Ripen()</c> &mdash; which returns immediately on every blueprint the game ships,
		/// because none of them arms <c>RegenTime</c>. The machine is real, powered, sited, and
		/// does nothing to any plant in Qud. It does something to a field of ours: each pulse
		/// pulls the stamp ten ticks earlier, so a crop standing inside a running irrigator comes
		/// ripe in half the time. Nothing else about the cycle changes &mdash; the pull is bounded
		/// at now, and it is the stamp that moves rather than the crop.
		/// </para>
		/// </summary>
		public override void Register(GameObject Object, IEventRegistrar Registrar)
		{
			Registrar.Register("AccelerateRipening");
			base.Register(Object, Registrar);
		}

		public override bool FireEvent(Event E)
		{
			if (E.ID == "AccelerateRipening" && Stage == KingdomCropRules.PlotStage.Growing && The.Game != null)
			{
				NextStageTick = KingdomCropRules.IrrigatedRipeTick(NextStageTick, The.Game.TimeTicks);
			}
			return base.FireEvent(E);
		}

		/// <summary>
		/// The founder's own two actions on a field: put seed in it, and take that seed back out.
		/// The withdrawal is the protection law made operable &mdash; a committed seed is a
		/// designation, and a designation the founder made is one only the founder unmakes.
		/// </summary>
		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != GetInventoryActionsEvent.ID)
			{
				return ID == InventoryActionEvent.ID;
			}
			return true;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			if (Stage != KingdomCropRules.PlotStage.Dormant)
			{
				E.AddAction("Withdraw Seed", "withdraw seed", "r_WithdrawSeed", null, 'w', FireOnActor: false, 5);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_WithdrawSeed" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomCrops.Withdraw(E.Actor, ParentObject);
			}
			return base.HandleEvent(E);
		}
	}
}
