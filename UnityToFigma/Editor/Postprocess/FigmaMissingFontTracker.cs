using System.Collections.Generic;
using UnityEditor;

namespace UnityToFigma.Editor.Postprocess
{
    // FontManager 가 Figma 문서의 폰트 매핑 시 다운로드 실패/미보유로 GetClosestFont 폴백을 사용한 (family, weight) 를 누적한다.
    // Sync 종료 후 FigmaMissingFontWindow 가 이 목록을 읽어 사용자에게 직접 선택받는다.
    // SessionState 기반이라 도메인 리로드는 유지되지만 Unity 재시작 시 리셋된다.
    internal static class FigmaMissingFontTracker
    {
        private const string Key = "UnityToFigma.Postprocess.MissingFonts";
        private const string Separator = "|";
        private const string FieldSeparator = "::";

        public readonly struct Entry
        {
            public readonly string Family;
            public readonly int Weight;
            public Entry(string family, int weight) { Family = family; Weight = weight; }
            public override string ToString() => Weight > 0 ? $"{Family} ({Weight})" : Family;
        }

        public static void Record(string family, int weight)
        {
            if (string.IsNullOrEmpty(family)) return;
            var existing = LoadRaw();
            var key = family + FieldSeparator + weight;
            if (existing.Contains(key)) return;
            existing.Add(key);
            SessionState.SetString(Key, string.Join(Separator, existing));
        }

        public static List<Entry> GetAll()
        {
            var list = new List<Entry>();
            foreach (var raw in LoadRaw())
            {
                var idx = raw.IndexOf(FieldSeparator, System.StringComparison.Ordinal);
                if (idx < 0)
                {
                    list.Add(new Entry(raw, 0));
                    continue;
                }
                var family = raw.Substring(0, idx);
                int.TryParse(raw.Substring(idx + FieldSeparator.Length), out var weight);
                list.Add(new Entry(family, weight));
            }
            return list;
        }

        public static int Count => LoadRaw().Count;

        public static void Clear() => SessionState.EraseString(Key);

        private static List<string> LoadRaw()
        {
            var s = SessionState.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(s)) return new List<string>();
            return new List<string>(s.Split(new[] { Separator }, System.StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
