using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

namespace  CCEPL_CAD
{

    public class SplashScreenController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private CanvasGroup splashCanvas;

        [Header("Timing")]
        [SerializeField] private float fadeInDuration = 1f;
        [SerializeField] private float stayDuration = 2f;
        [SerializeField] private float fadeOutDuration = 1f;

        [Header("Target")]
        [SerializeField] private string targetScene;

        /// <summary>
        /// Initiates the splash screen animation sequence when the scene starts.
        /// </summary>
        private void Start()
        {
            PlaySplash();
        }

        /// <summary>
        /// Animates the splash screen fade in, pause, and fade out sequence using DOTween, then loads the target scene upon completion.
        /// </summary>
        private void PlaySplash()
        {
            splashCanvas.alpha = 0f;

            Sequence seq = DOTween.Sequence();

            seq.Append(splashCanvas.DOFade(1f, fadeInDuration))
                .AppendInterval(stayDuration)
                .Append(splashCanvas.DOFade(0f, fadeOutDuration))
                .OnComplete(() =>
                {
                    SceneManager.LoadScene(targetScene);
                });
        }
    }
}
