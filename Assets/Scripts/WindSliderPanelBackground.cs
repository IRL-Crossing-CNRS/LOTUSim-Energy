using UnityEngine;
using UnityEngine.UI;

namespace Lotusim
{
    [RequireComponent(typeof(RectTransform))]
    public class WindSliderPanelBackground : MonoBehaviour
    {
        [SerializeField] private int cornerRadius = 20;
        [SerializeField] private Color backgroundColor = new Color(0.8f, 0.8f, 0.8f, 0.85f);

        private void Awake()
        {
            Image bg = GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();

            bg.sprite = UISpriteFactory.CreateRoundedRectSprite(cornerRadius, color: backgroundColor);
            bg.type = Image.Type.Sliced;

            transform.SetAsFirstSibling();
        }
    }
}