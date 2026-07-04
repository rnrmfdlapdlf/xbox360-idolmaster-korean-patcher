namespace ImasKoreanPatcher
{
    internal sealed class CommunicationPerfectPatchResult
    {
        public int ManifestRows;
        public int BnaFilesSeen;
        public int BnaFilesPatched;
        public int ScbEntriesSeen;
        public int ScoreValuesPatched;
        public int ScoreValuesAlreadyPerfect;
        public int MissingBnaFiles;
        public int MissingScbEntries;
        public int ScoreMismatches;
        public int InvalidRows;
        public int Errors;
    }
}
