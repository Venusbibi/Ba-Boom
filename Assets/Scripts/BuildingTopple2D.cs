using System;
using System.Collections;
using UnityEngine;

public class BuildingTopple2D : MonoBehaviour
{
    [Header("Domino Topple Settings")]
    public float buildingHeight = 2.4f;     // ระยะเอื้อมไปชนตึกถัดไปตอนล้ม
    public float buildingWidth = 1.1f;      // ความกว้างของแนวตึก
    public float fallDuration = 0.42f;      // เวลาที่ใช้ในการล้มจนฟาดพื้น
    [Range(0.4f, 0.95f)]
    public float impactTriggerRatio = 0.65f;// จังหวะที่เอียงไปชนตึกถัดไป
    public LayerMask targetLayer;

    [Header("Collapse Smoke & Dust Particles (ควันฝุ่นตอนตึกถล่ม)")]
    public GameObject baseCrackSmokePrefab;     // ควัน/ฝุ่นเล็กๆ ตรงฐานตึกตอนเริ่มเอียงล้ม
    public GameObject collapseDustCloudPrefab;  // กลุ่มควันฝุ่นฟุ้งกระจายตอนตึกฟาดพื้นถล่ม
    public float smokeDestroyDelay = 3.5f;      // เวลาลบออบเจกต์ควันอัตโนมัติหลังเล่นจบ

    [Header("Visuals")]
    public Transform visualTransform;
    public Sprite rubbleSprite;             // รูปซากตึกหลังระเบิด (ถ้ามี)
    public bool destroyIfNoRubble = true;

    private bool isToppling = false;

    private void Awake()
    {
        if (visualTransform == null)
        {
            visualTransform = transform;
        }
    }

    public void StartTopple(Vector2 sourcePosition, Action onSlamAndExplode)
    {
        if (isToppling)
        {
            onSlamAndExplode?.Invoke();
            return;
        }

        isToppling = true;

        // ปิด Collider ทันทีที่เริ่มล้ม เพื่อไม่ให้โดนชนซ้ำ
        foreach (var col in GetComponentsInChildren<Collider2D>())
        {
            col.enabled = false;
        }

        Vector2 rawDir = (Vector2)transform.position - sourcePosition;
        if (rawDir.sqrMagnitude < 0.001f)
        {
            rawDir = Vector2.right;
        }

        Vector2 fallDir = SnapToFourDirections(rawDir.normalized);

        // 1. เสกควัน/ฝุ่นตรงฐานตึกจังหวะเริ่มแตกร้าวและเอนล้ม
        SpawnSmokeEffect(baseCrackSmokePrefab, transform.position, fallDir);

        StartCoroutine(DominoRoutine(fallDir, onSlamAndExplode));
    }

    private IEnumerator DominoRoutine(Vector2 fallDir, Action onSlamAndExplode)
    {
        float elapsed = 0f;
        bool hasTriggeredNextDomino = false;

        Vector3 startPos = visualTransform.localPosition;
        Quaternion startRot = visualTransform.localRotation;
        Vector3 startScale = visualTransform.localScale;

        SpriteRenderer sr = visualTransform.GetComponent<SpriteRenderer>();
        int originalOrder = 0;
        if (sr != null)
        {
            originalOrder = sr.sortingOrder;
            sr.sortingOrder += 5; // ดันให้อยู่เหนือตึกข้างๆ ระหว่างกำลังเอนล้ม
        }

        float hingeShift = buildingHeight * 0.4f;
        Vector3 endPos = startPos + new Vector3(
            fallDir.x * hingeShift,
            fallDir.y * hingeShift - 0.15f,
            0f
        );

        float targetAngleZ = 0f;
        Vector3 targetScale = startScale;

        if (fallDir == Vector2.right) targetAngleZ = -90f;
        else if (fallDir == Vector2.left) targetAngleZ = 90f;
        else if (fallDir == Vector2.up) targetScale = new Vector3(startScale.x, startScale.y * 0.45f, 1f);
        else if (fallDir == Vector2.down) targetScale = new Vector3(startScale.x, startScale.y * 0.45f, 1f);

        Quaternion endRot = Quaternion.Euler(0f, 0f, targetAngleZ);

        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fallDuration);
            float curve = t * t;

            visualTransform.localPosition = Vector3.Lerp(startPos, endPos, curve);
            visualTransform.localRotation = Quaternion.Lerp(startRot, endRot, curve);
            visualTransform.localScale = Vector3.Lerp(startScale, targetScale, curve);

            if (!hasTriggeredNextDomino && t >= impactTriggerRatio)
            {
                hasTriggeredNextDomino = true;
                PushNextDomino(fallDir);
            }

            yield return null;
        }

        if (!hasTriggeredNextDomino)
        {
            PushNextDomino(fallDir);
        }

        // 2. จังหวะตึกฟาดพื้น -> เสกกลุ่มควันฝุ่นตึกถล่มคลุมตามแนวที่ล้มลงไป!
        Vector2 slamCenter = (Vector2)transform.position + (fallDir * (buildingHeight * 0.45f));
        SpawnSmokeEffect(collapseDustCloudPrefab, slamCenter, fallDir);

        // ฟาดพื้นสนิทแล้ว -> สั่งให้ตึกนี้ระเบิด คิดคะแนน และดีดเศษซาก
        onSlamAndExplode?.Invoke();

        if (rubbleSprite != null && sr != null)
        {
            sr.sprite = rubbleSprite;
            sr.sortingOrder = originalOrder;
            visualTransform.localPosition = startPos;
            visualTransform.localRotation = Quaternion.identity;
            visualTransform.localScale = startScale;
        }
        else if (destroyIfNoRubble)
        {
            Destroy(gameObject);
        }
    }

    private void SpawnSmokeEffect(GameObject smokePrefab, Vector2 position, Vector2 fallDir)
    {
        if (smokePrefab == null) return;

        // หมุนทิศทางของกล่องพาร์ติเคิลให้วางตัวขนานไปกับทิศที่ตึกล้ม
        float angle = Mathf.Atan2(fallDir.y, fallDir.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0f, 0f, angle);

        GameObject smokeObj = Instantiate(smokePrefab, new Vector3(position.x, position.y, 0f), rot);
        if (smokeDestroyDelay > 0f)
        {
            Destroy(smokeObj, smokeDestroyDelay);
        }
    }

    private void PushNextDomino(Vector2 fallDir)
    {
        Vector2 boxCenter = (Vector2)transform.position + (fallDir * (buildingHeight * 0.55f));
        Vector2 boxSize = (fallDir.x != 0)
            ? new Vector2(buildingHeight, buildingWidth)
            : new Vector2(buildingWidth, buildingHeight);

        Collider2D[] hits = (targetLayer.value == 0)
            ? Physics2D.OverlapBoxAll(boxCenter, boxSize, 0f)
            : Physics2D.OverlapBoxAll(boxCenter, boxSize, 0f, targetLayer);

        foreach (Collider2D hit in hits)
        {
            if (hit.gameObject == this.gameObject) continue;

            ChainExplodable target = hit.GetComponent<ChainExplodable>();
            if (target != null && target.destructibleType != DestructibleType.PlayerBomb)
            {
                target.Explode(transform.position);
            }
        }
    }

    private Vector2 SnapToFourDirections(Vector2 dir)
    {
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return (dir.x > 0) ? Vector2.right : Vector2.left;
        else
            return (dir.y > 0) ? Vector2.up : Vector2.down;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.75f);
        Gizmos.DrawWireCube(transform.position + Vector3.right * (buildingHeight * 0.55f), new Vector3(buildingHeight, buildingWidth, 0));
        Gizmos.DrawWireCube(transform.position + Vector3.left * (buildingHeight * 0.55f), new Vector3(buildingHeight, buildingWidth, 0));
        Gizmos.DrawWireCube(transform.position + Vector3.up * (buildingHeight * 0.55f), new Vector3(buildingWidth, buildingHeight, 0));
        Gizmos.DrawWireCube(transform.position + Vector3.down * (buildingHeight * 0.55f), new Vector3(buildingWidth, buildingHeight, 0));
    }
}