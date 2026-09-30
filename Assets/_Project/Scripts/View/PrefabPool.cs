using System.Collections.Generic;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>한 번에 전부 돌려받는 단순 프리팹 풀 (매번 전체를 다시 그리는 오버레이용).</summary>
    public sealed class PrefabPool<T> where T : Component
    {
        readonly T _prefab;
        readonly Transform _parent;
        readonly List<T> _items = new();
        int _used;

        public PrefabPool(T prefab, Transform parent)
        {
            _prefab = prefab;
            _parent = parent;
        }

        public IEnumerable<T> Active
        {
            get { for (int i = 0; i < _used; i++) yield return _items[i]; }
        }

        public T Take()
        {
            if (_used == _items.Count) _items.Add(Object.Instantiate(_prefab, _parent));
            var item = _items[_used++];
            item.gameObject.SetActive(true);
            return item;
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < _used; i++)
                if (_items[i] != null) _items[i].gameObject.SetActive(false);
            _used = 0;
        }
    }
}
