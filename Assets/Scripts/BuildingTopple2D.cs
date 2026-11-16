using System;
using System.Collections;
using UnityEngine;

public class BuildingTopple2D : MonoBehaviour
{
    [Header("Topple Settings")]
    public float buildingHeight = 2.5f;     // ความยาวตอนตึกล้มนอน (ระยะทับ)
    public float buildingWidth = 1.2f;      // ความกว้างของแนวทับ
    public float fallDuration = 0.35f;      // เวลาที่ใช้ในการล้มจนฟาดพื้น
    public LayerMask targetLayer;           // เลือก Layer เป้าหมาย (ปล่อย Nothing = เช็กทุก Layer)

    [Header("Impact Debris (เศษซากกระเด็นตอนตึกฟาดพื้น)")]
    public bool spawnDebrisOnImpact = true; // ดีดซากตึกเพิ่มจังหวะฟาดพื้น
    public int impactDebrisCount = 3;       // จำนวนชิ้นที่กระเด็นตอนฟาดพื้น

    [Header("Visuals")]
    public Transform visualTransform;       // ปล่อยว่างได้ (ใช้ตัวตึกเองอัตโนมัติ)
    public Sprite rubbleSprite;             // รูปซากตึกหลังล้ม (ถ้ามี)
    public bool destroyIfNoRubble = true;   // ถ้าไม่มีรูปซากตึก ให้ลบออกหลังล้มเสร็จ

    private bool isToppling = false;
    private ChainExplodable chainExplodable;

    private void Awake()
    {
        if (visualTransform == null)
        {
            visualTransform = transform;
        }

        chainExplodable = GetComponent<ChainExplodable>();
    }

    public void StartTopple(Vector2 sourcePosition, Action onToppleFinished)
    {
        if (isToppling)
        {
            onToppleFinished?.Invoke();
            return;
        }

        isToppling = true;

        // ปิด Collider ของตึกที่กำลังล้ม เพื่อไม่ให้ขวางทางเศษซากหรือโดนเช็กซ้ำ
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Vector2 rawDir = (Vector2)transform.position - sourcePosition;
        if (rawDir.sqrMagnitude < 0.001f)
        {
            rawDir = UnityEngine.Random.insideUnitCircle.normalized;
        }

        Vector2 fallDir = SnapToFourDirections(rawDir.normalized);
        StartCoroutine(ToppleRoutine(fallDir, onToppleFinished));
    }

    private IEnumerator ToppleRoutine(Vector2 fallDir, Action onToppleFinished)
    {
        float elapsed = 0f;
        Vector3 startPos = visualTransform.localPosition;
        Quaternion startRot = visualTransform.localRotation;
        Vector3 startScale = visualTransform.localScale;

        SpriteRenderer sr = visualTransform.GetComponent<SpriteRenderer>();
        bool isCenterPivot = sr != null && sr.sprite != null &&
                             Mathf.Abs(sr.sprite.pivot.y - sr.sprite.rect.height * 0.5f) < 2f;

        float halfShift = isCenterPivot ? (buildingHeight * 0.35f) : 0f;
        Vector3 endPos = startPos + new Vector3(
            fallDir.x * halfShift,
            fallDir.y * halfShift - (isCenterPivot ? 0.2f : 0f),
            0f
        );

        float targetAngleZ = 0f;
        Vector3 targetScale = startScale;

        if (fallDir == Vector2.right) targetAngleZ = -90f;
        else if (fallDir == Vector2.left) targetAngleZ = 90f;
        else if (fallDir == Vector2.up) targetScale = new Vector3(startScale.x, startScale.y * 0.45f, 1f);
        else if (fallDir == Vector2.down) targetScale = new Vector3(startScale.x, startScale.y * 0.45f, 1f);

        Quaternion endRot = Quaternion.Euler(0, 0, targetAngleZ);

        // ค่อยๆ เอียงตึกลงพื้น
        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fallDuration);
            float curve = t * t;

            visualTransform.localPosition = Vector3.Lerp(startPos, endPos, curve);
            visualTransform.localRotation = Quaternion.Lerp(startRot, endRot, curve);
            visualTransform.localScale = Vector3.Lerp(startScale, targetScale, curve);
            yield return null;
        }

        // จังหวะตึกฟาดพื้น: ทับเป้าหมายใต้ตึก + ดีดเศษซากตึกกระเด็นต่อ
        CheckCrushImpact(fallDir);

        if (rubbleSprite != null && sr != null)
        {
            sr.sprite = rubbleSprite;
            visualTransform.localPosition = startPos;
            visualTransform.localRotation = Quaternion.identity;
            visualTransform.localScale = startScale;
            onToppleFinished?.Invoke();
        }
        else if (destroyIfNoRubble)
        {
            yield return new WaitForSeconds(0.25f);
            onToppleFinished?.Invoke();
            Destroy(gameObject);
        }
        else
        {
            onToppleFinished?.Invoke();
        }
    }

    private void CheckCrushImpact(Vector2 fallDir)
    {
        Vector2 boxCenter = (Vector2)transform.position + (fallDir * (buildingHeight * 0.5f));
        Vector2 boxSize = (fallDir.x != 0)
            ? new Vector2(buildingHeight, buildingWidth)
            : new Vector2(buildingWidth, buildingHeight);

        // 1. ทับตึกข้างเคียงและรถที่อยู่ใต้แนวตึกล้ม
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

        // 2. ดีดเศษซากฟิสิกส์เพิ่มตรงจุดที่ตึกฟาดพื้น เพื่อให้กระเด็นต่อไปโดนรถไกลๆ ได้
        if (spawnDebrisOnImpact && chainExplodable != null)
        {
            chainExplodable.SpawnDebrisBurst(boxCenter, fallDir, impactDebrisCount);
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
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.7f);
        Gizmos.DrawWireCube(transform.position + Vector3.right * (buildingHeight * 0.5f), new Vector3(buildingHeight, buildingWidth, 0));
        Gizmos.DrawWireCube(transform.position + Vector3.left * (buildingHeight * 0.5f), new Vector3(buildingHeight, buildingWidth, 0));
        Gizmos.DrawWireCube(transform.position + Vector3.up * (buildingHeight * 0.5f), new Vector3(buildingWidth, buildingHeight, 0));
        Gizmos.DrawWireCube(transform.position + Vector3.down * (buildingHeight * 0.5f), new Vector3(buildingWidth, buildingHeight, 0));
    }
}