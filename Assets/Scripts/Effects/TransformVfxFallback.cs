using UnityEngine;
using ProjectScale.Core;

namespace ProjectScale.Effects
{
    /// <summary>
    /// Резервный визуал для трансформации, если VFX Graph недоступен.
    /// Использует встроенный ParticleSystem с настройками "вспышки" и кольца частиц.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class TransformVfxFallback : MonoBehaviour
    {
        private ParticleSystem _ps;

        private void Awake()
        {
            _ps = GetComponent<ParticleSystem>();
            ConfigureSystem();
        }

        private void OnEnable()
        {
            GameEvents.OnScaleChanged += HandleScaleChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnScaleChanged -= HandleScaleChanged;
        }

        private void ConfigureSystem()
        {
            var main = _ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.startLifetime = 0.55f;
            main.startSpeed = 6f;
            main.startSize = 0.25f;
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            var emission = _ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBurst(0, new ParticleSystem.Burst(0f, 80));

            var shape = _ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.4f;

            var colorOverLifetime = _ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;
        }

        private void HandleScaleChanged(ScaleState _, ScaleState newState)
        {
            var main = _ps.main;
            main.startColor = newState switch
            {
                ScaleState.Small  => new Color(0.4f, 0.85f, 1f),
                ScaleState.Normal => Color.white,
                ScaleState.Big    => new Color(1f, 0.55f, 0.15f),
                _ => Color.white
            };
            _ps.Play();
        }
    }
}
