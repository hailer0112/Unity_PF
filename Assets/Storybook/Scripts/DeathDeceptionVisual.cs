using UnityEngine;
using UnityEngine.Rendering;

namespace Storybook
{
    [DefaultExecutionOrder(-20)]
    internal sealed class DeathDeceptionVisual : MonoBehaviour
    {
        const float WaveDuration = .30f;
        static readonly Color Glow = new(1f, .24f, .43f);
        static readonly Vector3[] CrackPoints =
        {
            new(-.12f, .20f), new(.01f, .08f), new(-.055f, .01f),
            new(.06f, -.045f), new(.015f, -.13f), new(.105f, -.23f)
        };

        PlayerHealth health;
        SpriteRenderer body, heart;
        LineRenderer crack, wave;
        Camera view;
        Material shardMaterial;
        Color originalTint;
        float startedAt, duration, height, headHeight;
        bool tintApplied, shattered;

        internal static DeathDeceptionVisual Show(Transform caster, SpriteRenderer body, Camera view,
            PlayerHealth health, DeceiversHeartRelic relic)
        {
            if (body == null || relic.Icon == null) return null;

            var effect = new GameObject("Death Deception").AddComponent<DeathDeceptionVisual>();
            effect.transform.SetParent(caster, false);
            effect.health = health;
            effect.body = body;
            effect.originalTint = body.color;
            effect.view = view;
            effect.startedAt = Time.time;
            effect.duration = relic.VisualDuration;
            effect.height = relic.VisualHeight;
            effect.headHeight = relic.HeadHeight;
            effect.shardMaterial = relic.ShardMaterial;
            effect.heart = new GameObject("Deceiver's heart").AddComponent<SpriteRenderer>();
            effect.heart.transform.SetParent(effect.transform, false);
            effect.heart.sprite = relic.Icon;

            effect.crack = effect.MakeLine("Heart fracture");
            effect.crack.positionCount = CrackPoints.Length;
            effect.crack.enabled = false;
            effect.wave = effect.MakeLine("Recovery wave");
            effect.wave.transform.localPosition = Vector3.up * .038f;
            effect.wave.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            effect.wave.positionCount = 32;
            effect.wave.loop = true;

            if (relic.RecoverySparks != null)
            {
                var burst = Instantiate(relic.RecoverySparks, caster.position + Vector3.up * .32f,
                    Quaternion.identity, effect.transform);
                var main = burst.main;
                main.startColor = new Color(1f, .50f, .57f, .8f);
                burst.Play();
            }

            effect.LateUpdate();
            return effect;
        }

        LineRenderer MakeLine(string name)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = heart.sharedMaterial;
            line.useWorldSpace = false;
            line.alignment = LineAlignment.TransformZ;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        void LateUpdate()
        {
            if (body == null || health == null || health.IsDead)
            {
                Remove();
                return;
            }

            float age = Time.time - startedAt;
            if (health.HasDeathDeceptionProtection)
            {
                float pulse = Mathf.Pow(.5f + .5f * Mathf.Cos(age * 11f * Mathf.PI), 2f);
                var tint = Color.Lerp(originalTint, new Color(1f, .54f, .62f, originalTint.a * .72f), pulse);
                body.color = tint;
                tintApplied = true;
            }
            else RestoreTint();

            if (age >= duration && !health.HasDeathDeceptionProtection)
            {
                Remove();
                return;
            }

            DrawRecoveryWave(age);
            DrawHeart(Mathf.Clamp01(age / duration));
        }

        void DrawRecoveryWave(float age)
        {
            wave.enabled = age < WaveDuration;
            if (!wave.enabled) return;
            float progress = age / WaveDuration;
            float radius = Mathf.Lerp(.12f, .68f, Mathf.Sqrt(progress));
            wave.widthMultiplier = Mathf.Lerp(.032f, .010f, progress);
            wave.startColor = wave.endColor = new Color(Glow.r, Glow.g, Glow.b, .66f * (1f - progress));
            for (int i = 0; i < wave.positionCount; i++)
            {
                float angle = 2f * Mathf.PI * i / wave.positionCount;
                wave.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
            }
        }

        void DrawHeart(float progress)
        {
            var rotation = view != null ? view.transform.rotation : body.transform.rotation;
            Vector3 anchor = transform.position + Vector3.up * headHeight +
                rotation * Vector3.up * (height * .5f + .08f + progress * .07f);
            float pulse = progress < .56f ? Mathf.Abs(Mathf.Sin(progress / .56f * 2f * Mathf.PI)) : 0f;
            float scale = 1f + pulse * .24f;
            float opacity = Mathf.Clamp01(progress / .06f) * (1f - Mathf.InverseLerp(.73f, .93f, progress));
            heart.transform.SetPositionAndRotation(anchor, rotation);
            heart.transform.localScale = Vector3.one * (height * scale / heart.sprite.bounds.size.y);
            heart.sortingLayerID = crack.sortingLayerID = wave.sortingLayerID = body.sortingLayerID;
            wave.sortingOrder = body.sortingOrder - 1;
            heart.sortingOrder = body.sortingOrder + 2;
            crack.sortingOrder = heart.sortingOrder + 1;
            heart.enabled = opacity > 0f;
            heart.color = new Color(1f, 1f, 1f, opacity);
            crack.enabled = progress >= .55f && opacity > 0f;
            if (crack.enabled)
            {
                crack.transform.SetPositionAndRotation(anchor, rotation);
                float trace = Mathf.InverseLerp(.55f, .72f, progress) * (CrackPoints.Length - 1);
                for (int i = 0; i < CrackPoints.Length; i++)
                {
                    float point = Mathf.Min(i, trace);
                    int from = Mathf.Min((int)point, CrackPoints.Length - 2);
                    crack.SetPosition(i, Vector3.Lerp(CrackPoints[from], CrackPoints[from + 1], point - from) * (height * scale));
                }
                crack.widthMultiplier = .016f;
                crack.startColor = crack.endColor = new Color(1f, .65f, .74f, opacity);
            }

            if (!shattered && progress >= .73f)
            {
                shattered = true;
                EmitShards(anchor, rotation);
            }
        }

        void EmitShards(Vector3 position, Quaternion rotation)
        {
            if (shardMaterial == null) return;
            var shards = new GameObject("Heart shards").AddComponent<ParticleSystem>();
            shards.transform.SetParent(transform, false);
            shards.transform.SetPositionAndRotation(position, rotation);
            shards.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = shards.main;
            main.duration = .02f;
            main.loop = false;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.startLifetime = duration * .24f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(.6f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(.025f, .060f);
            // The imported spark material boosts brightness, so use deep red particle colors.
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(.60f, .025f, .055f), new Color(.85f, .065f, .10f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = shards.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = height * .14f;
            shape.sphericalDirectionAmount = 1f;
            var emission = shards.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)18) });
            var renderer = shards.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = shardMaterial;
            renderer.sortingLayerID = heart.sortingLayerID;
            renderer.sortingOrder = heart.sortingOrder + 1;
            shards.Play();
        }

        void RestoreTint()
        {
            if (!tintApplied) return;
            if (body != null) body.color = originalTint;
            tintApplied = false;
        }

        void OnDisable() => RestoreTint();

        internal void Remove()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
