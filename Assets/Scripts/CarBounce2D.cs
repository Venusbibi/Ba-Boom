using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class CarBounce2D : MonoBehaviour
{
    private enum BounceState
    {
        Idle,
        Bouncing,
        Stopped
    }

    [Header("Bounce Physics (แรงดีดและการชิ่ง)")]
    [SerializeField] private float launchSpeed = 14f;          // ความเร็วเริ่มต้นตอนรถกระเด็น
    [SerializeField] private float linearDamping = 0.75f;      // แรงเสียดทาน (ค่าน้อย = เด้งชิ่งได้ไกลและนานขึ้น)
    [SerializeField] private float spinSpeed = 720f;           // ความเร็วการหมุนติ้วกลางอากาศ (องศา/วินาที)
    [SerializeField] private float minComboSpeed = 1.8f;       // ความเร็วขั้นต่ำที่ยังชนแล้วเกิดคอมโบ
    [SerializeField] private float stopSpeedThreshold = 0.4f;  // ความเร็วที่ถือว่ารถหยุดนิ่งและเลิกคอมโบ
    [SerializeField] private bool bounceOffScreenEdges = true; // ชิ่งขอบจอไม่ให้รถหลุดออกนอกฉาก

    [Header("Hop Visuals (จังหวะเด้งขึ้น-ลง)")]
    [SerializeField] private float hopFrequency = 3.5f;        // ความถี่ในการเด้งขึ้น-ลง
    [SerializeField] private float maxHopScale = 1.7f;         // ขนาดขยายสูงสุดตอนลอยกลางอากาศ

    [Header("Wrecked State (สภาพรถพังหลังระเบิด)")]
    [SerializeField] private Sprite wreckedCarSprite;          // รูปซากรถพัง (ถ้าไม่มีระบบจะปรับสีรถให้ดำไหม้แทน)
    [SerializeField] private Color burnedTint = new Color(0.28f, 0.28f, 0.28f, 1f);
    [SerializeField] private GameObject impactSparkPrefab;     // เอฟเฟกต์ตอนรถกระแทกเป้าหมาย (ถ้ามี)

    private Rigidbody2D rb;
    private Camera mainCam;
    private SpriteRenderer[] spriteRenderers;
    private Vector3 baseScale;

    private BounceState currentState = BounceState.Idle;
    private Action onBounceFinishedCallback;
    private float launchTime;
    private float hopPhase;
    private readonly Dictionary<GameObject, float> hitCooldowns = new Dictionary<GameObject, float>();

    public bool IsBouncing => currentState == BounceState.Bouncing;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.linearDamping = linearDamping;

        mainCam = Camera.main;
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        baseScale = transform.localScale;
    }

    // ชื่อเมธอดและพารามิเตอร์ตรงกับที่ ChainExplodable.cs เรียกใช้
    public void StartBounce(Vector2 sourcePosition, Action onBounceFinished)
    {
        if (currentState != BounceState.Idle)
        {
            onBounceFinished?.Invoke();
            return;
        }

        currentState = BounceState.Bouncing;
        onBounceFinishedCallback = onBounceFinished;
        launchTime = Time.time;
        hopPhase = 0f;
        hitCooldowns.Clear();

        // 1. ปิดระบบวิ่งตามถนนเดิม
        foreach (var move in GetComponentsInChildren<RoadCarMovement>())
        {
            move.enabled = false;
        }

        // 2. ตั้ง Collider เป็น Trigger เพื่อให้รถลอยข้ามทางเท้าไปชนตึกได้
        foreach (var col in GetComponentsInChildren<Collider2D>())
        {
            col.isTrigger = true;
        }

        // 3. เปลี่ยนเป็นรูปรถพัง/รถไหม้ทันที และดัน Sorting Order ให้อยู่บนสุดระหว่างลอย
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer sr = spriteRenderers[i];
            if (sr == null) continue;

            sr.sortingOrder += 20;

            if (wreckedCarSprite != null && i == 0)
            {
                sr.sprite = wreckedCarSprite;
                sr.color = Color.white;
            }
            else
            {
                sr.color = burnedTint;
            }
        }

        // 4. ส่งแรงดีดตัวหนีจากจุดระเบิดพร้อมสุ่มมุมแฉลบเล็กน้อย
        Vector2 rawDir = (Vector2)transform.position - sourcePosition;
        Vector2 launchDir = (rawDir.sqrMagnitude < 0.001f)
            ? UnityEngine.Random.insideUnitCircle.normalized
            : (Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-25f, 25f)) * rawDir.normalized);

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.simulated = true;
        rb.linearVelocity = launchDir.normalized * launchSpeed;
        rb.angularVelocity = spinSpeed * (UnityEngine.Random.value > 0.5f ? 1f : -1f);
    }

    private void FixedUpdate()
    {
        if (currentState != BounceState.Bouncing)
            return;

        float currentSpeed = rb.linearVelocity.magnitude;

        // เด้งชิ่งขอบหน้าจอกล้อง
        if (bounceOffScreenEdges && mainCam != null)
        {
            HandleScreenEdgeBounce();
        }

        // จำลองการเด้งขึ้น-ลง (ขยายสเกลตามความสูง) แปรผันตามความเร็วที่เหลืออยู่
        float speedRatio = Mathf.Clamp01(currentSpeed / launchSpeed);
        hopPhase += Time.fixedDeltaTime * hopFrequency * Mathf.PI * (0.45f + speedRatio);
        float hopHeight = Mathf.Abs(Mathf.Sin(hopPhase));
        float targetScale = Mathf.Lerp(1f, Mathf.Lerp(1f, maxHopScale, speedRatio), hopHeight);
        transform.localScale = baseScale * targetScale;

        // เมื่อความเร็วลดลงต่ำกว่าเกณฑ์ -> หยุดรถและเลิกนับคอมโบ
        if (Time.time - launchTime > 0.15f && currentSpeed <= stopSpeedThreshold)
        {
            StopBouncing();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // ถ้าหยุดเด้งแล้ว หรือความเร็วต่ำกว่าเกณฑ์ จะไม่เกิดคอมโบอีก
        if (currentState != BounceState.Bouncing)
            return;

        if (rb.linearVelocity.magnitude < minComboSpeed)
            return;

        // ข้ามโซนวางระเบิด ตัวแม่ของฉาก และเศษซากชิ้นเล็ก
        if (other.gameObject.name == "PlacementZone" ||
            other.gameObject.name == "City Prop" ||
            other.GetComponentInParent<BuildingDebris2D>() != null)
            return;

        ChainExplodable target = other.GetComponentInParent<ChainExplodable>();
        if (target == null || target.gameObject == this.gameObject)
            return;

        if (target.destructibleType == DestructibleType.PlayerBomb)
            return;

          // ป้องกันการชนเป้าหมายเดิมซ้ำถี่เกินไปในเสี้ยววินาที
        if (hitCooldowns.TryGetValue(target.gameObject, out float nextHitTime) && Time.time < nextHitTime)
            return;

        hitCooldowns[target.gameObject] = Time.time + 0.25f;

        // สั่งให้ตึกหรือรถคันอื่นที่โดนชนระเบิด/ล้ม/เด้งต่อเป็นคอมโบ!
        target.Explode(transform.position);

        if (impactSparkPrefab != null)
        {
            Instantiate(impactSparkPrefab, transform.position, Quaternion.identity);
        }

        // สะท้อนทิศทางรถให้เด้งชิ่งกลับออกไปหาเป้าหมายอื่นต่อ
        Vector2 surfaceNormal = ((Vector2)transform.position - (Vector2)other.bounds.center).normalized;
        if (surfaceNormal.sqrMagnitude > 0.01f)
        {
            Vector2 reflected = Vector2.Reflect(rb.linearVelocity, surfaceNormal);
            reflected = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-15f, 15f)) * reflected;
            rb.linearVelocity = reflected * 0.9f;
            rb.angularVelocity = -rb.angularVelocity;
        }
    }

    private void HandleScreenEdgeBounce()
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

    private void StopBouncing()
    {
        currentState = BounceState.Stopped;

        // หยุดการเคลื่อนที่ทั้งหมดและล็อกซากรถให้อยู่กับที่บนฉาก
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        transform.localScale = baseScale;

        foreach (var sr in spriteRenderers)
        {
            if (sr != null)
            {
                sr.sortingOrder -= 20;
            }
        }

        // แจ้งกลับไปที่ ChainExplodable ว่าเด้งเสร็จแล้ว (เพื่อให้ GameResultManager นับจบด่านได้)
        onBounceFinishedCallback?.Invoke();
        onBounceFinishedCallback = null;
    }
}