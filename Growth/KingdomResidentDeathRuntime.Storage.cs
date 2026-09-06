using System;
using System.Collections.Generic;
using XRL;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomResidentDeathRuntime
	{
		private sealed class Frame
		{
			internal XRLGame Game;
			internal KingdomSystem System;
			internal KingdomCityBook City;
			internal string Realm, Settlement, Key, Wire;
			internal object[] Tables;
			internal KingdomResidentDeathJournal Journal;
		}
		private static string Key(string realm, string settlement)
		{ return "r_TAF_ResidentDeaths_v1:" + realm + ":" + settlement; }
		private static bool Open(KingdomSystem system, KingdomCityBook city, out Frame frame)
		{
			frame = null; var game = The.Game;
			if (game == null || system == null || city == null || !system.Founded || system.LoadFailed
				|| !ReferenceEquals(game.GetSystem<KingdomSystem>(), system) || !KingdomIdentityRules.IsRealmId(system.CurrentRealmId)
				|| !KingdomIdentityRules.IsSettlementId(city.SettlementId) || !Owned(system, city)
				|| !city.HasValidSubsidenceStorage() || !city.TryReadExact(out _, out _)) return false;
			var value = new Frame { Game = game, System = system, City = city, Realm = system.CurrentRealmId, Settlement = city.SettlementId };
			value.Key = Key(value.Realm, value.Settlement);
			if (!ReadKey(game, value.Key, out value.Wire, out value.Tables)) return false;
			if (value.Wire == null) value.Journal = new KingdomResidentDeathJournal(value.Realm, value.Settlement, new KingdomResidentDeathReceipt[0]);
			else if (!KingdomResidentDeathCodec.TryDecode(value.Wire, out value.Journal)
				|| value.Journal.Realm != value.Realm || value.Journal.Settlement != value.Settlement) return false;
			if (!Exact(value)) return false; frame = value; return true;
		}
		private static bool Owned(KingdomSystem system, KingdomCityBook city)
		{
			if (!system.TryFindSettlement(city, out bool seated, out KingdomSettlement other)) return false;
			return seated ? ReferenceEquals(system.City, city) : other != null && ReferenceEquals(other.City, city);
		}
		private static bool Exact(Frame f)
		{
			if (f == null || !ReferenceEquals(The.Game, f.Game) || !ReferenceEquals(f.Game.GetSystem<KingdomSystem>(), f.System)
				|| f.System.LoadFailed || !f.System.Founded || f.System.CurrentRealmId != f.Realm || f.City.SettlementId != f.Settlement
				|| !Owned(f.System, f.City) || !ReadKey(f.Game, f.Key, out string wire, out object[] tables) || wire != f.Wire) return false;
			for (int i = 0; i < tables.Length; i++) if (!ReferenceEquals(tables[i], f.Tables[i])) return false;
			return true;
		}
		private static bool Save(Frame f, KingdomResidentDeathJournal next)
		{
			if (!Exact(f) || next.Realm != f.Realm || next.Settlement != f.Settlement
				|| !KingdomResidentDeathCodec.TryEncode(next, out string wire) || !Exact(f)) return false;
			f.Game.StringGameState[f.Key] = wire;
			f.Wire = wire; f.Journal = next;
			return Exact(f);
		}
		private static bool Save(Frame f, int index, KingdomResidentDeathReceipt r)
		{ return Save(f, f.Journal.With(index, r)); }
		private static bool ReadKey(XRLGame game, string key, out string wire, out object[] tables)
		{
			wire = null;
			tables = new object[] { game.StringGameState, game.IntGameState, game.Int64GameState, game.ObjectGameState, game.BooleanGameState };
			foreach (object table in tables) if (table == null) return false;
			var row = new KingdomDurableKeyObservation { HasString = game.HasStringGameState(key), HasInt = game.HasIntGameState(key),
				HasInt64 = game.HasInt64GameState(key), HasObject = game.HasObjectGameState(key), HasBoolean = game.HasBooleanGameState(key) };
			if (row.HasString) row.String = game.GetStringGameState(key);
			if (!KingdomScenarioStateShape.TryAuthorityText(row, out wire, out _, out _)) return false;
			return ReferenceEquals(The.Game, game) && ReferenceEquals(tables[0], game.StringGameState)
				&& ReferenceEquals(tables[1], game.IntGameState) && ReferenceEquals(tables[2], game.Int64GameState)
				&& ReferenceEquals(tables[3], game.ObjectGameState) && ReferenceEquals(tables[4], game.BooleanGameState);
		}
		internal static bool CanProceed(KingdomSystem system, out string failure)
		{
			failure = "A witnessed death awaits exact accounting; its journal is retained.";
			try
			{
				if (system == null || !system.Founded) return false;
				var books = system.OwnedCityBooks(); var ids = new HashSet<string>(StringComparer.Ordinal);
				if (books == null || books.Count == 0) return false;
				foreach (var book in books)
				{
					if (book == null || !ids.Add(book.SettlementId) || !Open(system, book, out Frame f)) return false;
					foreach (var r in f.Journal.Entries) if (r.Phase != KingdomResidentDeathPhase.Settled) return false;
					if (!Exact(f)) return false;
				}
				failure = null; return true;
			}
			catch { return false; }
		}
		internal static bool OwnsFuneral(KingdomSystem system, KingdomCityBook book, int residentId)
		{
			try
			{
				if (!Open(system, book, out Frame f)) return true;
				foreach (var r in f.Journal.Entries) if (r.Before.ResidentId == residentId) return true;
				return !Exact(f);
			}
			catch { return true; }
		}
	}
}
