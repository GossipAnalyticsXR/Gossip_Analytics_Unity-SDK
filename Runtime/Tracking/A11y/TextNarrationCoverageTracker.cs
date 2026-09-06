using UnityEngine;
using UnityEngine.UI;
using GossipSDK.A11y;
using GossipSDK.Core;
using GossipSDK.Tracking.A11y;

namespace GossipSDK.Tracking.A11y
{
    public class TextNarrationCoverageTracker : MonoBehaviour
    {
        private void Start()
        {
            Evaluate();
        }

        public void Evaluate()
        {
            var graphics = FindObjectsOfType<Graphic>(true);

            int denominator = 0;
            int numerator = 0;

            foreach (var graphic in graphics)
            {
                bool isText =
                    graphic is Text ||
                    graphic.GetType().Name == "TextMeshProUGUI";

                if (!isText)
                    continue;

                var label = graphic.GetComponent<GossipA11yLabel>();
                if (label != null && label.decorative)
                    continue;

                denominator++;

                if (label != null && !string.IsNullOrWhiteSpace(label.label))
                    numerator++;
            }

            Gossip.Instance?.A11yTracker?.CapCheck(
                metricKey: "text_narration_coverage",
                numerator: numerator,
                denominator: denominator,
                scope: "screen",
                meta: BuildMeta()
            );
        }

        private A11yTracker.MetaData BuildMeta()
        {
            return new A11yTracker.MetaData
            {
                platform = Application.platform.ToString(),
                app_version = Application.version,
                sdk_version = Constants.SdkVersion,
                locale = Application.systemLanguage.ToString(),
                scene_id = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            };
        }
    }
}
