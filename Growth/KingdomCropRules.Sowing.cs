using System.Text;

using ThousandAndFirst.Simulation.City;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomCropRules
	{
		// ==================================================================================
		// What a field says when it will not grow. STANDARDS 7b: a process that stops short
		// names the want, once, where the founder will see it.
		// ==================================================================================

		/// <summary>Why a field is not producing. Frozen values: a save carries the last reason a
		/// field gave, so it can be unsaid when the block lifts rather than repeated.</summary>
		public enum FieldWant
		{
			/// <summary>Nothing is wrong.</summary>
			None = 0,

			/// <summary>No seed has been committed. The whole of Addendum 11(b)'s gate.</summary>
			Seed = 1,

			/// <summary>Sown, but nobody is working it and the design asks for hands.</summary>
			Hands = 2,

			/// <summary>Gathered, with no dedicated larder anywhere in the realm to put it in.</summary>
			Larder = 3,

			/// <summary>Ruined past the point where anything comes out of it.</summary>
			Condemned = 4
		}

		/// <summary>The one line a blocked field gives, in the ledger's voice. Never empty for a
		/// real want, so a caller cannot accidentally announce silence.</summary>
		/// <param name="Want">What the field is short of.</param>
		/// <param name="FieldName">What the founder calls it, lower case.</param>
		/// <param name="SettlementName">The city it stands in.</param>
		public static string WantNote(FieldWant Want, string FieldName, string SettlementName)
		{
			string field = string.IsNullOrEmpty(FieldName) ? "field" : FieldName;
			string place = string.IsNullOrEmpty(SettlementName) ? "the settlement" : SettlementName;
			switch (Want)
			{
			case FieldWant.Seed:
				return "The " + field + " at " + place + " is bare ground: nothing has been sown in it. Put seed in it, and it will be worked.";
			case FieldWant.Hands:
				return "The " + field + " at " + place + " is sown and nobody is working it. A crop nobody weeds is a crop nobody eats.";
			case FieldWant.Larder:
				return "The " + field + " at " + place + " stands ripe with nowhere to put it. Dedicate a larder, and it will be gathered in.";
			case FieldWant.Condemned:
				return "The " + field + " at " + place + " is past working. Mend it, or strike it and sow somewhere the ground is sound.";
			default:
				return "The " + field + " at " + place + " is not producing.";
			}
		}

		/// <summary>Why a sowing was refused, or that it was allowed.</summary>
		public enum SowVerdict
		{
			Sown = 0,

			/// <summary>The founder is not standing in a field the settlement built.</summary>
			NoField = 1,

			/// <summary>This field already carries somebody's committed seed.</summary>
			AlreadySown = 2,

			/// <summary>The stores cannot spare the water a sowing pours without eating the
			/// settlement's own drinking reserve.</summary>
			NoWater = 3,

			/// <summary>The field is ruined past working.</summary>
			Condemned = 4,

			/// <summary>The seed names a crop this build has no rows for.</summary>
			NoCrop = 5,

			/// <summary>This ground is not the realm's.</summary>
			NotClaimed = 6
		}

		/// <summary>Whether a sowing may go ahead, given everything the caller has already
		/// gathered. Pure, so the whole gate is one tabled decision rather than a ladder of
		/// engine calls nobody can test.</summary>
		/// <param name="HasField">A settlement-built field stands under the founder.</param>
		/// <param name="Claimed">That ground is claimed by the realm.</param>
		/// <param name="AlreadySown">The field already carries seed.</param>
		/// <param name="Condemned">The field is worn past working.</param>
		/// <param name="HasRow">The seed names a crop with a standing row object.</param>
		/// <param name="StoredWater">Drams in the dedicated stores.</param>
		/// <param name="Population">Living settlers, for the reserve.</param>
		public static SowVerdict AssessSow(bool HasField, bool Claimed, bool AlreadySown, bool Condemned, bool HasRow, int StoredWater, int Population)
		{
			if (!HasField)
			{
				return SowVerdict.NoField;
			}
			if (!Claimed)
			{
				return SowVerdict.NotClaimed;
			}
			if (Condemned)
			{
				return SowVerdict.Condemned;
			}
			if (AlreadySown)
			{
				return SowVerdict.AlreadySown;
			}
			if (!HasRow)
			{
				return SowVerdict.NoCrop;
			}
			if (!CanAffordPlanting(StoredWater, Population))
			{
				return SowVerdict.NoWater;
			}
			return SowVerdict.Sown;
		}

		/// <summary>The refusal a founder reads. Never empty, including for
		/// <see cref="SowVerdict.Sown"/>, which no caller should be showing.</summary>
		public static string SowRefusal(SowVerdict Verdict)
		{
			switch (Verdict)
			{
			case SowVerdict.NoField:
				return "There is no field here to sow. Stand in one the settlement has raised - a kitchen garden, a field, a grange - and try again.";
			case SowVerdict.NotClaimed:
				return "This ground is not the realm's. A field is sown where the settlement can work it.";
			case SowVerdict.AlreadySown:
				return "This field is already sown. Withdraw what is in it first, if you mean to change the crop.";
			case SowVerdict.Condemned:
				return "This field is past working. Mend it before you put seed in it.";
			case SowVerdict.NoCrop:
				return "Nothing in this seed knows how to stand in a row here.";
			case SowVerdict.NoWater:
				return "There is not enough water in the stores to wet a seedbed without drinking the settlement's own reserve. Fill the casks first.";
			default:
				return "The seed goes into the ground.";
			}
		}

		/// <summary>What the founder is asked before the seed is spent, in the carry-sign's own
		/// consent-before-cost shape: the exact crop, the exact rows, the exact wait, the exact
		/// water.</summary>
		public static string SowConfirm(string CropName, string FieldName, int Rows, int Drams)
		{
			string field = string.IsNullOrEmpty(FieldName) ? "field" : FieldName;
			string crop = string.IsNullOrEmpty(CropName) ? "the crop" : CropName;
			StringBuilder text = new StringBuilder();
			text.Append("Sow the ").Append(field).Append(" with ").Append(crop).Append("?\n\n");
			text.Append(Rows).Append((Rows == 1) ? " row goes into the ground" : " rows go into the ground");
			text.Append(", and ").Append(Drams).Append((Drams == 1) ? " dram is poured" : " drams are poured");
			text.Append(" over the seedbed. It comes ripe in ").Append(CropDays).Append(" days, and again every ");
			text.Append(CropDays).Append(" days after that, whether or not you are standing here.\n\n");
			text.Append("The seed is yours until you take it back out.");
			return text.ToString();
		}

		/// <summary>The line both registers carry when a field is committed.</summary>
		public static string SownChronicle(string CropName, string FieldName, string SettlementName)
		{
			return "the " + (string.IsNullOrEmpty(FieldName) ? "field" : FieldName) + " at "
				+ (string.IsNullOrEmpty(SettlementName) ? "the settlement" : SettlementName)
				+ " was sown with " + (string.IsNullOrEmpty(CropName) ? "the season's crop" : CropName);
		}

		/// <summary>
		/// The one chronicle line a whole season of gatherings gets. A field harvested twelve
		/// times while the founder was away is one sentence with a count in it, never twelve
		/// &mdash; the register holds two hundred entries and a farm would eat all of them.
		/// </summary>
		/// <param name="Cycles">Gatherings resolved at once. One reads as one harvest.</param>
		/// <param name="Yield">Servings they brought in between them.</param>
		/// <param name="SettlementName">The city.</param>
		/// <param name="DaysAgo">Whole days since the LAST of them came due. Zero and below read
		/// as "today", the same shape <c>KingdomLocusRules.PassageWhen</c> keeps.</param>
		public static string HarvestChronicle(int Cycles, int Yield, string SettlementName, int DaysAgo)
		{
			string place = string.IsNullOrEmpty(SettlementName) ? "the settlement" : SettlementName;
			string when = (DaysAgo <= 0)
				? ""
				: ((DaysAgo == 1) ? ", the last of it a day before you saw it" : (", the last of it " + DaysAgo + " days before you saw it"));
			if (Cycles <= 1)
			{
				return "the fields of " + place + " were gathered in for " + Yield + (Yield == 1 ? " serving" : " servings") + when;
			}
			return Cycles + " harvests came in at " + place + " for " + Yield + (Yield == 1 ? " serving" : " servings") + when;
		}

		/// <summary>The same, in the ledger's shorter voice.</summary>
		public static string HarvestNote(int Cycles, int Yield, int Delivered, int Pending, int Lost)
		{
			StringBuilder text = new StringBuilder();
			text.Append((Cycles <= 1) ? "The harvest came in" : (Cycles + " harvests came in"));
			text.Append(" for ").Append(Yield).Append((Yield == 1) ? " serving" : " servings").Append(". ");
			if (Delivered > 0)
			{
				text.Append(Delivered).Append(" went into the larders");
			}
			else
			{
				text.Append("None of it reached a larder here");
			}
			if (Pending > 0)
			{
				text.Append("; ").Append(Pending).Append(" is on the road to the city's stores");
			}
			if (Lost > 0)
			{
				text.Append("; ").Append(Lost).Append(" was left in the field for want of room");
			}
			text.Append(".");
			return text.ToString();
		}

		/// <summary>The line a cross-zone load gets when it finally reaches a pantry.</summary>
		public static string DeliveryNote(int Delivered, string SettlementName)
		{
			return Delivered + (Delivered == 1 ? " serving" : " servings") + " of the harvest reached the larders of "
				+ (string.IsNullOrEmpty(SettlementName) ? "the settlement" : SettlementName) + ".";
		}

		/// <summary>The line the founder reads when they take their own seed back out.</summary>
		public static string WithdrawnNote(string CropName, string FieldName, string SettlementName)
		{
			return "The " + (string.IsNullOrEmpty(FieldName) ? "field" : FieldName) + " at "
				+ (string.IsNullOrEmpty(SettlementName) ? "the settlement" : SettlementName)
				+ " is turned back to bare ground, and the " + (string.IsNullOrEmpty(CropName) ? "seed" : CropName)
				+ " seed is yours again. It grows nothing until you sow it once more.";
		}

	}
}
