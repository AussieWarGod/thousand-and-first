using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ThousandAndFirst.Simulation.City;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	internal static class KingdomResidentDeathCodec
	{
		internal const int MaxWire = 8388608;
		private const int Magic = 0x31445254;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
		internal static bool TryEncode(KingdomResidentDeathJournal journal, out string wire)
		{
			wire = null;
			if (!KingdomResidentDeathRules.Valid(journal)) return false;
			try
			{
				using (var stream = new MemoryStream())
				using (var w = new BinaryWriter(stream, Utf8, true))
				{
					w.Write(Magic); w.Write(journal.Realm); w.Write(journal.Settlement); w.Write(journal.Entries.Count);
					foreach (var r in journal.Entries) Write(w, r);
					w.Flush();
					if (stream.Length > MaxWire / 4 * 3 - 4) return false;
					wire = "rd1:" + Convert.ToBase64String(stream.ToArray()); return wire.Length <= MaxWire;
				}
			}
			catch { wire = null; return false; }
		}
		internal static bool TryDecode(string wire, out KingdomResidentDeathJournal journal)
		{
			journal = null;
			if (wire == null || wire.Length > MaxWire || !wire.StartsWith("rd1:", StringComparison.Ordinal)) return false;
			try
			{
				using (var stream = new MemoryStream(Convert.FromBase64String(wire.Substring(4)), false))
				using (var r = new BinaryReader(stream, Utf8, true))
				{
					if (r.ReadInt32() != Magic) return false;
					string realm = r.ReadString(), settlement = r.ReadString(); int count = r.ReadInt32();
					if (count < 0 || count > KingdomResidentDeathRules.MaxEntries) return false;
					var rows = new List<KingdomResidentDeathReceipt>();
					for (int i = 0; i < count; i++) rows.Add(Read(r));
					var value = new KingdomResidentDeathJournal(realm, settlement, rows);
					if (stream.Position != stream.Length || !TryEncode(value, out string canonical) || canonical != wire) return false;
					journal = value; return true;
				}
			}
			catch { return false; }
		}
		internal static string Row(KingdomResidentRow row)
		{
			using (var s = new MemoryStream())
			using (var w = new BinaryWriter(s, Utf8, true))
			{ WriteRow(w, row); w.Flush(); return Convert.ToBase64String(s.ToArray()); }
		}
		internal static string Fields(params string[] fields)
		{
			using (var s = new MemoryStream())
			using (var w = new BinaryWriter(s, Utf8, true))
			{ w.Write(fields.Length); foreach (string field in fields) Nullable(w, field); w.Flush(); return Convert.ToBase64String(s.ToArray()); }
		}
		internal static string[] ReadFields(string text, int maximum = 65536)
		{
			using (var s = new MemoryStream(Convert.FromBase64String(text), false))
			using (var r = new BinaryReader(s, Utf8, true))
			{
				int count = r.ReadInt32(); if (count < 0 || count > maximum) throw new InvalidDataException();
				var fields = new string[count]; for (int i = 0; i < count; i++) fields[i] = Nullable(r);
				if (s.Position != s.Length || Fields(fields) != text) throw new InvalidDataException(); return fields;
			}
		}
		internal static string Map(IDictionary<string, int> map)
		{
			if (map == null || map.Count > 4096) throw new InvalidDataException();
			var keys = new List<string>(map.Keys); keys.Sort(StringComparer.Ordinal);
			using (var s = new MemoryStream())
			using (var w = new BinaryWriter(s, Utf8, true))
			{
				w.Write(keys.Count);
				foreach (string key in keys)
				{
					if (!KingdomResidentDeathRules.Text(key, 1024, false) || map[key] < 0) throw new InvalidDataException();
					w.Write(key); w.Write(map[key]);
				}
				w.Flush(); return Convert.ToBase64String(s.ToArray());
			}
		}
		internal static Dictionary<string, int> ReadMap(string text)
		{
			using (var s = new MemoryStream(Convert.FromBase64String(text), false))
			using (var r = new BinaryReader(s, Utf8, true))
			{
				int count = r.ReadInt32(); if (count < 0 || count > 4096) throw new InvalidDataException();
				var map = new Dictionary<string, int>(StringComparer.Ordinal);
				for (int i = 0; i < count; i++) map.Add(r.ReadString(), r.ReadInt32());
				if (s.Position != s.Length || Map(map) != text) throw new InvalidDataException();
				return map;
			}
		}
		private static void Write(BinaryWriter w, KingdomResidentDeathReceipt r)
		{
			w.Write(r.Realm); w.Write(r.Settlement); w.Write(r.SettlementName); w.Write(r.Body); w.Write(r.Zone);
			w.Write(r.Tick); w.Write(r.MintedTick); WriteRow(w, r.Before); w.Write((byte)r.Cause); w.Write(r.Memory);
			w.Write(r.Remembrance); w.Write(r.RemembranceUnavailable);
			w.Write(r.StepWire); w.Write(r.RoofWork); w.Write(r.RoofIndex); w.Write(r.RoofBlocked);
			w.Write(r.Culture); w.Write(r.Species); w.Write(r.IdentityKeys); w.Write(r.Creed); w.Write(r.PastCreeds);
			w.Write(r.OfficeGeneration); w.Write(r.CookGeneration); w.Write(r.OfficeBefore); w.Write(r.CookBefore); w.Write(r.FigureId); w.Write(r.RoleFault);
			w.Write(r.ExpeditionBefore); w.Write(r.ExpeditionPrepared);
			w.Write((byte)r.Phase); w.Write((byte)r.Telling); w.Write(r.AccountProof); w.Write(r.BeforeAccounts.Length);
			for (int i = 0; i < r.BeforeAccounts.Length; i++) { w.Write(r.BeforeAccounts[i]); w.Write(r.AfterAccounts[i]); }
		}
		private static KingdomResidentDeathReceipt Read(BinaryReader r)
		{
			var v = new KingdomResidentDeathReceipt
			{
				Realm = r.ReadString(), Settlement = r.ReadString(), SettlementName = r.ReadString(), Body = r.ReadString(), Zone = r.ReadString(),
				Tick = r.ReadInt64(), MintedTick = r.ReadInt64(), Before = ReadRow(r), Cause = (KingdomStandingCause)r.ReadByte(), Memory = Flag(r),
				Remembrance = Flag(r), RemembranceUnavailable = Flag(r),
				StepWire = r.ReadString(), RoofWork = r.ReadString(), RoofIndex = r.ReadInt32(), RoofBlocked = Flag(r),
				Culture = r.ReadString(), Species = r.ReadString(), IdentityKeys = r.ReadString(), Creed = r.ReadString(), PastCreeds = r.ReadString(),
				OfficeGeneration = r.ReadInt32(), CookGeneration = r.ReadInt32(), OfficeBefore = r.ReadString(), CookBefore = r.ReadString(), FigureId = r.ReadString(), RoleFault = r.ReadString(),
				ExpeditionBefore = r.ReadString(), ExpeditionPrepared = r.ReadString(),
				Phase = (KingdomResidentDeathPhase)r.ReadByte(), Telling = (KingdomResidentDeathTelling)r.ReadByte(), AccountProof = r.ReadString()
			};
			int count = r.ReadInt32(); if (count != 0 && count != 6) throw new InvalidDataException();
			v.BeforeAccounts = new string[count]; v.AfterAccounts = new string[count];
			for (int i = 0; i < count; i++) { v.BeforeAccounts[i] = r.ReadString(); v.AfterAccounts[i] = r.ReadString(); }
			return v;
		}
		private static bool Flag(BinaryReader r)
		{ byte b = r.ReadByte(); if (b > 1) throw new InvalidDataException(); return b == 1; }
		private static void WriteRow(BinaryWriter w, KingdomResidentRow r)
		{
			w.Write(r.ResidentId); w.Write(r.Name); w.Write(r.Origin); w.Write(r.OriginCode); w.Write(r.CreedCode); w.Write(r.ArrivedTick);
			w.Write(r.Arrived); w.Write(r.HomeWorkId); w.Write(r.JobWorkId); w.Write(r.JobRole); w.Write((byte)r.DayShape);
			w.Write((byte)r.Standing); w.Write((byte)r.Cause); Nullable(w, r.BoundZoneId);
			Brink(w, r.RoofBrink); Brink(w, r.CreedBrink); Nullable(w, r.CreedToward); w.Write(r.CreedChannel); Nullable(w, r.KeptCreeds);
		}
		private static KingdomResidentRow ReadRow(BinaryReader r)
		{
			int id = r.ReadInt32(); string name = r.ReadString(), origin = r.ReadString(); int originCode = r.ReadInt32(), creedCode = r.ReadInt32();
			long tick = r.ReadInt64(); string arrived = r.ReadString(); int home = r.ReadInt32(), job = r.ReadInt32(); byte role = r.ReadByte();
			var day = (KingdomDayShape)r.ReadByte(); var standing = (KingdomResidentStanding)r.ReadByte(); var cause = (KingdomStandingCause)r.ReadByte();
			string zone = Nullable(r); var roof = Brink(r); var creed = Brink(r); string toward = Nullable(r); byte channel = r.ReadByte();
			return new KingdomResidentRow(id, name, originCode, creedCode, tick, home, job, role, day, standing, cause, zone,
				roof, creed, toward, channel, Nullable(r), origin, arrived);
		}
		private static void Nullable(BinaryWriter w, string value) { w.Write(value != null); if (value != null) w.Write(value); }
		private static string Nullable(BinaryReader r) { return Flag(r) ? r.ReadString() : null; }
		private static void Brink(BinaryWriter w, KingdomBrinkWindow b) { w.Write(b.Stands); w.Write(b.ReachedTick); w.Write(b.WarnedTick); }
		private static KingdomBrinkWindow Brink(BinaryReader r) { return new KingdomBrinkWindow(Flag(r), r.ReadInt64(), r.ReadInt64()); }
	}
}
