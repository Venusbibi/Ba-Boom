using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StarRatingHUD : MonoBehaviour
{
    public static StarRatingHUD Instance { get; private set; }

    [Header("Star Score Thresholds (เกณฑ์คะแนนแต่ละดาว)")]
    public int star1Score = 3000;
    public int star2Score = 8000;
    public int star3Score = 15000;

    [Header("Score Source (ดึงค่าจาก Text คะแนนเดิม)")]
    public TMP_Text legacyScoreText;          // ลาก Text แสดงคะแนนเดิมมาใส่ช่องนี้
    public bool hideLegacyScoreText = true;   // ติ๊กถูกเพื่อซ่อนตัวเลขคะแนนเดิม ให้โชว์เฉพาะดาว

    [Header("Star UI Icons (รูปดาว 3 ดวงบนหน้าจอ)")]
    public Image[] starIcons;                 // ใส่ Image ดาวเรียงลำดับ: Element 0 = ดาว 1, 1 = ดาว 2, 2 = ดาว 3
    public Sprite emptyStarSprite;            // รูปดาวสีเทา/ดาวว่าง (ถ้าไม่มีใช้เปลี่ยนสีแทนได้)
    public Sprite filledStarSprite;           // รูปดาวสีทองตอนปลดล็อก
    public Color lockedTint = new Color(0.35f, 0.35f, 0.35f, 0.85f);
    public Color unlockedTint = Color.white;

    [Header("Optional Progress Gauge (หลอดเกจสะสมดาว)")]
    public Image starGaugeFill;               // ใส่ Image แบบ Filled Horizontal (ปล่อยว่างได้ถ้าไม่ใช้หลอด)

    [Header("Juicy Pop Effect (เด้งขยายตอนได้ดาว)")]
    public float popScale = 1.45f;
    public float popDuration = 0.35f;

    private int currentScore = 0;
    private int earnedStars = 0;
    private int lastParsedHash = -1;

    public int EarnedStars => earnedStars;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (legacyScoreText != null && hideLegacyScoreText)
        {
            // ซ่อนตัวเลขเดิมแต่ยังให้สคริปต์อ่านค่าคะแนนเบื้องหลังได้
            legacyScoreText.alpha = 0f;
        }

        RefreshVisuals(playPopAnimation: false);
    }

    private void Update()
    {
        // อ่านค่าตัวเลขจาก Text คะแนนเดิมแบบเรียลไทม์
        if (legacyScoreText != null)
        {
            string raw = legacyScoreText.text;
            int hash = raw.GetHashCode();
            if (hash != lastParsedHash)
            {
                lastParsedHash = hash;
                int parsed = ExtractFirstNumber(raw);
                if (parsed != currentScore)
                {
                    UpdateScore(parsed);
                }
            }
        }
    }

    public void UpdateScore(int newScore)
    {
        currentScore = Mathf.Max(0, newScore);
        RefreshVisuals(playPopAnimation: true);
    }

    private void RefreshVisuals(bool playPopAnimation)
    {
        int newStars = 0;
        if (currentScore >= star3Score) newStars = 3;
        else if (currentScore >= star2Score) newStars = 2;
        else if (currentScore >= star1Score) newStars = 1;

        // อัปเดตหลอดเกจ (แบ่งสัดส่วน 3 ช่วงเท่ากันตามตำแหน่งดาว 1, 2, 3)
        if (starGaugeFill != null)
        {
            float fill = 0f;
            if (currentScore <= star1Score)
            {
                fill = Mathf.Clamp01((float)currentScore / Mathf.Max(1, star1Score)) * 0.333f;
            }
            else if (currentScore <= star2Score)
            {
                float seg = (float)(currentScore - star1Score) / Mathf.Max(1, star2Score - star1Score);
                fill = 0.333f + Mathf.Clamp01(seg) * 0.333f;
            }
            else
            {
                float seg = (float)(currentScore - star2Score) / Mathf.Max(1, star3Score - star2Score);
                fill = 0.666f + Mathf.Clamp01(seg) * 0.334f;
            }
            starGaugeFill.fillAmount = fill;
        }

        // อัปเดตรูปดาวทั้ง 3 ดวง
        for (int i = 0; i < starIcons.Length; i++)
        {
            if (starIcons[i] == null) continue;

            bool isUnlocked = (i < newStars);
            bool justUnlocked = isUnlocked && (i >= earnedStars);

            if (isUnlocked)
            {
                if (filledStarSprite != null) starIcons[i].sprite = filledStarSprite;
                starIcons[i].color = unlockedTint;

                if (playPopAnimation && justUnlocked)
                {
                    StartCoroutine(PopStarRoutine(starIcons[i].rectTransform));
                }
            }
            else
            {
                if (emptyStarSprite != null) starIcons[i].sprite = emptyStarSprite;
                starIcons[i].color = lockedTint;
            }
        }

        earnedStars = newStars;
    }

    private IEnumerator PopStarRoutine(RectTransform target)
    {
        float elapsed = 0f;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / popDuration);
            float scale = (t < 0.45f)
                ? Mathf.Lerp(1f, popScale, t / 0.45f)
                : Mathf.Lerp(popScale, 1f, (t - 0.45f) / 0.55f);

            target.localScale = Vector3.one * scale;
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    private int ExtractFirstNumber(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        int result = 0;
        bool foundDigit = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c >= '0' && c <= '9')
            {
                foundDigit = true;
                result = (result * 10) + (c - '0');
            }
            else if (c == ',' && foundDigit)
            {
                continue; // ข้ามเครื่องหมายจุลภาค เช่น 47,400 -> 47400
            }
            else if (foundDigit)
            {
                break; // หยุดอ่านเมื่อจบตัวเลขชุดแรก (เช่น หยุดก่อนเครื่องหมาย / 1,000)
            }
        }

        return result;
    }
}