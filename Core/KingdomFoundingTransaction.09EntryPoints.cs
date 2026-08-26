using System;
using System.Collections.Generic;
using System.Reflection;
using Qud.API;
using XRL;
using XRL.Language;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomFoundingTransaction
	{
		public static KingdomFoundingResult BeginFirst(r_FounderBasin Basin,
			GameObject Actor, Zone Site, string Name)
		{
			if (!TryEnterFounding(null, Basin, out var lease))
			{
				return ReentryRefusal();
			}
			using (lease)
			{
				KingdomFoundingResult start = Begin(Basin, Actor, Site,
					KingdomFoundingKind.FirstCity, Name, null, null, null);
				if (start.Outcome != KingdomFoundingOutcome.Committed)
				{
					return start;
				}
				if (!lease.Bind(Basin.PendingAuthority, Basin))
				{
					return Result(KingdomFoundingOutcome.RecoverableFailure,
						KingdomFoundingWaterDisposition.HeldForRecovery,
						KingdomFoundingProjection.Water,
						"The exact paid authority could not enter its synchronous guard.");
				}
				return Run(Basin, Actor, Site);
			}
		}

		public static KingdomFoundingResult BeginSecond(r_FounderBasin Basin,
			GameObject Actor, Zone Site, string Name, string Vocation)
		{
			if (!TryEnterFounding(null, Basin, out var lease))
			{
				return ReentryRefusal();
			}
			using (lease)
			{
				KingdomFoundingResult start = Begin(Basin, Actor, Site,
					KingdomFoundingKind.SecondCity, Name, Vocation, null, null);
				if (start.Outcome != KingdomFoundingOutcome.Committed)
				{
					return start;
				}
				if (!lease.Bind(Basin.PendingAuthority, Basin))
				{
					return Result(KingdomFoundingOutcome.RecoverableFailure,
						KingdomFoundingWaterDisposition.HeldForRecovery,
						KingdomFoundingProjection.Water,
						"The exact paid authority could not enter its synchronous guard.");
				}
				return Run(Basin, Actor, Site);
			}
		}

		public static KingdomFoundingResult BeginVillageCharter(r_FounderBasin Basin,
			GameObject Actor, Zone Site, string FactionName, string DisplayName)
		{
			if (!TryEnterFounding(null, Basin, out var lease))
			{
				return ReentryRefusal();
			}
			using (lease)
			{
				KingdomFoundingResult start = Begin(Basin, Actor, Site,
					KingdomFoundingKind.VillageCharter, DisplayName, null,
					FactionName, DisplayName);
				if (start.Outcome != KingdomFoundingOutcome.Committed)
				{
					return start;
				}
				if (!lease.Bind(Basin.PendingAuthority, Basin))
				{
					return Result(KingdomFoundingOutcome.RecoverableFailure,
						KingdomFoundingWaterDisposition.HeldForRecovery,
						KingdomFoundingProjection.Water,
						"The exact paid authority could not enter its synchronous guard.");
				}
				return Run(Basin, Actor, Site);
			}
		}

		/// <summary>Resumes the one serialized receipt carried by <paramref name="Basin"/>.</summary>
		public static KingdomFoundingResult Resume(r_FounderBasin Basin,
			GameObject Actor, Zone Site)
		{
			if (!TryEnterFounding(Basin?.PendingAuthority, Basin, out var lease))
			{
				return ReentryRefusal();
			}
			using (lease)
			{
				return ResumeGuarded(Basin, Actor, Site, lease);
			}
		}

		private static KingdomFoundingResult ResumeGuarded(r_FounderBasin Basin,
			GameObject Actor, Zone Site, FoundingLease Lease)
		{
			KingdomFoundingReceiptNormalization normalization = NormalizeReceipt(Basin);
			if (normalization == KingdomFoundingReceiptNormalization.ClearStaged)
			{
				if (TryClearStagedReceipt(Basin, Site))
				{
					return Result(KingdomFoundingOutcome.Refused,
						KingdomFoundingWaterDisposition.Untouched,
						KingdomFoundingProjection.None,
						"The interrupted staging ended before any water was spent; its exact reservation was cleared.");
				}
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.RestorationFailed,
					KingdomFoundingProjection.None,
					"The unpaid staged receipt no longer matches its exact basin, site, or reservation and was quarantined.");
			}
			if (normalization == KingdomFoundingReceiptNormalization.Clean)
			{
				if (!SafeClearReceipt(Basin))
				{
					return Result(KingdomFoundingOutcome.RecoverableFailure,
						KingdomFoundingWaterDisposition.RestorationFailed,
						KingdomFoundingProjection.None,
						"The empty founding receipt could not prove every receipt field absent.");
				}
				return Result(KingdomFoundingOutcome.Refused,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None, "There is no interrupted rite to resume.");
			}
			if (normalization == KingdomFoundingReceiptNormalization.Quarantine)
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.RestorationFailed,
					KingdomFoundingProjection.None,
					"The founding receipt header is malformed and has been quarantined without another debit.");
			}
			if (!Lease.Bind(Basin.PendingAuthority, Basin))
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.HeldForRecovery,
					KingdomFoundingProjection.None,
					"The exact pending authority could not enter its synchronous guard.");
			}
			return Run(Basin, Actor, Site);
		}

		/// <summary>
		/// A temporary Committed result means only that Begin established the exact water receipt;
		/// the public Begin* methods immediately continue into Run and never expose it to UI.
		/// </summary>
	}
}
