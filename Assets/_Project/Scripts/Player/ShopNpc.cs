using System.Collections;
using System.Collections.Generic;
using ProvidenceKnight.Data;
using ProvidenceKnight.View;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;

namespace ProvidenceKnight.Player
{
    /// <summary>
    /// 상점 NPC. 트리거 안에서 상호작용 키(E)를 누르면 Yarn 대화가 시작되고, 대사 안의 <c>&lt;&lt;open_shop&gt;&gt;</c> 커맨드에서 상점이 열린다
    /// (상점을 닫으면 대화가 이어진다). Dialogue 를 비워 두면 대화 없이 바로 상점이 열린다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ShopNpc : MonoBehaviour
    {
        const string OpenShopCommand = "open_shop";

        [SerializeField] string shopName = "상인";
        [SerializeField] List<EquipmentData> stock = new();
        [SerializeField] Key interactKey = Key.E;
        [SerializeField] ShopScreen shop;
        [SerializeField] Vector3 promptOffset = new(0f, 1.6f, 0f);

        [Header("대화 (선택)")]
        [SerializeField] DialogueRunner dialogue;
        [Tooltip("이 NPC 가 말을 걸 때 시작하는 Yarn 노드 이름")]
        [SerializeField] string dialogueNode = "Merchant";

        // 커맨드는 러너당 한 번만 등록하고, 지금 말하고 있는 NPC 에게 전달한다.
        static readonly HashSet<DialogueRunner> Registered = new();
        static ShopNpc _talking;

        TextMeshPro _prompt;
        bool _inRange;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Registered.Clear();
            _talking = null;
        }

        void Reset() => GetComponent<Collider2D>().isTrigger = true;

        void Awake()
        {
            if (shop == null) shop = FindFirstObjectByType<ShopScreen>();
            if (dialogue == null) dialogue = FindFirstObjectByType<DialogueRunner>();

            var go = new GameObject("Prompt");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = promptOffset;
            _prompt = go.AddComponent<TextMeshPro>();
            _prompt.text = $"[{interactKey}] {shopName}";
            _prompt.fontSize = 4f;
            _prompt.alignment = TextAlignmentOptions.Center;
            _prompt.sortingOrder = 50;
            _prompt.enabled = false;
        }

        void Update()
        {
            _prompt.enabled = _inRange && !GamePause.IsPaused;
            var kb = Keyboard.current;
            if (_inRange && !GamePause.IsPaused && kb != null && kb[interactKey].wasPressedThisFrame) Interact();
        }

        /// <summary>말을 건다. 대화가 설정돼 있으면 대화, 아니면 바로 상점.</summary>
        public void Interact()
        {
            if (dialogue != null && !string.IsNullOrEmpty(dialogueNode)) Talk();
            else OpenShop();
        }

        public void OpenShop()
        {
            if (shop != null) shop.Open(stock, shopName);
        }

        void Talk()
        {
            if (dialogue.IsDialogueRunning) return;
            if (Registered.Add(dialogue))
                dialogue.AddCommandHandler(OpenShopCommand, (System.Func<IEnumerator>)(() => _talking != null ? _talking.OpenShopAndWait() : Nothing()));

            _talking = this;
            GamePause.Push(freezeTime: false);   // 글자 타이핑 연출이 시간을 쓰므로 시간은 흐르게 두고 조작만 막는다
            dialogue.onDialogueComplete.AddListener(OnTalkFinished);
            dialogue.StartDialogue(dialogueNode);
        }

        void OnTalkFinished()
        {
            dialogue.onDialogueComplete.RemoveListener(OnTalkFinished);
            if (_talking == this) _talking = null;
            GamePause.Pop(freezeTime: false);
        }

        void OnDestroy()
        {
            // 대화 도중 씬이 사라지는 경우에도 멈춤이 남지 않게
            if (_talking == this) OnTalkFinished();
        }

        IEnumerator OpenShopAndWait()
        {
            OpenShop();
            while (shop != null && shop.IsOpen) yield return null;
        }

        static IEnumerator Nothing() { yield break; }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController2D>() != null) _inRange = true;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController2D>() != null) _inRange = false;
        }
    }
}
