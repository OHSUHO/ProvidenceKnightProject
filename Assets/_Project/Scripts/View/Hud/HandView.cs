using System;
using System.Collections.Generic;
using ProvidenceKnight.Data;
using UnityEngine;

namespace ProvidenceKnight.View
{
    /// <summary>손패. container 아래에 미리 놓인 Card 인스턴스를 쓰고, 모자라면 cardPrefab 으로 채운다.</summary>
    public class HandView : MonoBehaviour
    {
        [SerializeField] RectTransform container;
        [SerializeField] CardView cardPrefab;

        public event Action<int> CardClicked;

        readonly List<CardView> _slots = new();

        void Awake()
        {
            foreach (Transform child in container)
                if (child.TryGetComponent(out CardView card)) AddSlot(card);
        }

        public void EnsureSlots(int count)
        {
            while (_slots.Count < count) AddSlot(Instantiate(cardPrefab, container));
        }

        void AddSlot(CardView card)
        {
            int index = _slots.Count;
            card.Button.onClick.AddListener(() => CardClicked?.Invoke(index));
            _slots.Add(card);
        }

        public void Refresh(IReadOnlyList<CardData> hand, int energy, int selectedIndex)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var card = i < hand.Count ? hand[i] : null;
                _slots[i].gameObject.SetActive(card != null);
                if (card != null) _slots[i].Set(card, affordable: card.cost <= energy, selected: i == selectedIndex);
            }
        }
    }
}
