using UnityEngine;

namespace SpiderSurge.Enemies
{
    public class ShieldedEnemyVisuals : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] renderers;
        [SerializeField] private Color[] unshieldedColors;
        [SerializeField] private Color shieldColor;

        public void Initialize(SpriteRenderer[] enemyRenderers, Color color)
        {
            renderers = enemyRenderers;
            unshieldedColors = new Color[renderers.Length];
            shieldColor = color;
            for (int i = 0; i < renderers.Length; i++)
            {
                unshieldedColors[i] = renderers[i].color;
            }
        }

        private void OnEnable()
        {
            if (renderers == null) return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color color = Color.Lerp(unshieldedColors[i], shieldColor, 0.7f);
                color.a = unshieldedColors[i].a;
                renderers[i].color = color;
            }
        }

        private void OnDisable()
        {
            if (renderers == null) return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].color = unshieldedColors[i];
            }
        }
    }
}
