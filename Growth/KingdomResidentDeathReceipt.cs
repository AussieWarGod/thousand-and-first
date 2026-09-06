using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal enum KingdomResidentDeathPhase : byte
	{ Witnessed, StandingWritten, BindingRetired, RolesSettled, AccountingPrepared, Accounted, Settled }
	internal enum KingdomResidentDeathTelling : byte
	{ Pending, Attempting, Owned, Uncertain, Disabled }

	// These are codec-owned records, never engine-reflected fields or body-absence evidence.
	internal sealed class KingdomResidentDeathReceipt
	{
		internal string Realm = "", Settlement = "", SettlementName = "", Body = "", Zone = "";
		internal long Tick, MintedTick;
		internal KingdomResidentRow Before;
		internal KingdomStandingCause Cause;
		internal bool Memory;
		internal bool Remembrance, RemembranceUnavailable;
		internal string StepWire = "", RoofWork = "";
		internal int RoofIndex = -1;
		internal bool RoofBlocked;
		internal string Culture = "", Species = "", IdentityKeys = "", Creed = "", PastCreeds = "";
		internal int OfficeGeneration, CookGeneration;
		internal string OfficeBefore = "", CookBefore = "", FigureId = "";
		internal string ExpeditionBefore = "", ExpeditionPrepared = "";
		internal string RoleFault = "";
		internal KingdomResidentDeathPhase Phase;
		internal KingdomResidentDeathTelling Telling;
		internal string AccountProof = "";
		internal string[] BeforeAccounts = new string[0], AfterAccounts = new string[0];
		internal KingdomResidentDeathReceipt Copy()
		{
			var copy = (KingdomResidentDeathReceipt)MemberwiseClone();
			copy.BeforeAccounts = (string[])BeforeAccounts.Clone();
			copy.AfterAccounts = (string[])AfterAccounts.Clone();
			return copy;
		}
	}

	internal sealed class KingdomResidentDeathJournal
	{
		internal readonly string Realm, Settlement;
		internal readonly IReadOnlyList<KingdomResidentDeathReceipt> Entries;
		internal KingdomResidentDeathJournal(string realm, string settlement,
			IEnumerable<KingdomResidentDeathReceipt> entries)
		{
			Realm = realm; Settlement = settlement;
			var rows = new List<KingdomResidentDeathReceipt>();
			foreach (var row in entries) rows.Add(row?.Copy());
			Entries = rows.AsReadOnly();
		}
		internal KingdomResidentDeathJournal With(int index, KingdomResidentDeathReceipt row)
		{
			var rows = new List<KingdomResidentDeathReceipt>(Entries);
			if (index == rows.Count) rows.Add(row); else rows[index] = row;
			return new KingdomResidentDeathJournal(Realm, Settlement, rows);
		}
	}
}
