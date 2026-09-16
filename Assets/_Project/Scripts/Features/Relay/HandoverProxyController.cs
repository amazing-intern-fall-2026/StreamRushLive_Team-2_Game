using TMPro;
using UnityEngine;

namespace SteamRush.Relay
{
    // View: chỉ hiển thị Tên (tạm thời/thật) của người chơi tiếp theo trên Model đứng chờ tại
    // mốc chuyển gậy - không tự xử lý logic bàn giao, không tự huỷ (BatonHandoverController lo
    // việc đó) - SRP, không truy cập module khác.
    public class HandoverProxyController : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameLabel;

        // avatar: chưa có nguồn dữ liệu avatar viewer thật (chờ module StreamIntegration) -
        // tham số giữ chỗ sẵn, hiện tại luôn null.
        public void SetInfo(string displayName, Sprite avatar)
        {
            if (nameLabel != null)
            {
                nameLabel.text = displayName;
            }
        }
    }
}
