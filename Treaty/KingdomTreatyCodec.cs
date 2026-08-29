using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst.Treaty
{
	public static class KingdomTreatyCodec
	{
		// Exact semantic maximum: 44-byte frame + 12-byte ledger + 16 * 15,083-byte rows.
		public const int Magic=0x31524654, CurrentVersion=1, MaxEnvelopeBytes=241384;
		private static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
		public static bool TryEncode(KingdomTreatyLedger ledger,out byte[] bytes,out string failure)
		{
			bytes=null;failure=null;try
			{
				if(!KingdomTreatyRules.Valid(ledger))throw Bad();byte[] payload;
				using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8,true))
				{w.Write(ledger.Revision);w.Write(ledger.Pacts.Count);for(int i=0;i<ledger.Pacts.Count;i++)Write(w,ledger.Pacts[i]);w.Flush();payload=s.ToArray();}
				using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8,true))
				{w.Write(Magic);w.Write(CurrentVersion);w.Write(payload.Length);w.Write(payload);w.Write(Auth(CurrentVersion,payload));w.Flush();
					if(s.Length>MaxEnvelopeBytes)throw Bad();bytes=s.ToArray();return true;}
			}catch(Exception e)when(Wire(e)){failure="treaty wire is invalid or exceeds its cap";return false;}
		}
		public static KingdomTreatyLedger Decode(byte[] bytes)
		{
			try
			{
				if(bytes==null||bytes.Length>MaxEnvelopeBytes)throw Bad();using(var r=new BinaryReader(new MemoryStream(bytes,false),Utf8,false))
				{
					if(r.ReadInt32()!=Magic)throw Bad();int version=r.ReadInt32();int n=r.ReadInt32();
					if(n<0||n>MaxEnvelopeBytes-44)throw Bad();byte[] payload=r.ReadBytes(n), hash=r.ReadBytes(32);
					if(payload.Length!=n||hash.Length!=32||r.BaseStream.Position!=r.BaseStream.Length||!Hash(Auth(version,payload),hash))throw Bad();
					if(version>CurrentVersion)return Opaque(bytes,"future treaty payload preserved");if(version!=1)throw Bad();
					var ledger=new KingdomTreatyLedger();using(var p=new BinaryReader(new MemoryStream(payload,false),Utf8,false))
					{ledger.Revision=p.ReadInt64();int count=Count(p,KingdomTreatyLedger.MaxPacts);for(int i=0;i<count;i++)ledger.Pacts.Add(Read(p));
						if(p.BaseStream.Position!=p.BaseStream.Length||!KingdomTreatyRules.Valid(ledger))throw Bad();return ledger;}
				}
			}catch(Exception e)when(Wire(e)){return Opaque(bytes,"malformed treaty payload quarantined");}
		}
		private static void Write(BinaryWriter w,KingdomTreatyRecord p)
		{
			w.Write(p.Version);S(w,p.PactId);S(w,p.PartyA);S(w,p.PartyB);w.Write((byte)p.Phase);
			Strings(w,p.Clauses);Strings(w,p.Obligations);Strings(w,p.Favors);S(w,p.RitualLiquid);
			S(w,p.SignatureA,true);S(w,p.SignatureB,true);w.Write(p.ProposedTick);w.Write(p.StartTick);w.Write(p.ExpiryTick);w.Write(p.Revision);
			S(w,p.ProjectionId,true);S(w,p.ProjectionLocator,true);w.Write((byte)p.WitnessStatus);
			S(w,p.BreachActorId,true);S(w,p.BreachCause,true);w.Write(p.EffectsSuspended);Strings(w,p.AppliedEffectIds);
			w.Write(p.WitnessEvents.Count);for(int i=0;i<p.WitnessEvents.Count;i++){var x=p.WitnessEvents[i];S(w,x.EventId);S(w,x.PactId);S(w,x.ProjectionId);w.Write((byte)x.Kind);S(w,x.ActorId,true);S(w,x.Cause);w.Write(x.Tick);}
		}
		private static KingdomTreatyRecord Read(BinaryReader r)
		{
			var p=new KingdomTreatyRecord{Version=r.ReadInt32(),PactId=S(r),PartyA=S(r),PartyB=S(r),Phase=(PactPhase)r.ReadByte()};
			ReadStrings(r,p.Clauses,8);ReadStrings(r,p.Obligations,8);ReadStrings(r,p.Favors,8);p.RitualLiquid=S(r);
			p.SignatureA=S(r,true);p.SignatureB=S(r,true);p.ProposedTick=r.ReadInt64();p.StartTick=r.ReadInt64();p.ExpiryTick=r.ReadInt64();p.Revision=r.ReadInt64();
			p.ProjectionId=S(r,true);p.ProjectionLocator=S(r,true);p.WitnessStatus=(PactWitnessStatus)r.ReadByte();p.BreachActorId=S(r,true);p.BreachCause=S(r,true);
			p.EffectsSuspended=r.ReadBoolean();ReadStrings(r,p.AppliedEffectIds,16);int count=Count(r,8);
			for(int i=0;i<count;i++)p.WitnessEvents.Add(new PactWitnessEvent{EventId=S(r),PactId=S(r),ProjectionId=S(r),Kind=(PactWitnessEventKind)r.ReadByte(),ActorId=S(r,true),Cause=S(r),Tick=r.ReadInt64()});return p;
		}
		private static void Strings(BinaryWriter w,IList<string>x){w.Write(x.Count);for(int i=0;i<x.Count;i++)S(w,x[i]);}
		private static void ReadStrings(BinaryReader r,List<string>x,int max){int n=Count(r,max);for(int i=0;i<n;i++)x.Add(S(r));}
		private static int Count(BinaryReader r,int max){int n=r.ReadInt32();if(n<0||n>max)throw Bad();return n;}
		private static void S(BinaryWriter w,string x,bool nullable=false){if(x==null){if(!nullable)throw Bad();w.Write(-1);return;}byte[] b=Utf8.GetBytes(x);if(b.Length>256)throw Bad();w.Write(b.Length);w.Write(b);}
		private static string S(BinaryReader r,bool nullable=false){int n=r.ReadInt32();if(n==-1&&nullable)return null;if(n<0||n>256)throw Bad();byte[] b=r.ReadBytes(n);if(b.Length!=n)throw Bad();return Utf8.GetString(b);}
		private static byte[] Auth(int version,byte[]p){using(var sha=SHA256.Create())using(var s=new MemoryStream()){s.Write(BitConverter.GetBytes(version),0,4);s.Write(p,0,p.Length);return sha.ComputeHash(s.ToArray());}}
		private static bool Hash(byte[]a,byte[]expected){int d=0;for(int i=0;i<32;i++)d|=a[i]^expected[i];return d==0;}
		private static KingdomTreatyLedger Opaque(byte[]b,string f)=>new KingdomTreatyLedger{StoreState=f.StartsWith("future",StringComparison.Ordinal)?KingdomTreatyStoreState.FutureOpaque:KingdomTreatyStoreState.Quarantined,Quarantined=true,Fault=f,OpaquePayload=b!=null&&b.Length<=MaxEnvelopeBytes?(byte[])b.Clone():null};
		private static InvalidDataException Bad()=>new InvalidDataException("invalid treaty wire");
		private static bool Wire(Exception e)=>e is IOException||e is InvalidDataException||e is EncoderFallbackException||e is DecoderFallbackException||e is ArgumentException;
	}
}
