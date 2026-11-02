using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCEPL_CAD
{
    public class SliderForSpeed : MonoBehaviour
    {
        public Slider SpeedSlider;
        public EventTrigger SliderEventTrigger;
        public TMP_Text NameText;
        public TMP_Text MinValue;
        public TMP_Text MaxValue;
    }
}