using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	public partial class r_KingdomImprovement
	{
		internal static bool FailHandover(r_KingdomImprovement Receipt, string Failure)
		{
			if (Receipt != null)
			{
				Receipt.HandoverQuarantined = true;
				Receipt.HandoverFailure = Failure != null && Failure.Length > 2048
					? Failure.Substring(0, 2048) : Failure;
			}
			return false;
		}

		private static string EncodeEmptyLiquid()
		{
			return "v1";
		}

		private static bool ExactLiquidReceiptShape(r_KingdomImprovement Receipt)
		{
			if (Receipt.HandoverPhase == 0) return true;
			if (Receipt.HandoverSourceVolumeBefore == 0)
				return Receipt.HandoverPhase == 3
					&& Receipt.HandoverSourceVolumeAfter == 0;
			if (Receipt.HandoverSourceVolumeBefore < 0
				|| Receipt.HandoverSourceVolumeAfter != 0
				|| Receipt.HandoverTargetVolumeBefore < 0) return false;
			long expected = (long)Receipt.HandoverTargetVolumeBefore
				+ Receipt.HandoverSourceVolumeBefore;
			if (expected > int.MaxValue
				|| (Receipt.HandoverTargetCapacity != -1
					&& Receipt.HandoverTargetCapacity < expected)
				|| !TryFrozenLiquid(Receipt.HandoverSourceComposition,
					Receipt.HandoverSourceVolumeBefore, out _)
				|| (Receipt.HandoverTargetVolumeBefore == 0
					? Receipt.HandoverTargetCompositionBefore != EncodeEmptyLiquid()
					: !TryFrozenLiquid(Receipt.HandoverTargetCompositionBefore,
						Receipt.HandoverTargetVolumeBefore, out _))) return false;
			if (Receipt.HandoverPhase < 3)
				return Receipt.HandoverTargetVolumeAfter == -1
					&& Receipt.HandoverTargetCompositionAfter == null;
			return Receipt.HandoverTargetVolumeAfter == (int)expected
				&& TryFrozenLiquid(Receipt.HandoverTargetCompositionAfter,
					(int)expected, out _);
		}

		private static string EncodeLiquid(LiquidVolume Volume)
		{
			if (Volume == null || Volume.Volume <= 0) return EncodeEmptyLiquid();
			if (Volume.ComponentLiquids == null || Volume.ComponentLiquids.Count == 0
				|| Volume.ComponentLiquids.Count > MaxHandoverComponents) return null;
			List<string> keys = new List<string>(Volume.ComponentLiquids.Keys);
			keys.Sort(StringComparer.Ordinal);
			StringBuilder text = new StringBuilder("v1");
			int total = 0;
			for (int i = 0; i < keys.Count; i++)
			{
				int proportion = Volume.ComponentLiquids[keys[i]];
				if (string.IsNullOrEmpty(keys[i]) || keys[i].Length > 128
					|| proportion <= 0 || proportion > 1000) return null;
				total += proportion;
				text.Append(';').Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(keys[i])))
					.Append(',').Append(proportion.ToString(
						CultureInfo.InvariantCulture));
				if (text.Length > MaxHandoverText) return null;
			}
			return total == 1000 ? text.ToString() : null;
		}

		private static bool TryFrozenLiquid(string Text, int Volume, out LiquidVolume Frozen)
		{
			Frozen = null;
			if (Volume <= 0 || string.IsNullOrEmpty(Text) || Text.Length > MaxHandoverText) return false;
			string[] terms = Text.Split(';');
			if (terms.Length < 2 || terms.Length - 1 > MaxHandoverComponents
				|| terms[0] != "v1") return false;
			Dictionary<string, int> components = new Dictionary<string, int>();
			int total = 0;
			for (int i = 1; i < terms.Length; i++)
			{
				string[] pair = terms[i].Split(',');
				int proportion;
				string key;
				try { key = Encoding.UTF8.GetString(Convert.FromBase64String(pair[0])); }
				catch { return false; }
				if (pair.Length != 2 || string.IsNullOrEmpty(key) || key.Length > 128
					|| components.ContainsKey(key)
					|| !int.TryParse(pair[1], NumberStyles.None, CultureInfo.InvariantCulture,
						out proportion) || proportion <= 0 || proportion > 1000) return false;
				components.Add(key, proportion);
				total += proportion;
			}
			if (total != 1000) return false;
			Frozen = new LiquidVolume();
			Frozen.Volume = Volume;
			Frozen.ComponentLiquids = components;
			return EncodeLiquid(Frozen) == Text;
		}

		/// <summary>
		/// Puts the settlement's intent for this work on the work itself, so the founder can
		/// read it by looking at the thing rather than only in the Charter.
		/// </summary>
		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			if (HandoverQuarantined)
			{
				E.Postfix.Append("\n{{r|This improvement handover requires inspection: ")
					.Append(HandoverFailure ?? "its physical receipt is ambiguous")
					.Append(".}}");
			}
			else if (Working)
			{
				E.Postfix.Append("\n{{rules|The settlement is raising this into ")
					.Append(KingdomUpgrade.DisplayNameOf(SuccessorKey))
					.Append(".}}");
			}
			else if (Held)
			{
				E.Postfix.Append("\n{{rules|The settlement will leave this as it is.}}");
			}
			return base.HandleEvent(E);
		}

		/// <summary>
		/// Watches for the scaffold's own completion and moves everything across when it comes.
		/// Called once a turn while an improvement is under way, and does nothing at all until
		/// the scaffold is gone, which is cheap.
		/// </summary>
		/// <param name="TimeTick">Engine tick, for the abandonment grace period.</param>
	}
}
