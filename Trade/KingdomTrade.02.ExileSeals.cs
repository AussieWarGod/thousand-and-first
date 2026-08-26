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
		private static bool ExactSettlementTopology(List<string> Current,
			List<string> Expected)
		{
			if (Current == null || Expected == null || Current.Count != Expected.Count)
				return false;
			for (int i = 0; i < Expected.Count; i++)
				if (!string.Equals(Current[i], Expected[i], StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool TryCaptureExileCoreSeal(KingdomSystem System,
			out TradeExileCoreSeal Seal, out string Failure)
		{
			Seal = null;
			Failure = null;
			try
			{
				if (System?.City == null || !KingdomRealmArchive.TryCurrentGraphHash(System,
					out string graphHash, out Failure)
					|| !TryExileReferenceRoots(System, out List<object> roots)
					|| !KingdomTradeRules.TryCaptureExactReferenceSeal(roots,
						out KingdomTradeReferenceSeal references))
				{
					Failure = Failure ?? "exact Core reference graph could not be frozen";
					return false;
				}
				Seal = new TradeExileCoreSeal
				{
					System = System,
					City = System.City,
					Away = System.Away,
					GraphHash = graphHash,
					References = references
				};
				return true;
			}
			catch (Exception error)
			{
				Failure = error.Message;
				Seal = null;
				return false;
			}
		}

		private static bool ExactExileCoreSeal(KingdomSystem System, TradeExileCoreSeal Seal)
		{
			try
			{
				return Seal != null && ReferenceEquals(System, Seal.System)
					&& ReferenceEquals(System.City, Seal.City)
					&& ReferenceEquals(System.Away, Seal.Away)
					&& KingdomRealmArchive.TryCurrentGraphHash(System, out string graphHash,
						out string _)
					&& string.Equals(graphHash, Seal.GraphHash, StringComparison.Ordinal)
					&& TryExileReferenceRoots(System, out List<object> roots)
					&& KingdomTradeRules.ExactReferenceSeal(roots, Seal.References);
			}
			catch { return false; }
		}

		private static bool TryExileReferenceRoots(KingdomSystem System,
			out List<object> Roots)
		{
			Roots = new List<object>();
			if (System == null) return false;
			try
			{
				// Capture every mutable seated-settlement field by the same field-name contract
				// KingdomSystem.Capture uses, plus every realm-level mutable root in TAG1.
				FieldInfo[] settlement = typeof(KingdomSettlement).GetFields(
					BindingFlags.Instance | BindingFlags.Public);
				Array.Sort(settlement,
					(left, right) => string.CompareOrdinal(left.Name, right.Name));
				for (int i = 0; i < settlement.Length; i++)
				{
					FieldInfo archived = settlement[i];
					if (archived.IsStatic || archived.FieldType.IsValueType
						|| archived.FieldType == typeof(string)
						|| archived.GetCustomAttribute<NonSerializedAttribute>() != null) continue;
					FieldInfo live = typeof(KingdomSystem).GetField(archived.Name,
						BindingFlags.Instance | BindingFlags.Public);
					if (live == null || live.FieldType != archived.FieldType) return false;
					Roots.Add(live.GetValue(System));
				}
				Roots.Add(System.City);
				Roots.Add(System.Away);
				Roots.Add(System.Seceded);
				Roots.Add(System.CarryBook);
				Roots.Add(System.Bindings);
				Roots.Add(System.Jobs);
				Roots.Add(System.Standings);
				Roots.Add(System.ChronicleEntries);
				Roots.Add(System.OutsiderEntries);
				Roots.Add(System.Haul);
				return Roots.Count <= 256;
			}
			catch { Roots = null; return false; }
		}

		/// <summary>
		/// Freezes only the exact active settlement ground already indexed for this trade lease.
		/// Cached zones are not transaction participants: scanning them made one local delivery
		/// proportional to every zone the player had visited and silently created foreign surveys
		/// inside the bound semantic pass. A caller standing on unavailable or non-active ground
		/// receives no witness and therefore defers without touching physical authority.
		/// </summary>
	}
}
