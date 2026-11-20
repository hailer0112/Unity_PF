using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Storybook
{
    public sealed partial class HongGildong
    {
        [Header("W - Clone Slash")]
        [SerializeField, Min(.01f)] float cloneSlashRange = 3f;
        [SerializeField, Range(1f, 180f)] float cloneSlashHalfArc = 68f;
        [SerializeField, Min(0f)] float cloneSlashDamageMultiplier = 1f;
        [SerializeField, Min(.01f)] float cloneSlashCastTime = .8f;
        [SerializeField, Min(0f)] float cloneSlashCooldown = 8f;
        [SerializeField] float[] cloneSlashHitTimes = { .15f, .4f, .65f };
        [SerializeField, Min(0f)] float cloneSlashSideOffset = 1f;
        [SerializeField, Range(0f, 1f)] float cloneSlashOpacity = .8f;
        [SerializeField, Min(.01f)] float cloneSlashFadeTime = .15f;
        [SerializeField] CloneSlashVisual cloneSlashPrefab;
        [SerializeField] Sprite[] cloneSlashChargeSprites = new Sprite[4];

        SpriteRenderer cloneSlashCharge;

        readonly List<CloneSlashVisual> cloneSlashVisuals = new List<CloneSlashVisual>();
        readonly HashSet<AttackTarget> cloneSlashHitTargets = new HashSet<AttackTarget>();
        InputAction cloneSlashInput;
        float[] activeCloneSlashTimes;
        double cloneSlashStartedAt, cloneSlashReadyAt;
        int cloneSlashHitIndex;
        Vector3 cloneSlashDirection, cloneSlashOrigin;
        CloneSlashVisual nextClone;
        bool cloneCastDrawsWeapon;

        public bool IsCastingCloneSlash { get; private set; }

        void InitializeCloneSlashInput() =>
            cloneSlashInput = new InputAction("Clone Slash", InputActionType.Button, "<Keyboard>/w");

        bool BeginCloneSlash(Vector2 cursor)
        {
            if (IsCastingCloneSlash || IsCastingAzureSlash || IsDashing || Attacking ||
                Time.timeAsDouble < cloneSlashReadyAt || cloneSlashHitTimes == null ||
                cloneSlashHitTimes.Length == 0 || !TryGroundPoint(cursor, out Vector3 ground))
                return false;

            if (cloneSlashPrefab == null) cloneSlashPrefab = Resources.Load<CloneSlashVisual>("CloneSlashVisual");
            if (cloneSlashPrefab == null)
            {
                Debug.LogError("Clone Slash requires its visual prefab.", this);
                return false;
            }

            if (!TrySpendSkillMana(HudSkill.CloneSlash, cloneSlashManaCost)) return false;
            cloneCastDrawsWeapon = EnterCombatStance();

            Vector3 direction = Vector3.ProjectOnPlane(ground - transform.position, Vector3.up);
            cloneSlashDirection = direction.sqrMagnitude > .0025f ? direction.normalized : facing.normalized;
            cloneSlashOrigin = transform.position;
            PrepareForAction();
            Face(cloneSlashDirection);
            bladeTrail.enabled = false;
            activeCloneSlashTimes = (float[])cloneSlashHitTimes.Clone();
            for (int i = 0; i < activeCloneSlashTimes.Length; i++)
                activeCloneSlashTimes[i] = Mathf.Clamp(activeCloneSlashTimes[i], 0f, cloneSlashCastTime);
            Array.Sort(activeCloneSlashTimes);
            cloneSlashHitIndex = 0;
            cloneSlashStartedAt = Time.timeAsDouble;
            cloneSlashReadyAt = cloneSlashStartedAt + cloneSlashCooldown;
            IsCastingCloneSlash = true;
            return true;
        }

        void UpdateCloneSlash()
        {
            MoveOnGround(Vector3.zero);
            float elapsed = (float)(Time.timeAsDouble - cloneSlashStartedAt);
            int direction = Mathf.Abs(cloneSlashDirection.x) > Mathf.Abs(cloneSlashDirection.z)
                ? (cloneSlashDirection.x > 0f ? 0 : 1) : (cloneSlashDirection.z > 0f ? 3 : 2);
            Sprite[] frames = direction switch
            {
                0 => swingRight,
                1 => swingLeft,
                2 => swingFrontBasic,
                _ => swingBackBasic
            };
            bool renewed = frames != null && frames.Length == 4 && Array.TrueForAll(frames, frame => frame != null);
            if (!renewed) frames = back ? swingBack : swingFront;
            float firstHit = activeCloneSlashTimes[0];
            int pose = renewed
                ? (elapsed < firstHit ? (elapsed < firstHit * .5f ? 0 : 1) : (elapsed < firstHit + .10f ? 2 : 3))
                : CloneSlashVisual.FrameAt(elapsed, firstHit, frames.Length);
            body.flipX = !renewed && left;
            body.transform.localPosition = Vector3.up * artworkGroundOffset;
            body.sprite = frames[renewed && !cloneCastDrawsWeapon && pose < 2 ? 3 : pose];
            UpdateCloneSlashCharge(direction, elapsed, firstHit);

            while (IsCastingCloneSlash && cloneSlashHitIndex < activeCloneSlashTimes.Length &&
                   elapsed >= activeCloneSlashTimes[cloneSlashHitIndex])
            {
                Vector3 origin = nextClone != null ? nextClone.AttackOrigin : cloneSlashOrigin;
                if (nextClone != null) nextClone.Impact();
                StrikeCloneSlash(origin);
                if (!IsCastingCloneSlash) return;

                float previous = activeCloneSlashTimes[cloneSlashHitIndex++];
                nextClone = cloneSlashHitIndex < activeCloneSlashTimes.Length
                    ? SpawnCloneSlash(frames, previous, elapsed, renewed)
                    : null;
            }

            if (elapsed >= cloneSlashCastTime) CancelCloneSlash();
        }

        CloneSlashVisual SpawnCloneSlash(Sprite[] frames, float previousHit, float elapsed, bool renewed)
        {
            Vector3 right = Vector3.Cross(Vector3.up, cloneSlashDirection);
            Vector3 side = cloneSlashHitIndex % 2 == 1 ? -right : right;
            Vector3 origin = cloneSlashOrigin + side * SafeCloneOffset(side);
            float hitTime = activeCloneSlashTimes[cloneSlashHitIndex];
            float lead = Mathf.Max(0f, hitTime - previousHit);
            float fade = Mathf.Min(cloneSlashFadeTime, Mathf.Max(0f, cloneSlashCastTime - hitTime));
            var clone = Instantiate(cloneSlashPrefab, origin + Vector3.up * artworkGroundOffset, body.transform.rotation);
            clone.transform.localScale = body.transform.lossyScale;
            clone.Initialize(origin, frames, !renewed && left, cloneSlashOpacity, lead, Mathf.Max(0f, elapsed - previousHit),
                fade, cloneSlashDirection, cloneSlashRange, cloneSlashHalfArc, renewed);
            cloneSlashVisuals.Add(clone);
            return clone;
        }

        void UpdateCloneSlashCharge(int direction, float elapsed, float firstHit)
        {
            Sprite sprite = cloneSlashChargeSprites.Length == 4 ? cloneSlashChargeSprites[direction] : null;
            if (sprite == null || elapsed >= firstHit)
            {
                if (cloneSlashCharge != null) cloneSlashCharge.enabled = false;
                return;
            }

            if (cloneSlashCharge == null)
            {
                cloneSlashCharge = new GameObject("Clone Slash Charge").AddComponent<SpriteRenderer>();
                cloneSlashCharge.transform.SetParent(body.transform, false);
            }
            cloneSlashCharge.sprite = sprite;
            cloneSlashCharge.sortingLayerID = body.sortingLayerID;
            cloneSlashCharge.sortingOrder = body.sortingOrder - 1;
            float strength = Mathf.SmoothStep(0f, 1f, elapsed / firstHit);
            cloneSlashCharge.color = new Color(1f, 1f, 1f, strength);
            cloneSlashCharge.enabled = true;
        }

        float SafeCloneOffset(Vector3 side)
        {
            if (cloneSlashSideOffset <= 0f) return 0f;

            float distance = cloneSlashSideOffset;
            foreach (RaycastHit hit in Physics.BoxCastAll(cloneSlashOrigin + Vector3.up * .35f,
                         new Vector3(.15f, .25f, .15f), side, Quaternion.identity, distance,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (BlocksCloneSlash(hit.collider))
                    distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - .03f));
            }

            // Keep the visual in the caster's room (or corridor) even at open doors.
            Vector3 start = cloneSlashOrigin + Vector3.up * .35f;
            foreach (var room in FindObjectsByType<CombatRoom>())
            {
                if (!room.TryGetComponent(out Collider trigger) || !trigger.enabled) continue;

                Bounds bounds = trigger.bounds;
                if (bounds.Contains(start))
                {
                    Vector3 end = start + side * distance;
                    if (!bounds.Contains(end) && bounds.IntersectRay(new Ray(end, -side), out float returnDistance))
                        distance = Mathf.Max(0f, distance - returnDistance - .03f);
                }
                else if (bounds.IntersectRay(new Ray(start, side), out float entryDistance))
                {
                    distance = Mathf.Min(distance, Mathf.Max(0f, entryDistance - .03f));
                }
            }

            return distance;
        }

        bool BlocksCloneSlash(Collider collider) =>
            !collider.transform.IsChildOf(transform) && collider.GetComponentInParent<AttackTarget>() == null;

        void StrikeCloneSlash(Vector3 origin)
        {
            cloneSlashHitTargets.Clear();
            Vector3 center = origin + Vector3.up * .4f;
            foreach (Collider collider in Physics.OverlapSphere(center, cloneSlashRange + .4f,
                         targetMask, QueryTriggerInteraction.Collide))
            {
                AttackTarget target = AttackTarget.FromDamageCollider(collider, out bool useSurface);
                if (target == null || !target.CanTakeDamage || cloneSlashHitTargets.Contains(target)) continue;

                Vector3 contact = useSurface ? collider.ClosestPoint(center) : target.transform.position;
                Vector3 offset = Vector3.ProjectOnPlane(contact - origin, Vector3.up);
                if (offset.sqrMagnitude > cloneSlashRange * cloneSlashRange ||
                    Vector3.Angle(cloneSlashDirection, offset) > cloneSlashHalfArc)
                    continue;

                bool blocked = false;
                if (offset.sqrMagnitude > .0001f)
                {
                    foreach (RaycastHit hit in Physics.RaycastAll(origin + Vector3.up * .35f, offset.normalized,
                                 offset.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        if (!BlocksCloneSlash(hit.collider)) continue;
                        blocked = true;
                        break;
                    }
                }

                if (!blocked && cloneSlashHitTargets.Add(target))
                {
                    float damage = attackDamage * cloneSlashDamageMultiplier;
                    if (target is MonsterHealth monster) monster.ReceiveSkillDamage(damage, origin, SkillHit.CloneSlash, camera: CombatCamera);
                    else target.ReceiveDamage(damage, origin);
                }
                if (!IsCastingCloneSlash) return;
            }
        }

        void CancelCloneSlash()
        {
            IsCastingCloneSlash = false;
            if (cloneSlashCharge != null) cloneSlashCharge.enabled = false;
            nextClone = null;
            foreach (var clone in cloneSlashVisuals)
                if (clone != null) Destroy(clone.gameObject);
            cloneSlashVisuals.Clear();
        }
    }
}
