namespace ImasKoreanPatcher
{
    internal sealed class LessonGainPatchResult
    {
        public bool TargetBnaFound;
        public int TargetBxrEntriesSeen;
        public int BnaEntriesVerified;
        public int BnaFilesPatched;
        public int RowsVerified;
        public int FieldsVerified;
        public int NonzeroValuesPatched;
        public int NonzeroValuesAlreadyPatched;
        public int ZeroValuesUnchanged;
        public int UpdatedReferenceFields;
        public int OtherBnaEntriesPreserved;
    }
}
