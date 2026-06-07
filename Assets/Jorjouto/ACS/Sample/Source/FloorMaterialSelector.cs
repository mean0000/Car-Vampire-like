using UnityEngine;
using UnityEngine.Rendering;

namespace Jorjouto.AnimComposerSystem.Sample
{
    [RequireComponent(typeof(Renderer))]
    public class FloorMaterialSelector : MonoBehaviour
    {
        [Header("URP Material")]
        public Material urpMaterial;

        [Header("Fallback Material (Built-in / HDRP)")]
        public Material fallbackMaterial;

        void Awake()
        {
            ApplyMaterial();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            ApplyMaterial();
        }
#endif

        private void ApplyMaterial()
        {
            Renderer rend = GetComponent<Renderer>();
            if (rend == null)
                return;

            bool isURP = IsUniversalRenderPipelineActive();

            if (isURP)
            {
                if (urpMaterial != null)
                    rend.sharedMaterial = urpMaterial;
            }
            else
            {
                if (fallbackMaterial != null)
                    rend.sharedMaterial = fallbackMaterial;
            }
        }

        private static bool IsUniversalRenderPipelineActive()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null)
                return false; // Built-in RP

            var type = pipeline.GetType();
            var ns = type.Namespace;

            // Future-proof, dependency-free URP detection
            return !string.IsNullOrEmpty(ns) &&
                   ns.Contains("Universal", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
