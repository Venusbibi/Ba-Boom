using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class CarBounce2D : MonoBehaviour
{
    [Header("Wrecked Visuals (เปลี่ยนสภาพเป็นรถพัง)")]
    public Sprite wreckedCarSprite;         // ลากรูปซากรถพังมาใส่ (ถ้าไม่มีระบบจะปรับสีรถให้ดำไหม้แทน)
    public bool charDarkIfNoSprite = true;  // ปรับสีรถเป็นสีดำไหม้อัตโนมัติหากไม่ได้ใส่รูปซากรถ
    public bool keepWreckedCarOnMap = true; // ทิ้งซากรถพังค้างไว้บนฉากหลังเด้งหยุด
    public GameObject hitBuildingEffect;    // เอฟเฟกต์ระเบิด/ควันตอนรถกระเด็นไปชนตึก (ถ้ามี)

    [Header("Bounce & Ricochet Physics")]
    public float launchSpeed = 14f;         // ความแรงตอนรถกระเด็นไปชนตึก
    public float linearDamping = 0.55f;     // แรงต้าน (ค่าน้อย = รถเด้งชิ่งได้ไกลและนานขึ้น)
    public float spinSpeed = 720f;          // ความเร็วในการหมุนติ้วกลางอากาศ
    public bool bounceOffScreenEdges = true;// เด้งชิ่งขอบจอไม่ให้หลุดออกนอกฉาก

    [Header("Multi-Hop Settings (จังหวะการเด้ง)")]
    [Min(1)]
    public int bounceCount = 4;             // จำนวนครั้งที่รถเด้งขึ้น-ลง
    public float firstBounceDuration = 0.42f;
    public float firstBounceScale = 1.75f;  // ขยายขนาดตอนลอยสูงสุด
    [Range(0.4f, 0.9f)]
    public float bounceDecay = 0.7f;

    [Header("Combo Impact")]
    public bool canHitBuildings = true;     // เปิดให้ชนตึกและวัตถุอื่นเพื่อเพิ่มคอมโบ
    public float impactRadius = 0.85f;      // รัศมีแรงกระแทกตอนตกกระแทกพื้น

    private Rigidbody2D rb;
    private Camera mainCam;
    private bool isBouncing = false;
    private float lastRicochetTime = -1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.linearDamping = linearDamping;
        mainCam = Camera.main;
    }

    public void StartBounce(Vector2 sourcePosition, Action onBounceFinished)
    {
        if (isBouncing)
        {
            onBounceFinished?.Invoke();
            return;
        }

        isBouncing = true;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.simulated = true;

        // ปิดระบบวิ่งตามถนนเดิม (เผื่อใช้กับรถที่วิ่งบนถนนด้วย)
        foreach (var move in GetComponentsInChildren<RoadCarMovement>())
        {
            move.enabled = false;
        }

        // เปลี่ยน Collider เป็น Trigger เพื่อให้รถลอยข้ามทางเท้าและรั้วไปชนตึกได้
        foreach (var col in GetComponentsInChildren<Collider2D>())
        {
            col.isTrigger = true;
        }

        // 1. เปลี่ยนภาพเป็น "รถพัง" ทันทีที่ระเบิด!
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder += 20; // ดันให้อยู่ชั้นบนสุดระหว่างลอยเด้ง

            if (wreckedCarSprite != null)
            {
                sr.sprite = wreckedCarSprite;
                sr.color = Color.white;
            }
            else if (charDarkIfNoSprite)
            {
                sr.color = new Color(0.32f, 0.32f, 0.32f, 1f); // สีเทาดำเหมือนรถถูกไฟไหม้
            }
        }

        // 2. คำนวณทิศทางดีดตัวหนีจากจุดระเบิดเพื่อพุ่งไปหาตึก
        Vector2 bounceDir = (Vector2)transform.position - sourcePosition;
        if (bounceDir.sqrMagnitude < 0.001f)
        {
            bounceDir = UnityEngine.Random.insideUnitCircle.normalized;
        }
        else
        {
            float randomAngle = UnityEngine.Random.Range(-25f, 25f);
            bounceDir = Quaternion.Euler(0, 0, randomAngle) * bounceDir.normalized;
        }

        StartCoroutine(BounceRoutine(bounceDir, sr, onBounceFinished));
    }

    private void Update()
    {
        if (isBouncing && bounceOffScreenEdges && mainCam != null)
        {
            KeepInsideCameraAndBounce();
        }
    }

    private IEnumerator BounceRoutine(Vector2 bounceDir, SpriteRenderer sr, Action onBounceFinished)
    {
        Vector3 baseScale = transform.localScale;

        rb.linearVelocity = bounceDir * launchSpeed;
        float spinDir = (UnityEngine.Random.value > 0.5f) ? 1f : -1f;
        rb.angularVelocity = spinSpeed * spinDir;

        float currentDuration = firstBounceDuration;
        float currentPeakScale = firstBounceScale;

        for (int i = 0; i < bounceCount; i++)
        {
            float elapsed = 0f;

            while (elapsed < currentDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / currentDuration);

                float heightCurve = Mathf.Sin(t * Mathf.PI);
                float scaleMultiplier = Mathf.Lerp(1f, currentPeakScale, heightCurve);
                transform.localScale = baseScale * scaleMultiplier;

                yield return null;
            }

            // จังหวะรถตกกระแทกพื้น -> เช็กชนตึกรอบตัว
            transform.localScale = baseScale;
            TriggerAreaImpact();

            if (rb.linearVelocity.sqrMagnitude > 0.5f)
            {
                float jitter = UnityEngine.Random.Range(-30f, 30f);
                rb.linearVelocity = Quaternion.Euler(0, 0, jitter) * rb.linearVelocity;
                rb.angularVelocity = -rb.angularVelocity * 0.85f;
            }

            currentDuration *= bounceDecay;
            currentPeakScale = 1f + ((currentPeakScale - 1f) * bounceDecay);
        }

        // จบการเด้ง -> หยุดนิ่งเป็นซากรถพังบนฉาก
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        transform.localScale = baseScale;
        isBouncing = false;

        if (sr != null)
        {
            sr.sortingOrder -= 20;
        }

        onBounceFinished?.Invoke();

        if (!keepWreckedCarOnMap)
        {
            Destroy(gameObject);
        }
    }

    private void KeepInsideCameraAndBounce()
    {
        Vector3 vp = mainCam.WorldToViewportPoint(transform.position);
        Vector2 vel = rb.linearVelocity;
        bool bounced = false;

        if (vp.x < 0.04f && vel.x < 0f) { vel.x = Mathf.Abs(vel.x); bounced = true; }
        else if (vp.x > 0.96f && vel.x > 0f) { vel.x = -Mathf.Abs(vel.x); bounced = true; }

        if (vp.y < 0.05f && vel.y < 0f) { vel.y = Mathf.Abs(vel.y); bounced = true; }
        else if (vp.y > 0.88f && vel.y > 0f) { vel.y = -Mathf.Abs(vel.y); bounced = true; }

        if (bounced)
        {
            rb.linearVelocity = vel;
            rb.angularVelocity = -rb.angularVelocity;
        }
    }

    // ชนตึกหรือรถคันอื่นระหว่างกระเด็น -> สั่งระเบิดเพิ่มคอมโบและเด้งชิ่งต่อ
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isBouncing || !canHitBuildings) return;
        if (other.GetComponentInParent<BuildingDebris2D>() != null) return;

        ChainExplodable target = other.GetComponentInParent<ChainExplodable>();
        if (target != null && target.gameObject != this.gameObject && target.destructibleType != DestructibleType.PlayerBomb)
        {
            // สั่งให้ตึกที่โดนชนระเบิดและล้มต่อ (บวก Combo อัตโนมัติ)
            target.Explode(transform.position);

            if (hitBuildingEffect != null)
            {
                Instantiate(hitBuildingEffect, transform.position, Quaternion.identity);
            }

            // เด้งชิ่งสะท้อนกลับไปหาตึกอื่นต่อ
            if (Time.time - lastRicochetTime > 0.08f)
            {
                lastRicochetTime = Time.time;
                Vector2 normal = ((Vector2)transform.position - (Vector2)other.transform.position).normalized;
                if (normal.sqrMagnitude > 0.01f)
                {
                    rb.linearVelocity = Vector2.Reflect(rb.linearVelocity, normal);
                    rb.angularVelocity = -rb.angularVelocity;
                }
            }
        }
    }

    private void TriggerAreaImpact()
    {
        if (!canHitBuildings) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, impactRadius);
        foreach (Collider2D hit in hits)
        {
            ChainExplodable target = hit.GetComponentInParent<ChainExplodable>();
            if (target != null && target.gameObject != this.gameObject && target.destructibleType != DestructibleType.PlayerBomb)
            {
                target.Explode(transform.position);
            }
        }
    }
}