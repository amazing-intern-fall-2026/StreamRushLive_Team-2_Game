using UnityEngine;
using UnityEngine.UI;

namespace SteamRush.Features.UI.Views
{
    // Nut bat/tat 1 GameObject muc tieu khi bam - dung cho panel huong dan choi dang thu gon
    // sau nut "?" thay vi hien thuong truc, tranh chiem dien tich man hinh.
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
