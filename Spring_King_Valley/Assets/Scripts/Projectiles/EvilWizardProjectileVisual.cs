using UnityEngine;

namespace Vampire
{
    public class EvilWizardProjectileVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float frameTime = 0.06f;

        private float timer;
        private int frameIndex;

        public void Setup(SpriteRenderer renderer, Sprite[] animationFrames, float animationFrameTime = 0.06f)
        {
            targetRenderer = renderer;
            frames = animationFrames;
            frameTime = Mathf.Max(0.02f, animationFrameTime);
            frameIndex = 0;
            timer = 0f;
            ApplyFrame();
        }

        private void OnEnable()
        {
            frameIndex = 0;
            timer = 0f;
            ApplyFrame();
        }

        private void Update()
        {
            if (targetRenderer == null || frames == null || frames.Length <= 1)
                return;

            timer += Time.deltaTime;
            if (timer < frameTime)
                return;

            timer -= frameTime;
            frameIndex = (frameIndex + 1) % frames.Length;
            ApplyFrame();
        }

        private void ApplyFrame()
        {
            if (targetRenderer == null || frames == null || frames.Length == 0)
                return;

            targetRenderer.sprite = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
        }
    }
}
