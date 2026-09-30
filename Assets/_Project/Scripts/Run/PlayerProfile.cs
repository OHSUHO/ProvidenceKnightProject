using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProvidenceKnight.Data;
using UnityEngine;

namespace ProvidenceKnight.Run
{
    public enum EquipCheck
    {
        Ok,
        NotOwned,
        LevelTooLow,
    }

    /// <summary>
    /// 런이 바뀌어도 이어지는 플레이어 성장: 레벨·경험치·골드·보유/착용 장비.
    /// 스탯은 레벨이 아니라 착용한 장비의 합으로만 오른다. 레벨은 장비 착용(과 나중에 스킬 습득)의 조건이다.
    /// </summary>
    public class PlayerProfile
    {
        readonly ProgressionData _progression;
        readonly List<EquipmentData> _owned = new();
        readonly Dictionary<EquipSlot, EquipmentData> _equipped = new();
        readonly HashSet<string> _collected = new();

        public int Level { get; private set; } = 1;
        public int Exp { get; private set; }
        public int Gold { get; private set; }

        public IReadOnlyList<EquipmentData> Owned => _owned;
        public bool IsMaxLevel => Level >= _progression.maxLevel;
        public int ExpToNext => _progression.ExpToNext(Level);

        /// <summary>값이 바뀔 때마다 (HUD 갱신용).</summary>
        public event Action Changed;
        /// <summary>레벨이 올랐을 때 새 레벨.</summary>
        public event Action<int> LeveledUp;

        public PlayerProfile(ProgressionData progression)
        {
            _progression = progression != null ? progression : throw new ArgumentNullException(nameof(progression));
        }

        // ---------------- 경험치 · 골드 ----------------

        /// <summary>경험치를 더하고 오른 레벨 수를 돌려준다. 최대 레벨이면 경험치는 쌓이지 않는다.</summary>
        public int AddExp(int amount)
        {
            if (amount <= 0 || IsMaxLevel) return 0;
            Exp += amount;
            int gained = 0;
            while (!IsMaxLevel && Exp >= ExpToNext)
            {
                Exp -= ExpToNext;
                Level++;
                gained++;
                LeveledUp?.Invoke(Level);
            }
            if (IsMaxLevel) Exp = 0;
            Changed?.Invoke();
            return gained;
        }

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            Changed?.Invoke();
        }

        public bool TrySpendGold(int amount)
        {
            if (amount < 0 || Gold < amount) return false;
            Gold -= amount;
            if (amount > 0) Changed?.Invoke();
            return true;
        }

        // ---------------- 장비 ----------------

        public bool Owns(EquipmentData item) => _owned.Contains(item);

        public void AddOwned(EquipmentData item)
        {
            if (item == null || _owned.Contains(item)) return;
            _owned.Add(item);
            Changed?.Invoke();
        }

        public EquipmentData GetEquipped(EquipSlot slot) => _equipped.TryGetValue(slot, out var e) ? e : null;

        public EquipCheck CanEquip(EquipmentData item)
        {
            if (item == null || !Owns(item)) return EquipCheck.NotOwned;
            return Level >= item.requiredLevel ? EquipCheck.Ok : EquipCheck.LevelTooLow;
        }

        /// <summary>같은 슬롯에 끼고 있던 장비는 벗겨진다 (보유 목록에는 그대로).</summary>
        public EquipCheck TryEquip(EquipmentData item)
        {
            var check = CanEquip(item);
            if (check != EquipCheck.Ok) return check;
            _equipped[item.slot] = item;
            Changed?.Invoke();
            return check;
        }

        public void Unequip(EquipSlot slot)
        {
            if (_equipped.Remove(slot)) Changed?.Invoke();
        }

        /// <summary>착용 중인 장비의 스탯 합. 착용 레벨이 부족해진 장비(레벨 하락 등)도 그대로 더한다 — 레벨은 착용 시점에만 검사.</summary>
        public StatBlock Stats
        {
            get
            {
                var sum = new StatBlock();
                foreach (var item in _equipped.Values) sum += item.stats;
                return sum;
            }
        }

        // ---------------- 필드 획득물 ----------------

        public bool IsCollected(string key) => _collected.Contains(key);
        public void MarkCollected(string key) => _collected.Add(key);

        // ---------------- 저장 ----------------

        public ProfileSaveData ToSaveData() => new()
        {
            level = Level,
            exp = Exp,
            gold = Gold,
            ownedIds = _owned.Select(e => e.Id).ToArray(),
            equippedIds = _equipped.Values.Select(e => e.Id).ToArray(),
            collected = _collected.ToArray(),
        };

        /// <summary>없는 장비 id 는 건너뛰고 경고만 남긴다 (데이터가 지워졌을 수 있음).</summary>
        public static PlayerProfile FromSaveData(ProfileSaveData data, ProgressionData progression, GameDatabase db)
        {
            var p = new PlayerProfile(progression)
            {
                Level = Mathf.Clamp(data.level, 1, progression.maxLevel),
                Exp = Mathf.Max(0, data.exp),
                Gold = Mathf.Max(0, data.gold),
            };
            foreach (var id in data.ownedIds ?? Array.Empty<string>())
            {
                var item = db != null ? db.Get<EquipmentData>(id) : null;
                if (item == null) { Debug.LogWarning($"[PlayerProfile] 장비 id '{id}' 를 찾을 수 없어 건너뜁니다"); continue; }
                if (!p._owned.Contains(item)) p._owned.Add(item);
            }
            foreach (var id in data.equippedIds ?? Array.Empty<string>())
            {
                var item = db != null ? db.Get<EquipmentData>(id) : null;
                if (item != null && p._owned.Contains(item)) p._equipped[item.slot] = item;
            }
            foreach (var key in data.collected ?? Array.Empty<string>()) p._collected.Add(key);
            return p;
        }
    }

    [Serializable]
    public class ProfileSaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int level = 1;
        public int exp;
        public int gold;
        public string[] ownedIds = Array.Empty<string>();
        public string[] equippedIds = Array.Empty<string>();
        public string[] collected = Array.Empty<string>();
    }

    /// <summary>persistentDataPath/profile_save.json. 런 세이브(run_save.json)와는 별개 — 런이 끝나도 남는다.</summary>
    public static class ProfileSaveStore
    {
        public static string FilePath => Path.Combine(Application.persistentDataPath, "profile_save.json");
        public static bool HasSave => File.Exists(FilePath);

        public static void Save(ProfileSaveData data) => File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));

        public static bool TryLoad(out ProfileSaveData data)
        {
            data = null;
            if (!HasSave) return false;
            try
            {
                data = JsonUtility.FromJson<ProfileSaveData>(File.ReadAllText(FilePath));
                return data != null && data.version == ProfileSaveData.CurrentVersion;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ProfileSaveStore] 읽기 실패: {e.Message}");
                return false;
            }
        }

        public static void Delete()
        {
            if (HasSave) File.Delete(FilePath);
        }
    }

    /// <summary>씬을 넘나드는 현재 프로필. 처음 접근할 때 세이브 파일에서 불러오고, 없으면 새로 만든다.</summary>
    public static class ProfileSession
    {
        static PlayerProfile _current;

        public static PlayerProfile Current => _current ??= Load();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => _current = null;

        /// <summary>테스트나 새 게임에서 프로필을 직접 지정한다.</summary>
        public static void Set(PlayerProfile profile) => _current = profile;

        public static void Save()
        {
            if (_current != null) ProfileSaveStore.Save(_current.ToSaveData());
        }

        static PlayerProfile Load()
        {
            var settings = GameSettings.Instance;
            if (settings == null || settings.progression == null)
                throw new InvalidOperationException("Resources/GameSettings 에 Progression 이 지정되지 않음");
            return ProfileSaveStore.TryLoad(out var data)
                ? PlayerProfile.FromSaveData(data, settings.progression, settings.database)
                : new PlayerProfile(settings.progression);
        }
    }
}
