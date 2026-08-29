#if !TAF_TESTS
using System;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Treaty
{
	public sealed class PactVesselTheftFact
	{
		public string PactId, ProjectionId, ActorId, EventId, Cause; public long Tick;
	}
	public static class KingdomPactVesselBridge
	{
		public static Action<string,string,PactWitnessEventKind,string,string,string,long,bool> Observe;
		public static bool Configure(GameObject vessel,string pactId,string projectionId)
		{
			if(vessel==null||string.IsNullOrEmpty(pactId)||string.IsNullOrEmpty(projectionId))return false;
			LiquidVolume liquid=vessel.GetPart<LiquidVolume>();if(liquid==null)return false;
			liquid.ManualSeal=false;liquid.Sealed=true;vessel.RequirePart<Unreplicable>();
			var part=vessel.RequirePart<r_KingdomPactVessel>();part.PactId=pactId;part.ProjectionId=projectionId;return true;
		}
		public static bool ObserveTypedTheft(PactVesselTheftFact fact)
		{
			if(fact==null||string.IsNullOrEmpty(fact.ActorId)||string.IsNullOrEmpty(fact.EventId))return false;
			Observe?.Invoke(fact.PactId,fact.ProjectionId,PactWitnessEventKind.WitnessStolen,
				fact.EventId,fact.ActorId,fact.Cause,fact.Tick,true);return Observe!=null;
		}
		public static bool ObserveNonMoral(string pactId,string projectionId,
			PactWitnessEventKind kind,string eventId,string cause,long tick)
		{
			if(kind!=PactWitnessEventKind.WitnessLost&&kind!=PactWitnessEventKind.WitnessDamaged)
				return false;
			Observe?.Invoke(pactId,projectionId,kind,eventId,null,cause,tick,false);
			return Observe!=null;
		}
	}
}

namespace XRL.World.Parts
{
	[Serializable] public sealed class r_KingdomPactVessel:IPart
	{
		public string PactId,ProjectionId;
		public override bool WantEvent(int id,int cascade)=>base.WantEvent(id,cascade)
			||id==GetInventoryActionsAlwaysEvent.ID||id==InventoryActionEvent.ID||id==BeforeDestroyObjectEvent.ID;
		public override bool HandleEvent(GetInventoryActionsAlwaysEvent e)
		{e.AddAction("Break covenant","break covenant","TAF_BreakPact",null,'b',FireOnActor:false,0);return base.HandleEvent(e);}
		public override bool HandleEvent(InventoryActionEvent e)
		{
			if(e.Command!="TAF_BreakPact")return base.HandleEvent(e);if(e.Actor==null||!e.Actor.IsPlayer())return false;
			if(Popup.ShowYesNo("Break this covenant deliberately? This is the only action that records a deliberate breach.")!=DialogResult.Yes)return false;
			string actor=e.Actor.id,eventId="taf:pact-break:"+PactId+":"+ProjectionId;
			ThousandAndFirst.Treaty.KingdomPactVesselBridge.Observe?.Invoke(PactId,ProjectionId,
				ThousandAndFirst.Treaty.PactWitnessEventKind.DeliberateBreach,eventId,actor,
				"player-confirmed covenant break",The.Game?.TimeTicks??0L,true);e.RequestInterfaceExit();return false;
		}
		public override bool HandleEvent(BeforeDestroyObjectEvent e)
		{
			ThousandAndFirst.Treaty.KingdomPactVesselBridge.Observe?.Invoke(PactId,ProjectionId,
				ThousandAndFirst.Treaty.PactWitnessEventKind.WitnessLost,"taf:pact-loss:"+PactId+":"+ProjectionId,
				null,string.IsNullOrEmpty(e.Reason)?"generic vessel destruction":e.Reason,The.Game?.TimeTicks??0L,false);
			return base.HandleEvent(e);
		}
	}
}
#endif
