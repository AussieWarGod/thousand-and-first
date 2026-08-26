using System;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomRealmCallbackReceipt
	{
		public const int MaxEffectChars = 65536;
		public KingdomRealmCallbackPhase Phase;
		public KingdomRealmCallbackDisposition Disposition;
		public KingdomRealmCallbackScope Scope;
		public string BeforeGraph;
		public string AfterGraph;
		public string BeforeArchiveGraph;
		public string AfterArchiveGraph;
		public string BeforeEffect;
		public string AfterEffect;
		public string ObservedEffect;
		public int BeforeStamp = int.MinValue;
		public int AfterStamp = int.MinValue;

		public bool Validate()
		{
			if (!Enum.IsDefined(typeof(KingdomRealmCallbackPhase), Phase) ||
				!Enum.IsDefined(typeof(KingdomRealmCallbackDisposition), Disposition) ||
				!Enum.IsDefined(typeof(KingdomRealmCallbackScope), Scope)) return false;
			if (Phase == KingdomRealmCallbackPhase.None)
				return Disposition == KingdomRealmCallbackDisposition.None && Scope ==
					KingdomRealmCallbackScope.None &&
					BeforeGraph == null && AfterGraph == null &&
					BeforeArchiveGraph == null && AfterArchiveGraph == null &&
					BeforeEffect == null && AfterEffect == null && ObservedEffect == null &&
					BeforeStamp == int.MinValue && AfterStamp == int.MinValue;
			if (string.IsNullOrEmpty(BeforeGraph) || BeforeGraph.Length != 64 ||
				string.IsNullOrEmpty(BeforeArchiveGraph) || BeforeArchiveGraph.Length != 64 ||
				Scope == KingdomRealmCallbackScope.None || BeforeEffect == null ||
				AfterEffect == null) return false;
			if (Scope == KingdomRealmCallbackScope.Feelings)
			{
				if (!Enum.IsDefined(typeof(RealmRegard), BeforeStamp) ||
					!Enum.IsDefined(typeof(RealmRegard), AfterStamp)) return false;
			}
			else if (BeforeStamp != int.MinValue || AfterStamp != int.MinValue) return false;
			if (Phase != KingdomRealmCallbackPhase.Settled)
				return Disposition == KingdomRealmCallbackDisposition.None &&
					AfterGraph == null && AfterArchiveGraph == null && ObservedEffect == null;
			return Disposition != KingdomRealmCallbackDisposition.None &&
				!string.IsNullOrEmpty(AfterGraph) && AfterGraph.Length == 64 &&
				!string.IsNullOrEmpty(AfterArchiveGraph) && AfterArchiveGraph.Length == 64 &&
				ObservedEffect != null;
		}
	}
}
