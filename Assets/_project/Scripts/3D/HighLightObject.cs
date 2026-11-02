using UnityEngine;

namespace CCEPL_CAD
{
    [DisallowMultipleComponent]
    public class HighLightObject : MonoBehaviour
    {
        internal void Setup(Material mat, bool isHighlighted)
        {
            var GlowStrength = isHighlighted ? 1 : 0;
            mat.SetFloat("_GlowStrength", GlowStrength);
            if (gameObject.name.Contains("SAT") || gameObject.name.Contains("FAT") || gameObject.name.Contains("RAT") ||
                gameObject.name.Contains("Smoke"))
                EventManager.OnSensorHighlightChanged?.Invoke(gameObject.name, isHighlighted);
        }
    }
}