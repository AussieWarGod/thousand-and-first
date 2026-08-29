using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst.Treaty
{
	public static class KingdomTreatyRules
	{
		public const int MaxIdBytes=128, MaxLocatorBytes=192, MaxProseBytes=256,
			MaxCauseBytes=128, MaxLiquidBytes=64, MaxClauses=8, MaxObligations=8, MaxFavors=8,
			MaxEffects=16, MaxWitnessEvents=8;
		public static KingdomTreatyRecord Propose(KingdomTreatyLedger l,long expected,string id,
			string a,string b,IList<string> clauses,IList<string> obligations,IList<string> favors,
			string liquid,long tick,out string failure)
		{
			failure=null;if(!Valid(l)||l.Revision!=expected||!Id(id)||!Id(a)||!Id(b)||a==b
				||!Liquid(liquid)||tick<0||!Copy(clauses,MaxClauses,out var c)
				||!Copy(obligations,MaxObligations,out var o)||!Copy(favors,MaxFavors,out var f))
			{failure="invalid treaty proposal";return null;}
			for(int i=0;i<l.Pacts.Count;i++)if(l.Pacts[i].PactId==id){if(!SameProposal(l.Pacts[i],a,b,c,o,f,liquid))
				{failure="treaty identity conflicts";return null;}return Clone(l.Pacts[i]);}
			if(l.Pacts.Count>=KingdomTreatyLedger.MaxPacts){failure="treaty capacity full";return null;}
			var p=new KingdomTreatyRecord{PactId=id,PartyA=a,PartyB=b,RitualLiquid=liquid,
				Phase=PactPhase.Proposed,ProposedTick=tick};p.Clauses.AddRange(c);p.Obligations.AddRange(o);
			p.Favors.AddRange(f);if(!Valid(p)){failure="invalid treaty proposal";return null;}
			l.Pacts.Add(p);l.Revision++;return Clone(p);
		}
		public static bool Ratify(KingdomTreatyLedger l,long expected,string id,string sa,string sb,
			long tick,out string failure)=>Transition(l,expected,id,PactPhase.Proposed,PactPhase.Ratified,
				tick,(p)=>{if(tick<p.ProposedTick||!Id(sa)||!Id(sb))return false;p.SignatureA=sa;p.SignatureB=sb;return true;},out failure);
		public static bool Activate(KingdomTreatyLedger l,long expected,string id,long start,long expiry,
			bool partiesExist,out string failure)=>Transition(l,expected,id,PactPhase.Ratified,
				PactPhase.Active,start,(p)=>{if(start<p.ProposedTick||expiry!=-1&&expiry<=start)return false;
				p.StartTick=start;p.ExpiryTick=expiry;p.EffectsSuspended=!partiesExist;return true;},out failure);
		public static bool Fulfill(KingdomTreatyLedger l,long e,string id,long tick,out string f)=>
			Transition(l,e,id,PactPhase.Active,PactPhase.Fulfilled,tick,p=>tick>=p.StartTick,out f);
		public static bool Dissolve(KingdomTreatyLedger l,long e,string id,long tick,out string f)
		{
			f=null;if(!Valid(l)||l.Revision!=e||tick<0)return Fail("invalid dissolution",out f);
			var p=Find(l,id);if(p==null||p.Phase==PactPhase.Dissolved)return p!=null;
			if(tick<p.ProposedTick)return Fail("dissolution predates pact",out f);
			var next=Clone(p);next.Phase=PactPhase.Dissolved;next.Revision++;
			if(!Valid(next))return Fail("invalid dissolution",out f);l.Pacts[l.Pacts.IndexOf(p)]=next;l.Revision++;return true;
		}
		public static bool ApplyEffect(KingdomTreatyLedger l,long e,string id,string effect,
			bool partiesExist,out string f)
		{
			f=null;if(!Valid(l)||l.Revision!=e||!Id(effect))return Fail("invalid treaty effect",out f);
			var p=Find(l,id);if(p==null||p.Phase!=PactPhase.Active)return Fail("treaty inactive",out f);
			if(!partiesExist){if(!p.EffectsSuspended){var suspended=Clone(p);suspended.EffectsSuspended=true;suspended.Revision++;
				if(!Valid(suspended))return Fail("invalid suspension",out f);l.Pacts[l.Pacts.IndexOf(p)]=suspended;l.Revision++;}return false;}
			if(p.AppliedEffectIds.Contains(effect))return true;if(p.AppliedEffectIds.Count>=MaxEffects)return Fail("effect capacity full",out f);
			var next=Clone(p);next.EffectsSuspended=false;next.AppliedEffectIds.Add(effect);next.Revision++;
			if(!Valid(next))return Fail("invalid treaty effect",out f);l.Pacts[l.Pacts.IndexOf(p)]=next;l.Revision++;return true;
		}
		public static bool ObserveWitness(KingdomTreatyLedger l,long e,string pact,string projection,
			PactWitnessEventKind kind,string eventId,string actor,string cause,long tick,bool typedTheft,out string f)
		{
			f=null;if(!Valid(l)||l.Revision!=e||!Id(projection)||!Id(eventId)||!Cause(cause)||tick<0)
				return Fail("invalid pact witness",out f);var p=Find(l,pact);if(p==null)return Fail("pact absent",out f);
			if(tick<p.ProposedTick)return Fail("witness predates pact",out f);
			for(int i=0;i<p.WitnessEvents.Count;i++)if(p.WitnessEvents[i].EventId==eventId)return true;
			if(p.WitnessEvents.Count>=MaxWitnessEvents)return Fail("witness capacity full",out f);
			if(kind==PactWitnessEventKind.WitnessStolen&&(!typedTheft||!Id(actor)))kind=PactWitnessEventKind.WitnessLost;
			if(kind==PactWitnessEventKind.DeliberateBreach&&(!Id(actor)||p.Phase!=PactPhase.Active))return Fail("breach authority absent",out f);
			var next=Clone(p);bool duplicate=next.ProjectionId!=null&&next.ProjectionId!=projection;
			if(duplicate)kind=PactWitnessEventKind.DuplicateInert;
			else if(next.ProjectionId==null)next.ProjectionId=projection;
			if(!duplicate&&kind!=PactWitnessEventKind.DeliberateBreach)
				next.WitnessStatus=(PactWitnessStatus)(byte)kind;
			next.WitnessEvents.Add(new PactWitnessEvent{EventId=eventId,PactId=pact,ProjectionId=projection,
				Kind=kind,ActorId=actor,Cause=cause,Tick=tick});
			if(kind==PactWitnessEventKind.DeliberateBreach){next.Phase=PactPhase.Breached;next.BreachActorId=actor;next.BreachCause=cause;}
			next.Revision++;if(!Valid(next))return Fail("invalid witness result",out f);
			l.Pacts[l.Pacts.IndexOf(p)]=next;l.Revision++;return true;
		}
		public static bool Reissue(KingdomTreatyLedger l,long e,string id,string projection,string locator,out string f)
		{
			f=null;if(!Valid(l)||l.Revision!=e||!Id(projection)||!Locator(locator))return Fail("invalid reissue",out f);
			var p=Find(l,id);if(p==null)return Fail("pact absent",out f);var next=Clone(p);next.ProjectionId=projection;
			next.ProjectionLocator=locator;next.WitnessStatus=PactWitnessStatus.Projected;next.Revision++;
			if(!Valid(next))return Fail("invalid reissue",out f);l.Pacts[l.Pacts.IndexOf(p)]=next;l.Revision++;return true;
		}
		internal static bool Valid(KingdomTreatyLedger l)
		{
			if(l==null||l.StoreState!=KingdomTreatyStoreState.Healthy||l.Quarantined||l.Fault!=null||l.OpaquePayload!=null||l.Revision<0||l.Pacts.Count>16)return false;
			var ids=new HashSet<string>(StringComparer.Ordinal);for(int i=0;i<l.Pacts.Count;i++)if(!Valid(l.Pacts[i])||!ids.Add(l.Pacts[i].PactId))return false;return true;
		}
		private static bool Valid(KingdomTreatyRecord p)
		{
			if(p==null||p.Version!=1||!Id(p.PactId)||!Id(p.PartyA)||!Id(p.PartyB)||p.PartyA==p.PartyB
				||p.Phase<PactPhase.Proposed||p.Phase>PactPhase.Dissolved||!Liquid(p.RitualLiquid)
				||p.ProposedTick<0||p.Revision<0||p.Clauses.Count>8||p.Obligations.Count>8
				||p.Favors.Count>8||p.AppliedEffectIds.Count>16||p.WitnessEvents.Count>8)return false;
			bool signed=p.Phase==PactPhase.Ratified||p.Phase==PactPhase.Active
				||p.Phase==PactPhase.Fulfilled||p.Phase==PactPhase.Breached;
			if(signed&&(!Id(p.SignatureA)||!Id(p.SignatureB))
				||p.Phase==PactPhase.Dissolved&&((p.SignatureA==null)!=(p.SignatureB==null)
					||p.SignatureA!=null&&(!Id(p.SignatureA)||!Id(p.SignatureB))))return false;
			if(p.Phase==PactPhase.Proposed&&(p.SignatureA!=null||p.SignatureB!=null||p.StartTick!=-1||p.ExpiryTick!=-1)
				||p.Phase==PactPhase.Ratified&&(p.StartTick!=-1||p.ExpiryTick!=-1)
				||p.Phase>=PactPhase.Active&&p.Phase<=PactPhase.Breached
					&&(p.StartTick<p.ProposedTick||p.ExpiryTick!=-1&&p.ExpiryTick<=p.StartTick))return false;
			for(int i=0;i<p.Clauses.Count;i++)if(!Prose(p.Clauses[i]))return false;
			for(int i=0;i<p.Obligations.Count;i++)if(!Prose(p.Obligations[i]))return false;
			for(int i=0;i<p.Favors.Count;i++)if(!Prose(p.Favors[i]))return false;
			var effects=new HashSet<string>(StringComparer.Ordinal);for(int i=0;i<p.AppliedEffectIds.Count;i++)if(!Id(p.AppliedEffectIds[i])||!effects.Add(p.AppliedEffectIds[i]))return false;
			if(p.ProjectionId!=null&&!Id(p.ProjectionId)||p.ProjectionLocator!=null&&!Locator(p.ProjectionLocator)
				||p.BreachActorId!=null&&!Id(p.BreachActorId)||p.BreachCause!=null&&!Cause(p.BreachCause))return false;
			if(!Enum.IsDefined(typeof(PactWitnessStatus),p.WitnessStatus)
				||p.Phase==PactPhase.Breached&&(p.BreachActorId==null||p.BreachCause==null)
				||p.Phase!=PactPhase.Breached&&p.Phase!=PactPhase.Dissolved
					&&(p.BreachActorId!=null||p.BreachCause!=null))return false;
			var events=new HashSet<string>(StringComparer.Ordinal);for(int i=0;i<p.WitnessEvents.Count;i++){var x=p.WitnessEvents[i];if(x==null||!Enum.IsDefined(typeof(PactWitnessEventKind),x.Kind)||!Id(x.EventId)||x.PactId!=p.PactId||!Id(x.ProjectionId)||(x.ActorId!=null&&!Id(x.ActorId))||!Cause(x.Cause)||x.Tick<0||!events.Add(x.EventId))return false;}
			return true;
		}
		private static bool Transition(KingdomTreatyLedger l,long e,string id,PactPhase from,PactPhase to,long tick,Func<KingdomTreatyRecord,bool> apply,out string f)
		{f=null;if(!Valid(l)||l.Revision!=e||tick<0)return Fail("invalid treaty transition",out f);var p=Find(l,id);if(p==null||p.Phase!=from)return Fail("treaty transition rejected",out f);
			var next=Clone(p);if(!apply(next))return Fail("treaty transition rejected",out f);next.Phase=to;next.Revision++;
			if(!Valid(next))return Fail("treaty transition rejected",out f);l.Pacts[l.Pacts.IndexOf(p)]=next;l.Revision++;return true;}
		private static KingdomTreatyRecord Clone(KingdomTreatyRecord p){var n=new KingdomTreatyRecord{Version=p.Version,PactId=p.PactId,PartyA=p.PartyA,PartyB=p.PartyB,RitualLiquid=p.RitualLiquid,SignatureA=p.SignatureA,SignatureB=p.SignatureB,Phase=p.Phase,ProposedTick=p.ProposedTick,StartTick=p.StartTick,ExpiryTick=p.ExpiryTick,Revision=p.Revision,ProjectionId=p.ProjectionId,ProjectionLocator=p.ProjectionLocator,WitnessStatus=p.WitnessStatus,BreachActorId=p.BreachActorId,BreachCause=p.BreachCause,EffectsSuspended=p.EffectsSuspended};
			n.Clauses.AddRange(p.Clauses);n.Obligations.AddRange(p.Obligations);n.Favors.AddRange(p.Favors);n.AppliedEffectIds.AddRange(p.AppliedEffectIds);for(int i=0;i<p.WitnessEvents.Count;i++){var x=p.WitnessEvents[i];n.WitnessEvents.Add(new PactWitnessEvent{EventId=x.EventId,PactId=x.PactId,ProjectionId=x.ProjectionId,ActorId=x.ActorId,Cause=x.Cause,Kind=x.Kind,Tick=x.Tick});}return n;}
		private static KingdomTreatyRecord Find(KingdomTreatyLedger l,string id){for(int i=0;i<l.Pacts.Count;i++)if(l.Pacts[i].PactId==id)return l.Pacts[i];return null;}
		private static bool SameProposal(KingdomTreatyRecord p,string a,string b,IList<string>c,IList<string>o,IList<string>f,string liquid)
		{return p.PartyA==a&&p.PartyB==b&&p.RitualLiquid==liquid&&Same(p.Clauses,c)&&Same(p.Obligations,o)&&Same(p.Favors,f);}
		private static bool Same(IList<string>a,IList<string>b){if(a.Count!=b.Count)return false;for(int i=0;i<a.Count;i++)if(a[i]!=b[i])return false;return true;}
		private static bool Copy(IList<string>x,int max,out List<string>r){r=new List<string>();if(x==null||x.Count>max)return false;for(int i=0;i<x.Count;i++){if(!Prose(x[i]))return false;r.Add(x[i]);}return true;}
		private static bool Id(string s)=>Utf8(s,MaxIdBytes); private static bool Locator(string s)=>Utf8(s,MaxLocatorBytes);
		private static bool Prose(string s)=>Utf8(s,MaxProseBytes); private static bool Cause(string s)=>Utf8(s,MaxCauseBytes);
		private static bool Liquid(string s)=>Utf8(s,MaxLiquidBytes);
		private static bool Utf8(string s,int max){if(string.IsNullOrEmpty(s)||s.Trim()!=s)return false;try{return new UTF8Encoding(false,true).GetByteCount(s)<=max;}catch(EncoderFallbackException){return false;}}
		private static bool Fail(string x,out string f){f=x;return false;}
	}
}
