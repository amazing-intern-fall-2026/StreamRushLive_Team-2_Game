using UnityEngine;
using UnityEngine.UI;

namespace SteamRush.Features.UI.Views
{
    // Toggles visibility of target GameObject on button click.
    [RequireComponent(typeof(Button))]
    public class ToggleVisibilityButton : MonoBehaviour
    {
        [SerializeField] private GameObject target;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Toggle);
        }

        public void Toggle()
        {
            if (target != null)
            {
                target.SetActive(!target.activeSelf);
            }
        }
    }
}
