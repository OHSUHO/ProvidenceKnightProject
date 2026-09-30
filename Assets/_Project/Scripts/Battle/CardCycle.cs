using System;
using System.Collections.Generic;
using ProvidenceKnight.Data;

namespace ProvidenceKnight.Battle
{
    /// <summary>
    /// 손패 + 순서 고정 덱.
    /// 카드를 쓰면 그 카드는 덱 맨 아래로 가고, 덱 맨 위 카드가 손패 맨 뒤로 들어온다.
    /// </summary>
    public class CardCycle
    {
        public IReadOnlyList<CardData> Hand => _hand;
        public int DeckCount => _deck.Count;
        public int HandSize { get; }

        readonly List<CardData> _hand = new();
        readonly LinkedList<CardData> _deck = new();

        public CardCycle(IEnumerable<CardData> orderedCards, int handSize)
        {
            if (handSize <= 0) throw new ArgumentException("handSize must be positive");
            HandSize = handSize;
            foreach (var c in orderedCards)
            {
                if (c == null) continue;
                if (_hand.Count < handSize) _hand.Add(c);
                else _deck.AddLast(c);
            }
        }

        CardCycle(int handSize) => HandSize = handSize;

        /// <summary>시뮬레이션용 복사본 (카드 에셋 참조는 공유, 순서만 복사).</summary>
        public CardCycle Clone()
        {
            var copy = new CardCycle(HandSize);
            copy._hand.AddRange(_hand);
            foreach (var c in _deck) copy._deck.AddLast(c);
            return copy;
        }

        /// <summary>덱 위에서부터 count 장 (다음에 손으로 들어올 순서).</summary>
        public IEnumerable<CardData> PeekDeck(int count)
        {
            var node = _deck.First;
            for (int i = 0; i < count && node != null; i++, node = node.Next)
                yield return node.Value;
        }

        /// <summary>
        /// 손패에서 카드를 빼고, 덱 맨 위 카드를 손패로 가져온다.
        /// exhaust 가 아니면 뺀 카드를 덱 맨 아래로 되돌린다. exhaust 면 이번 사이클(스테이지)에서 완전히 사라진다.
        /// </summary>
        public CardData Cycle(int handIndex, bool exhaust = false)
        {
            if (handIndex < 0 || handIndex >= _hand.Count)
                throw new ArgumentOutOfRangeException(nameof(handIndex));

            var played = _hand[handIndex];
            _hand.RemoveAt(handIndex);
            if (!exhaust) _deck.AddLast(played);

            if (_deck.Count > 0)
            {
                var next = _deck.First.Value;
                _deck.RemoveFirst();
                _hand.Add(next);
            }
            return played;
        }

        /// <summary>손패를 전부 덱 맨 아래로 버리고, 덱 맨 위에서 최대 HandSize 장을 새로 채운다 (턴 종료 시 호출).</summary>
        public void DiscardHandAndRefill()
        {
            foreach (var c in _hand)
                _deck.AddLast(c);
            _hand.Clear();

            while (_hand.Count < HandSize && _deck.Count > 0)
            {
                _hand.Add(_deck.First.Value);
                _deck.RemoveFirst();
            }
        }
    }
}
