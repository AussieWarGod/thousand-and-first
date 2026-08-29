using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Pure canonical codec and monotone transitions for founding-heart retirement.</summary>
	public static class KingdomFoundingHeartTerminalRules
	{
		private const int MaximumText = 1024;

		public static bool TryCreate(string TransactionId, string CompletionSeal, string ZoneId,
			string PredecessorId, string FinalId, string Blueprint, string BuildKey, string PlotId,
			int X, int Y, out KingdomFoundingHeartTerminalPlan Plan)
		{
			Plan = new KingdomFoundingHeartTerminalPlan
			{
				TransactionId = TransactionId, CompletionSeal = CompletionSeal, ZoneId = ZoneId,
				PredecessorId = PredecessorId, FinalId = FinalId, Blueprint = Blueprint,
				BuildKey = BuildKey, PlotId = PlotId, X = X, Y = Y,
				Phase = KingdomFoundingHeartTerminalPhase.OutputPrepared,
				Raising = KingdomFoundingHeartSinkDisposition.Pending,
				Heart = KingdomFoundingHeartSinkDisposition.Pending
			};
			if (Valid(Plan)) return true;
			Plan = null;
			return false;
		}

		public static bool Valid(KingdomFoundingHeartTerminalPlan Plan)
		{
			if (Plan == null || !LowerHex32(Plan.TransactionId)
				|| !Token(Plan.CompletionSeal) || !Token(Plan.ZoneId)
				|| !Token(Plan.PredecessorId) || !Token(Plan.FinalId)
				|| !Token(Plan.Blueprint) || !Token(Plan.BuildKey) || !Token(Plan.PlotId)
				|| Plan.X < 0 || Plan.X > 4096 || Plan.Y < 0 || Plan.Y > 4096
				|| Plan.Phase < KingdomFoundingHeartTerminalPhase.OutputPrepared
				|| Plan.Phase > KingdomFoundingHeartTerminalPhase.EffectsSettled
				|| !Sink(Plan.Raising) || !Sink(Plan.Heart)) return false;
			if (Plan.Phase < KingdomFoundingHeartTerminalPhase.EffectsAttempting)
				return Plan.Raising == KingdomFoundingHeartSinkDisposition.Pending
					&& Plan.Heart == KingdomFoundingHeartSinkDisposition.Pending;
			if (Plan.Phase == KingdomFoundingHeartTerminalPhase.EffectsSettled)
				return Terminal(Plan.Raising) && Terminal(Plan.Heart);
			return true;
		}

		public static bool SameBinding(KingdomFoundingHeartTerminalPlan A,
			KingdomFoundingHeartTerminalPlan B)
		{
			return Valid(A) && Valid(B) && A.TransactionId == B.TransactionId
				&& A.CompletionSeal == B.CompletionSeal && A.ZoneId == B.ZoneId
				&& A.PredecessorId == B.PredecessorId && A.FinalId == B.FinalId
				&& A.Blueprint == B.Blueprint && A.BuildKey == B.BuildKey
				&& A.PlotId == B.PlotId && A.X == B.X && A.Y == B.Y;
		}

		public static bool TryAdvancePhase(KingdomFoundingHeartTerminalPlan Plan,
			KingdomFoundingHeartTerminalPhase Expected,
			KingdomFoundingHeartTerminalPhase Next)
		{
			if (!Valid(Plan) || Plan.Phase != Expected || (int)Next != (int)Expected + 1)
				return false;
			if (Next == KingdomFoundingHeartTerminalPhase.EffectsSettled
				&& (!Terminal(Plan.Raising) || !Terminal(Plan.Heart))) return false;
			Plan.Phase = Next;
			return Valid(Plan);
		}

		public static bool TryAdvanceSink(KingdomFoundingHeartTerminalPlan Plan, bool Heart,
			KingdomFoundingHeartSinkDisposition Expected,
			KingdomFoundingHeartSinkDisposition Next)
		{
			if (!Valid(Plan) || Plan.Phase != KingdomFoundingHeartTerminalPhase.EffectsAttempting
				|| (Expected == KingdomFoundingHeartSinkDisposition.Pending
					? Next != KingdomFoundingHeartSinkDisposition.Attempting
					: Expected != KingdomFoundingHeartSinkDisposition.Attempting
						|| (Next != KingdomFoundingHeartSinkDisposition.Settled
							&& Next != KingdomFoundingHeartSinkDisposition.Lost))) return false;
			if (Heart)
			{
				if (Plan.Heart != Expected) return false;
				Plan.Heart = Next;
			}
			else
			{
				if (Plan.Raising != Expected) return false;
				Plan.Raising = Next;
			}
			return Valid(Plan);
		}

		public static bool ExactRemovalTombstone(bool CallbackReturned, bool CallbackResult,
			bool PredecessorValid, bool ActiveIdAbsent, bool ExactIdentityTombstone)
		{
			return CallbackReturned && CallbackResult && !PredecessorValid
				&& ActiveIdAbsent && ExactIdentityTombstone;
		}

		/// <summary>Add return/throw is not authority; exact landed topology and custody are.</summary>
		public static bool ExactAddCut(bool CallbackReturned, bool ReturnedExact,
			bool ExactEndpoint, bool CanonicalRootPresent)
		{
			return ExactEndpoint && CanonicalRootPresent;
		}

		public static string Encode(KingdomFoundingHeartTerminalPlan Plan)
		{
			if (!Valid(Plan)) return null;
			string body = "ht1|" + B64(Plan.TransactionId) + "|" + B64(Plan.CompletionSeal)
				+ "|" + B64(Plan.ZoneId) + "|" + B64(Plan.PredecessorId) + "|"
				+ B64(Plan.FinalId) + "|" + B64(Plan.Blueprint) + "|" + B64(Plan.BuildKey)
				+ "|" + B64(Plan.PlotId) + "|" + N(Plan.X) + "|" + N(Plan.Y) + "|"
				+ N((int)Plan.Phase) + "|" + N((int)Plan.Raising) + "|" + N((int)Plan.Heart);
			return body + "|" + Digest(body);
		}

		public static bool TryDecode(string Encoded, out KingdomFoundingHeartTerminalPlan Plan)
		{
			Plan = null;
			if (string.IsNullOrEmpty(Encoded) || Encoded.Length > MaximumText * 12) return false;
			string[] p = Encoded.Split('|');
			if (p.Length != 15 || p[0] != "ht1") return false;
			string body = string.Join("|", p, 0, 14);
			if (p[14] != Digest(body)) return false;
			try
			{
				Plan = new KingdomFoundingHeartTerminalPlan
				{
					TransactionId = Text(p[1]), CompletionSeal = Text(p[2]), ZoneId = Text(p[3]),
					PredecessorId = Text(p[4]), FinalId = Text(p[5]), Blueprint = Text(p[6]),
					BuildKey = Text(p[7]), PlotId = Text(p[8]), X = Number(p[9]), Y = Number(p[10]),
					Phase = (KingdomFoundingHeartTerminalPhase)Number(p[11]),
					Raising = (KingdomFoundingHeartSinkDisposition)Number(p[12]),
					Heart = (KingdomFoundingHeartSinkDisposition)Number(p[13])
				};
				if (!Valid(Plan) || Encode(Plan) != Encoded) Plan = null;
			}
			catch { Plan = null; }
			return Plan != null;
		}

		private static bool Sink(KingdomFoundingHeartSinkDisposition Value)
		{
			return Value >= KingdomFoundingHeartSinkDisposition.Pending
				&& Value <= KingdomFoundingHeartSinkDisposition.Lost;
		}

		private static bool Terminal(KingdomFoundingHeartSinkDisposition Value)
		{
			return Value == KingdomFoundingHeartSinkDisposition.Settled
				|| Value == KingdomFoundingHeartSinkDisposition.Lost;
		}

		private static bool Token(string Value)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= MaximumText;
		}

		private static bool LowerHex32(string Value)
		{
			if (Value == null || Value.Length != 32) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9')
					|| Value[i] >= 'a' && Value[i] <= 'f')) return false;
			return true;
		}

		private static string B64(string Value) => Convert.ToBase64String(
			Encoding.UTF8.GetBytes(Value ?? ""));
		private static string Text(string Value) => Encoding.UTF8.GetString(
			Convert.FromBase64String(Value));
		private static string N(int Value) => Value.ToString(CultureInfo.InvariantCulture);
		private static int Number(string Value) => int.Parse(Value, NumberStyles.Integer,
			CultureInfo.InvariantCulture);

		private static string Digest(string Value)
		{
			byte[] digest;
			using (SHA256 hash = SHA256.Create())
				digest = hash.ComputeHash(Encoding.UTF8.GetBytes(Value ?? ""));
			StringBuilder text = new StringBuilder(64);
			for (int i = 0; i < digest.Length; i++) text.Append(digest[i].ToString("x2",
				CultureInfo.InvariantCulture));
			return text.ToString();
		}
	}
}
