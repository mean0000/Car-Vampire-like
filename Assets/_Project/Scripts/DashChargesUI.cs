using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대시 스택 HUD. PlayerController.Instance를 매 프레임 폴링한다(HUDController/BoostGaugeUI와 동일 패턴 — 대시는
/// 스택 변경 이벤트를 노출하지 않으므로 폴링이 정석).
///
/// 아이콘은 MaxDashCharges 개수에 맞춰 런타임에 코드로 생성한다 — 씬엔 이 컴포넌트가 붙은 빈 RectTransform 하나만
/// 두면 되고, 스택 수를 인스펙터에서 바꿔도 UI가 자동으로 따라온다.
///
/// 표시 규칙(슬롯 i, 좌→우):
/// - i &lt; 보유스택        → 가득 참(filledColor, fill=1)
/// - i == 보유스택(충전 중) → 충전 진행도만큼 아래에서 차오름(chargingColor, fill=진행도)
/// - i &gt; 보유스택        → 빈 슬롯(fill=0, 배경 윤곽만)
/// </summary>
public class DashChargesUI : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("스택 아이콘 한 변 크기(px).")]
    [SerializeField] float iconSize = 26f;
    [Tooltip("아이콘 사이 간격(px).")]
    [SerializeField] float spacing = 8f;

    [Header("Colors")]
    [Tooltip("가득 찬 스택 색(스캔펄스 시안과 맞물림).")]
    [SerializeField] Color filledColor = new Color(0.4f, 0.9f, 1f, 0.95f);
    [Tooltip("충전 중인 스택 색(반투명).")]
    [SerializeField] Color chargingColor = new Color(0.4f, 0.9f, 1f, 0.5f);
    [Tooltip("빈 슬롯 배경 윤곽 색.")]
    [SerializeField] Color emptyColor = new Color(1f, 1f, 1f, 0.12f);

    Image[] _fills;        // 슬롯별 앞면(충전/가득 표시). 배경은 부모 슬롯 Image.
    int _builtFor = -1;    // 마지막으로 구성한 MaxDashCharges — 변하면 재구성
    Sprite _sprite;        // 1x1 흰 스프라이트 공유(단색 사각, Filled에 필요)

    void Update()
    {
        var pc = PlayerController.Instance;
        if (pc == null) return;

        if (_builtFor != pc.MaxDashCharges) Build(pc.MaxDashCharges);
        if (_fills == null) return;

        int charges = pc.DashCharges;
        float prog = pc.DashRechargeProgress01;
        for (int i = 0; i < _fills.Length; i++)
        {
            if (i < charges) { _fills[i].fillAmount = 1f; _fills[i].color = filledColor; }
            else if (i == charges) { _fills[i].fillAmount = prog; _fills[i].color = chargingColor; }
            else { _fills[i].fillAmount = 0f; }
        }
    }

    void OnDestroy()
    {
        // Sprite.Create로 만든 공유 스프라이트는 GC 대상이 아니므로 명시적으로 해제.
        if (_sprite != null) Destroy(_sprite);
    }

    void Build(int max)
    {
        // 스택 수 변동 시 기존 아이콘 폐기 후 재구성.
        for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);

        if (_sprite == null)
            _sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));

        max = Mathf.Max(0, max);
        _fills = new Image[max];
        for (int i = 0; i < max; i++)
        {
            // 빈 슬롯 배경(항상 보이는 윤곽).
            var slot = NewImage("DashSlot" + i, transform);
            slot.color = emptyColor;
            var rt = slot.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;   // 부모(이 컴포넌트) 좌하단 기준 가로 배치
            rt.pivot = Vector2.zero;
            rt.sizeDelta = new Vector2(iconSize, iconSize);
            rt.anchoredPosition = new Vector2(i * (iconSize + spacing), 0f);

            // 충전/가득 fill(아래→위로 차오름).
            var fill = NewImage("Fill", slot.transform);
            fill.color = filledColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = 1f;
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;   // 슬롯에 꽉 채움
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;

            _fills[i] = fill;
        }
        _builtFor = max;
    }

    Image NewImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = _sprite;
        img.raycastTarget = false;   // HUD 장식 — 입력 가로채지 않게
        return img;
    }
}
