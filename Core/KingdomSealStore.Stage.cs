using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	internal sealed partial class KingdomSealStore
	{
		internal bool TryStage(KingdomSealRecord Record, out string Failure)
		{
			Failure = "";
			if (Record == null || !KingdomSealReceipt.ValidId(Record.OriginGameId))
			{
				Failure = "the record names no valid origin";
				return false;
			}
			FileStream gate;
			if (!TryLockStage(Record.OriginGameId, out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				return TryStageLocked(Record, out Failure);
			}
		}

		private bool TryStageLocked(KingdomSealRecord Record, out string Failure)
		{
			Failure = "";
			if (Record == null || Record.Status == KingdomSealStatus.Promoted)
			{
				Failure = "only a living, terminal, or retired record is a stage";
				return false;
			}
			if (!KingdomSealReceipt.ValidId(Record.OriginGameId))
			{
				Failure = "the record names no valid origin";
				return false;
			}
			string slotA = StagePath(Record.OriginGameId, 'a');
			string slotB = StagePath(Record.OriginGameId, 'b');
			KingdomSealRecord a = ReadSlot(slotA);
			KingdomSealRecord b = ReadSlot(slotB);
			if ((a != null && a.OriginGameId != Record.OriginGameId)
				|| (b != null && b.OriginGameId != Record.OriginGameId)
				|| !SameStageIdentity(a, b))
			{
				Failure = "the origin journal contains a record that does not match its filename or other slot";
				return false;
			}
			KingdomSealRecord best = Best(a, b);
			if (best != null)
			{
				if (best.LineageId != Record.LineageId || best.LegacyId != Record.LegacyId
					|| best.Generation != Record.Generation)
				{
					Failure = "an origin journal cannot change its lineage, legacy, or generation";
					return false;
				}
				if (best.Revision > Record.Revision)
				{
					Failure = "the stage revision would go backwards";
					return false;
				}
				if (best.Revision == Record.Revision)
				{
					if (SameRecord(best, Record))
					{
						return true;
					}
					Failure = "the stage revision already names different facts";
					return false;
				}
				if (best.Status == KingdomSealStatus.Retired)
				{
					Failure = "an explicitly retired generation cannot be rewritten";
					return false;
				}
			}
			string target = (best != null && object.ReferenceEquals(best, a)) ? slotB : slotA;
			return TryWriteSeal(target, Record, true, out Failure);
		}


		internal KingdomSealRecord ReadStage(string OriginGameId)
		{
			if (!KingdomSealReceipt.ValidId(OriginGameId))
			{
				return null;
			}
			KingdomSealRecord a = ReadSlot(StagePath(OriginGameId, 'a'));
			KingdomSealRecord b = ReadSlot(StagePath(OriginGameId, 'b'));
			if (a != null && a.OriginGameId != OriginGameId)
			{
				a = null;
			}
			if (b != null && b.OriginGameId != OriginGameId)
			{
				b = null;
			}
			if (SameStageIdentity(a, b))
			{
				return Best(a, b);
			}
			KingdomSealRecord newer;
			if (!TryRecoverableGenerationPair(a, b, out newer))
			{
				return null;
			}
			return newer;
		}

		internal List<string> StagedOrigins(out int Refused)
		{
			Refused = 0;
			List<string> origins = new List<string>();
			HashSet<string> seen = new HashSet<string>();
			bool overflow;
			int refusedJunk;
			foreach (string path in Files(StagesFolder, SealExtension,
				MaxStageFilesScanned, out overflow, out refusedJunk))
			{
				string name = Path.GetFileName(path);
				if (!name.EndsWith(SealExtension, StringComparison.Ordinal))
				{
					Refused++;
					continue;
				}
				string stem = name.Substring(0, name.Length - SealExtension.Length);
				int slotCut = stem.LastIndexOf(".", StringComparison.Ordinal);
				if (slotCut <= 0 || stem.Length - slotCut != 2
					|| (stem[slotCut + 1] != 'a' && stem[slotCut + 1] != 'b'))
				{
					Refused++;
					continue;
				}
				string origin = stem.Substring(0, slotCut);
				if (!KingdomSealReceipt.ValidId(origin))
				{
					Refused++;
					continue;
				}
				if (seen.Add(origin))
				{
					origins.Add(origin);
				}
			}
			if (overflow)
			{
				Refused++;
			}
			Refused += refusedJunk;
			origins.Sort(StringComparer.Ordinal);
			return origins;
		}

	}
}
