using System;
using XRL;
using XRL.World;
using ThousandAndFirst;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X". A part named
// in XML MUST live in this namespace or the object is built without it, silently.
namespace XRL.World.Parts
{
	/// <summary>
	/// A bench somebody thinks at: the work that charges elapsed world-time against the one subject
	/// the city took up.
	/// <para>
	/// <b>It is <c>r_KingdomScaffold</c>, for an idea, and that is not an analogy.</b> The same
	/// machinery raises a building: a stretch of elapsed time buys labour at the pace the crew can
	/// actually manage, idle time is SPENT and never banked, and a bench nobody stands at produces
	/// nothing however long the founder is away (Addendum 8 clause 2). The three differences are
	/// that this one holds a node key rather than a target blueprint, that its pace is multiplied by
	/// how far the city's best mind clears the subject's tier, and that on completion it mints
	/// roster keys instead of raising a structure.
	/// </para>
	/// <para>
	/// <b>Extra benches are throughput, never lanes.</b> Every bench in the city charges the same
	/// subject, each from its own stamp, so a second scriptorium makes the one subject go faster and
	/// there is still nothing to schedule (RR2).
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomInquiry : IPart
	{
		private const string SetSubjectCommand = "r_SetKingdomResearchSubject";

		/// <summary>What this bench is worth to the pace: a shelf and a copyist at 100, a room built
		/// to think in at 150, the ancients' own bench understood at 200. Authored as an XML
		/// parameter on the blueprint; a bench that declares none is a scriptorium.</summary>
		public int Rung = KingdomResearchRules.ScriptoriumPercent;

		/// <summary>Tick this bench last charged, or 0 before its first look. Its own, so two
		/// benches in one city each charge their own stretch.</summary>
		public long LastWorkedTick;

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
			if (KingdomResearch.Enabled)
			{
				E.AddAction("Study", "set the city's research subject", SetSubjectCommand,
					null, 's', FireOnActor: false, 5);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == SetSubjectCommand && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
				if (!KingdomMaster.NewWorkAllowed(system))
				{
					XRL.UI.Popup.Show("Settlement simulation is paused; the keepers cannot take up a new subject yet.");
					return true;
				}
				KingdomSystem.Guard("research bench subject", delegate
				{
					KingdomResearch.OpenBench(ParentObject, E.Actor);
				});
				return true;
			}
			return base.HandleEvent(E);
		}

		/// <summary>The staffing pass's own crew-only stamp on a work, 0-100. Read and never
		/// written, exactly as every other consumer reads it.</summary>
		private const string CrewStretchProperty = "KingdomEffectiveness";

		public override bool WantTurnTick()
		{
			return true;
		}

		public override void TurnTick(long TimeTick, int Amount)
		{
			KingdomSystem master = The.Game?.GetSystem<KingdomSystem>();
			if (!KingdomMaster.AutomaticWorkAllowed(master))
			{
				base.TurnTick(TimeTick, Amount);
				return;
			}
			if (LastWorkedTick <= master.MasterOptionTick)
			{
				LastWorkedTick = TimeTick;
				base.TurnTick(TimeTick, Amount);
				return;
			}
			// One long compare per turn, and no more: research is charged on the settlement's own
			// day, never on the heartbeat and never per turn. The first look banks the stamp and
			// charges nothing, exactly as a scaffold's does.
			if (LastWorkedTick <= 0L)
			{
				LastWorkedTick = TimeTick;
			}
			else if (TimeTick - LastWorkedTick >= KingdomRules.TicksPerDay)
			{
				Think(TimeTick);
			}
			base.TurnTick(TimeTick, Amount);
		}

		private void Think(long TimeTick)
		{
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			Zone zone = ParentObject?.CurrentZone;
			if (system == null || !system.Founded || zone == null || system.ClaimedZones == null
				|| !system.ClaimedZones.Contains(zone.ZoneID))
			{
				// Not this realm's ground, or no realm at all. Nothing is charged and nothing is
				// said: a bench standing somewhere that is not ours is not a stalled bench.
				LastWorkedTick = TimeTick;
				return;
			}
			// The two factors are read APART rather than through KingdomWear.EffectivenessOf, which
			// already multiplies them: the 7b sentence has to be able to say whether the bench is
			// empty or merely in a bad state, and one number cannot. Multiplied back together in
			// KingdomResearchRules.InquiryRate, they are exactly what every other work runs at.
			int staffNeeded = ParentObject.GetIntProperty(KingdomAdopt.StaffNeededProperty);
			int crew = (staffNeeded > 0) ? ParentObject.GetIntProperty(CrewStretchProperty) : 100;
			int condition = KingdomMaterialRules.ConditionPercent(KingdomWear.WearOf(ParentObject));
			LastWorkedTick = KingdomResearch.Advance(system, TimeTick, LastWorkedTick, crew, condition,
				(Rung > 0) ? Rung : KingdomResearchRules.ScriptoriumPercent,
				ParentObject.ShortDisplayName, KingdomCrews.AffinityOf(ParentObject));
		}
	}
}
