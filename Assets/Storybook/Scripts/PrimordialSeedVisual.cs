using UnityEngine;
using UnityEngine.Rendering;

namespace Storybook
{
    internal sealed class PrimordialSeedVisual : MonoBehaviour
    {
        static readonly Color Gold = new(1f, .77f, .28f);
        const float BloomTime = .42f;
        SpriteRenderer first, second;
        Sprite evolvedIcon;
        Transform body;
        Quaternion viewRotation;
        float started, duration, size;
        LineRenderer wave;
        ParticleSystem motes;
        bool evolved, bloomed;

        internal static PrimordialSeedVisual Show(Transform caster, SpriteRenderer body, Camera camera,
            PrimordialSeedRelic relic, bool evolved)
        {
            if (body == null) return null;

            var effect = new GameObject("Primordial Seed growth").AddComponent<PrimordialSeedVisual>();
            effect.transform.SetParent(caster, false);
            effect.started = Time.time;
            effect.evolved = evolved;
            effect.evolvedIcon = evolved ? relic.GetOwnedIcon(caster.GetComponent<PlayerHealth>()) : null;
            effect.size = evolved ? relic.EvolutionSize : relic.GrowthSize;
            effect.duration = evolved ? relic.EvolutionDuration : relic.GrowthDuration;
            effect.body = body.transform;
            effect.viewRotation = camera != null ? camera.transform.rotation : body.transform.rotation;
            effect.first = effect.CreateSprite(evolved ? "Growing seed" : "Sprout", evolved ? relic.Icon : relic.Sprout, body);
            if (!evolved && relic.Sprout != null)
            {
                effect.second = effect.CreateSprite("Side sprout", relic.Sprout, body);
                effect.second.flipX = true;
            }
            if (evolved)
            {
                effect.wave = new GameObject("Bloom wave").AddComponent<LineRenderer>();
                effect.wave.transform.SetParent(effect.transform, false);
                effect.wave.transform.localPosition = Vector3.up * .04f;
                effect.wave.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                effect.wave.useWorldSpace = false;
                effect.wave.alignment = LineAlignment.TransformZ;
                effect.wave.sharedMaterial = body.sharedMaterial;
                effect.wave.sortingLayerID = body.sortingLayerID;
                effect.wave.sortingOrder = body.sortingOrder - 1;
                effect.wave.shadowCastingMode = ShadowCastingMode.Off;
                effect.wave.receiveShadows = false;
                effect.wave.loop = true;
                effect.wave.positionCount = 32;
                effect.wave.enabled = false;
            }
            if (relic.LifeParticleMaterial != null)
                effect.CreateMotes(relic.LifeParticleMaterial, body);
            effect.LateUpdate();
            return effect;
        }

        SpriteRenderer CreateSprite(string label, Sprite sprite, SpriteRenderer styling)
        {
            var renderer = new GameObject(label).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(transform, false);
            renderer.sprite = sprite;
            renderer.sortingLayerID = styling.sortingLayerID;
            renderer.sortingOrder = styling.sortingOrder + (evolved ? 2 : -1);
            return renderer;
        }

        void CreateMotes(Material material, SpriteRenderer styling)
        {
            motes = new GameObject("Life motes").AddComponent<ParticleSystem>();
            motes.transform.SetParent(transform, false);
            motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = motes.main;
            main.duration = duration;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = motes.emission;
            emission.enabled = false;
            var renderer = motes.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingLayerID = styling.sortingLayerID;
            renderer.sortingOrder = styling.sortingOrder + 1;
            motes.Play();
            EmitMotes(false);
        }

        void EmitMotes(bool bloom)
        {
            if (motes == null) return;
            int count = bloom ? 20 : 13;
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI * 2f * i / count;
                var ray = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float life = duration * (bloom ? .43f : .56f);
                Vector3 position = bloom ? Vector3.up * .24f : ray * (size * 1.45f) + Vector3.up * .06f;
                Vector3 destination = bloom ? ray * (size * 1.60f) + Vector3.up * .57f : Vector3.up * .73f;
                if (evolved && !bloom)
                {
                    var head = Vector3.up * 1.14f + viewRotation * Vector3.up * (size * .5f + .09f);
                    position = head + viewRotation * new Vector3(ray.x, ray.z, 0f) * (size * .19f);
                    destination = head + viewRotation * new Vector3(ray.x, ray.z, 0f) * (size * .70f);
                }

                var particle = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = (destination - position) / life,
                    startLifetime = life,
                    startSize = i % 3 == 0 ? .049f : .029f,
                    startColor = bloom || i % 3 == 0 ? new Color(.65f, .36f, .035f) : new Color(.065f, .42f, .09f)
                };
                motes.Emit(particle, 1);
            }
        }

        void LateUpdate()
        {
            float progress = (Time.time - started) / duration;
            if (progress >= 1f || body == null)
            {
                Remove();
                return;
            }
            Vector3 side = viewRotation * Vector3.right;
            float fade = 1f - Mathf.InverseLerp(.70f, 1f, progress);
            if (!evolved)
            {
                float growth = Mathf.SmoothStep(.1f, 1f, Mathf.Clamp01(progress * 2.5f));
                DrawSprite(first, transform.position - side * .24f, growth, fade);
                DrawSprite(second, transform.position + side * .27f, growth * .72f, fade);
                return;
            }

            if (!bloomed && progress >= BloomTime)
            {
                bloomed = true;
                if (evolvedIcon != null) first.sprite = evolvedIcon;
                EmitMotes(true);
            }

            var anchor = transform.position + Vector3.up * 1.14f + viewRotation * Vector3.up * (size * .5f + .09f);
            float scale = Mathf.SmoothStep(.65f, 1f, Mathf.Clamp01(progress * 5f));
            DrawSprite(first, anchor, scale, fade * Mathf.Clamp01(progress * 10f));
            if (bloomed) DrawBloom(Mathf.InverseLerp(BloomTime, .94f, progress));
        }

        void DrawSprite(SpriteRenderer renderer, Vector3 anchor, float scale, float alpha)
        {
            if (renderer == null || renderer.sprite == null) return;
            if (!evolved) anchor += (viewRotation * Vector3.up) * (size * scale * .42f);
            renderer.transform.SetPositionAndRotation(anchor, viewRotation);
            renderer.transform.localScale = Vector3.one * (size * scale / renderer.sprite.bounds.size.y);
            renderer.color = new Color(1f, 1f, 1f, alpha);
        }

        void DrawBloom(float progress)
        {
            wave.enabled = progress < 1f;
            wave.widthMultiplier = Mathf.Lerp(.048f, .015f, progress);
            wave.startColor = wave.endColor = new Color(Gold.r, Gold.g, Gold.b, .73f * (1f - progress));
            float radius = Mathf.Lerp(size * .38f, size * 1.78f, Mathf.Sqrt(progress));
            for (int i = 0; i < wave.positionCount; i++)
            {
                float angle = Mathf.PI * 2f * i / wave.positionCount;
                wave.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
            }
        }

        internal void Remove()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
