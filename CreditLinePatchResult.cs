namespace ImasKoreanPatcher
{
    internal sealed class CreditLinePatchResult
    {
        public bool TargetBnaFound;
        public bool TargetScbFound;
        public bool TargetMsgFound;
        public bool Changed;
        public bool AlreadyPatched;
        public int StringsPatched;
        public int Errors;
    }
}
