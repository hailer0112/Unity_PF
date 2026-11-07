using System.Collections.Generic;
using UnityEngine;

namespace Storybook
{
    public sealed class CombatRoom : MonoBehaviour
    {
        public enum RoomState { NotStarted, InCombat, Cleared }

        [SerializeField] HongGildong player;
        [SerializeField] MonsterHealth[] enemies;
        [SerializeField] GameObject entranceGate;
        [SerializeField] CombatRoomExit exit;
        [SerializeField] bool reopenEntranceOnClear;
        [SerializeField] CombatRoom nextRoom;
        [SerializeField] string clearMessage = "방 클리어";

        readonly HashSet<MonsterHealth> remaining = new HashSet<MonsterHealth>();

        GUIStyle clearStyle;
        Font clearFont;

        public int RemainingEnemies => remaining.Count;
        public RoomState State { get; private set; }
        public bool HasStarted => State != RoomState.NotStarted;
        public bool IsCleared => State == RoomState.Cleared;

        void OnEnable()
        {
            foreach (MonsterHealth enemy in enemies)
                if (enemy != null) enemy.Died += CheckClear;
        }

        void OnDisable()
        {
            foreach (MonsterHealth enemy in enemies)
                if (enemy != null) enemy.Died -= CheckClear;
        }

        void OnTriggerEnter(Collider other)
        {
            if (HasStarted || player == null || other.GetComponentInParent<HongGildong>() != player)
                return;

            State = RoomState.InCombat;
            entranceGate.SetActive(true);

            foreach (MonsterHealth enemy in enemies)
            {
                if (enemy == null || enemy.IsDead) continue;

                remaining.Add(enemy);
                if (enemy.TryGetComponent(out CrabSoldierAI ai))
                    ai.enabled = true;
            }

            CheckClear();
        }

        void Update()
        {
            // A destroyed room enemy no longer participates. Deactivating a living
            // enemy alone does not count as a kill.
            if (State == RoomState.InCombat) CheckClear();
        }

        void CheckClear()
        {
            if (State != RoomState.InCombat) return;

            remaining.RemoveWhere(enemy => enemy == null || enemy.IsDead);
            if (remaining.Count != 0) return;

            State = RoomState.Cleared;
            if (reopenEntranceOnClear) entranceGate.SetActive(false);
            exit.Open();
        }

        void OnGUI()
        {
            if (!IsCleared || (nextRoom != null && nextRoom.HasStarted)) return;

            if (clearStyle == null)
            {
                clearFont = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 26);
                clearStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    font = clearFont,
                    fontSize = 26
                };
                clearStyle.normal.textColor = Color.white;
            }

            float width = Mathf.Max(230f, clearStyle.CalcSize(new GUIContent(clearMessage)).x + 40f);
            GUI.Box(new Rect((Screen.width - width) * .5f, 28f, width, 54f), clearMessage, clearStyle);
        }

        void OnDestroy()
        {
            if (clearFont != null) Destroy(clearFont);
        }
    }
}
