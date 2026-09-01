using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomTrade
	{
		public static void OnZoneActivated(KingdomSystem System, Zone Z,
			KingdomSurvey Shared = null)
		{
			if (!KingdomMaster.AutomaticWorkAllowed(System)) return;
			TradeLease lease;
			if (!TryEnter(System, out lease)) return;
			using (lease)
			{
				OnZoneActivatedCore(System, Z, Shared);
			}
		}

		private static void OnZoneActivatedCore(KingdomSystem System, Zone Z,
			KingdomSurvey Shared)
		{
			if (System == null || !System.Founded || Z == null
				|| !System.ClaimedZones.Contains(Z.ZoneID)) return;
			long now = The.Game.TimeTicks;
			KingdomTradeBook book = EnsureBook(System);
			if (book == null) return;
			KingdomTradeOptionAction option = ApplyOption(book, Enabled, now);
			if (!KingdomTradeRules.BookUsable(book)) return;
			KingdomSurvey survey = Shared ?? KingdomSurvey.Take(Z, System);

			if (book.OpenOperation != null)
			{
				if (!Enabled && book.OpenOperation.Phase == KingdomTradePhase.Prepared)
					return;
				ContinueOperation(System, book, Z, survey, now);
				if (book.OpenOperation != null || !Enabled) return;
			}
			if (!Enabled) return;
			if (book.RestampPending)
			{
				RestampTradeClocks(book, now);
				return;
			}
			if (option == KingdomTradeOptionAction.EnableAndRestamp) return;

			KingdomTradeManifestState manifest = book.Manifest;
			if (manifest != null && manifest.Status == KingdomTradeManifestStatus.InFlight)
			{
				if (KingdomManifestRules.ManifestExpired(now, manifest.DeadlineTick))
				{
					PrepareManifestClockOperation(System, book, manifest, Z, now);
					ContinueOperation(System, book, Z, survey, now);
					return;
				}
				string seatId = System.City?.SettlementId;
				if (string.Equals(manifest.DestinationId, seatId, StringComparison.Ordinal)
					&& string.Equals(manifest.DestinationName, System.SeatName,
						StringComparison.Ordinal))
				{
					PrepareManifestDelivery(System, book, manifest, Z, now);
					ContinueOperation(System, book, Z, survey, now);
					return;
				}
			}

			int due = KingdomTradeRules.DueCharterIndex(book, now);
			if (due >= 0 && PrepareCharterDelivery(System, book, book.Charters[due], Z,
				survey, now))
			{
				ContinueOperation(System, book, Z, survey, now);
			}
		}

		/// <summary>Publishes the exact route and debit intent before touching any source vessel.</summary>
		public static bool TryLoadManifest(KingdomSystem System, Zone Z, int Amount,
			string OriginName, string DestinationName, out string Failure)
		{
			if (!KingdomMaster.NewWorkAllowed(System))
			{
				Failure = "Settlement simulation is paused; no new manifest was loaded.";
				return false;
			}
			TradeLease lease;
			if (!TryEnter(System, out lease))
			{
				Failure = "Another trade callback is already in flight; no manifest was changed.";
				return false;
			}
			using (lease)
			{
				return TryLoadManifestCore(System, Z, Amount, OriginName, DestinationName,
					out Failure);
			}
		}

		private static bool TryLoadManifestCore(KingdomSystem System, Zone Z, int Amount,
			string OriginName, string DestinationName, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded || Z == null
				|| !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "Manifests are loaded standing on the kingdom's own ground.";
				return false;
			}
			if (!Enabled)
			{
				Failure = "Trade is disabled. No new manifest is loaded.";
				return false;
			}
			if (Amount <= 0 || Amount > KingdomManifestRules.MaximumManifestDrams
				|| string.IsNullOrEmpty(OriginName) || string.IsNullOrEmpty(DestinationName))
			{
				Failure = "The manifest amount or route cannot be recorded exactly.";
				return false;
			}
			long now = The.Game.TimeTicks;
			KingdomTradeBook book = EnsureBook(System);
			if (book == null)
			{
				Failure = "The trade book uses an unknown or quarantined schema.";
				return false;
			}
			if (book.OpenOperation != null)
			{
				Failure = "Another trade receipt is still being reconciled.";
				return false;
			}
			ApplyOption(book, true, now);
			if (!KingdomTradeRules.BookUsable(book))
			{
				Failure = book.SchemaFault ?? "Trade option evidence is not authoritative.";
				return false;
			}
			if (book.Manifest != null)
			{
				Failure = "Another manifest is already on the road or held for inspection.";
				return false;
			}
			KingdomTradeOperation operation = KingdomTradeRules.NewOperation(book,
				KingdomTradeOperationKind.ManifestLoad, now);
			if (operation == null)
			{
				Failure = "The trade ledger cannot open another durable receipt.";
				return false;
			}
			if (!BindOperationSettlement(System, book, operation, Z))
			{
				Quarantine(operation, "The manifest could not bind its exact settlement and zone.");
				FinalizeQuarantine(System, book, operation, now, null);
				Failure = operation.Fault;
				return false;
			}
			operation.ManifestId = KingdomTradeRules.ManifestId(operation.Id);
			if (System.City == null ||
				!System.TryFindNonSeatSettlementByName(DestinationName,
					out KingdomSettlement destination) || destination.City == null
				|| !string.Equals(OriginName, System.SeatName, StringComparison.Ordinal)
				|| !KingdomTradeRules.IdentityContainsSettlement(book, System.City.SettlementId)
				|| !KingdomTradeRules.IdentityContainsSettlement(book,
					destination.City.SettlementId))
			{
				Quarantine(operation,
					"The manifest route could not bind both exact city identities.");
				FinalizeQuarantine(System, book, operation, now, null);
				Failure = operation.Fault;
				return false;
			}
			operation.OriginId = System.City.SettlementId;
			operation.OriginName = OriginName;
			operation.DestinationId = destination.City.SettlementId;
			operation.DestinationName = DestinationName;
			operation.ManifestLoadedTick = now;
			operation.ManifestDeadlineTick = KingdomTradeRules.SaturatingAdd(now,
				KingdomManifestRules.ManifestWindowTicks);
			operation.WaterDirection = KingdomTradeWaterDirection.Debit;
			operation.RequestedWater = Amount;
			KingdomSurvey survey = KingdomSurvey.Take(Z, System);
			ContinueOperation(System, book, Z, survey, now);
			KingdomTradeManifestState manifest = book.Manifest;
			bool success = manifest != null
				&& string.Equals(manifest.Id, operation.ManifestId, StringComparison.Ordinal)
				&& manifest.Status == KingdomTradeManifestStatus.InFlight
				&& manifest.EscrowDrams == Amount;
			if (!success)
			{
				Failure = operation.Fault ?? "The exact source-vessel debit could not be proved; it was not retried.";
			}
			return success;
		}

		/// <summary>Compatibility facade for the Charter. A lapsed load is retained, not destroyed.</summary>
		public static KingdomManifest ExpireManifestIfStale(KingdomSystem System,
			Zone Here, long Now)
		{
			if (!KingdomMaster.AutomaticWorkAllowed(System)) return null;
			TradeLease lease;
			if (!TryEnter(System, out lease)) return null;
			using (lease)
			{
				return ExpireManifestIfStaleCore(System, Here, Now);
			}
		}

		private static KingdomManifest ExpireManifestIfStaleCore(KingdomSystem System,
			Zone Here, long Now)
		{
			KingdomTradeBook book = EnsureBook(System);
			KingdomTradeManifestState manifest = book?.Manifest;
			if (manifest == null || manifest.Status != KingdomTradeManifestStatus.InFlight
				|| !KingdomManifestRules.ManifestExpired(Now, manifest.DeadlineTick)) return null;
			if (book.OpenOperation != null) return null;
			bool lapse = manifest.TurnedBack;
			KingdomManifest answer = lapse ? LegacyManifestSnapshot(manifest) : null;
			PrepareManifestClockOperation(System, book, manifest, Here, Now);
			ContinueOperation(System, book, Here, Here == null ? null : KingdomSurvey.Take(Here, System), Now);
			return answer;
		}

		/// <summary>
		/// Atomically replaces live Trade authority with a durable exact exile receipt. Core must
		/// call this before changing realm identity, settlement topology, legacy rows, or chronicles.
		/// False leaves the current TradeBook graph and bytes untouched.
		/// </summary>
	}
}
