using System.IO;
using UnityEngine;

namespace Meta
{
    /// <summary>
    /// persistentDataPath/meta_save.json 에 메타 진행을 저장하는 ISaveStore 구현.
    /// </summary>
    public class JsonFileStore : ISaveStore
    {
        readonly string _path;

        public JsonFileStore(string fileName = "meta_save.json")
        {
            _path = Path.Combine(Application.persistentDataPath, fileName);
        }

        public string Load()
        {
            try
            {
                return File.Exists(_path) ? File.ReadAllText(_path) : null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[JsonFileStore] Load 실패: {e.Message}");
                return null;
            }
        }

        public void Save(string json)
        {
            try
            {
                // 원자적 쓰기: 임시 파일에 쓴 뒤 교체 — 쓰기 도중 크래시해도 기존 세이브 보존.
                string tmp = _path + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(_path)) File.Replace(tmp, _path, null);
                else File.Move(tmp, _path);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[JsonFileStore] Save 실패: {e.Message}");
            }
        }
    }
}
