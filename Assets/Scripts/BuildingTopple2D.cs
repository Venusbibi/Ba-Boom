using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BuildingTopple2D : MonoBehaviour
{
    [Header("Domino & Collapse Timing (ความเร็วในการล้ม)")]
    public float buildingHeight = 2.4f;
    public float buildingWidth = 1.1f;
    public float fallDuration = 0.85f;
    [Range(0.3f, 0.95f)]
    public float impactTriggerRatio = 0.6f;
    public LayerMask targetLayer;

    [Header("Domino Combo Multiplier (คอมโบตึกพังทับกัน x2 x4 x6...)")]
    [Min(0)]
    public int extraDominoCombo = 1;
    public bool multiplyBuildingScoreByCombo = true;
    public int dominoCrushBonusScore = 150;
    public bool onlyDoubleWhenCrushingNext = false;

    [Header("Floating Combo Popup (ตัวเลขเด้งบนหัวตึก)")]
    public bool showFloatingCombo = true;
    public bool showEarnedScoreOnPopup = true;
    public TMP_FontAsset popupFont;
    [Min(1f)]
    public float popupFontSize = 6.5f;          // ปรับขนาดฟอนต์ตรงนี้ (ยิ่งเยอะยิ่งใหญ่)
    public Color comboPopupColor = new Color(1f, 0.85f, 0.15f, 1f);
    public DominoPopupText2D customPopupPrefab;

    [Header("Domino Tilt Physics (มุมเอียงและแรงสั่น)")]
    [Range(45f, 90f)]
    public float maxTiltAngle = 82f;
    public float shakeIntensity = 0.06f;
    public int midCollapseSmokeCount = 2;

    [Header("Collapse Smoke & Dust Particles (ควันฝุ่นตอนตึกถล่ม)")]
    public GameObject baseCrackSmokePrefab;
    public GameObject collapseDustCloudPrefab;
    public float smokeDestroyDelay = 3.5f;

    [Header("Visuals")]
    public Transform visualTransform;
    public Sprite rubbleSprite;
    public bool destroyIfNoRubble = true;

    private bool isToppling = false;
    private bool didCrushNextTarget = false;
    private Vector2 originWorldPos;

    private static int activeDominoChain = 0;
    private static float lastDominoSlamTime = -10f;

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
        didCrushNextTarget = false;
        originWorldPos = transform.position;

        foreach (var col in GetComponentsInChildren<Collider2D>())
        {
            col.enabled = false;
        }

        Vector2 rawDir = originWorldPos - sourcePosition;
        if (rawDir.sqrMagnitude < 0.001f)
        {
            rawDir = Vector2.right;
        }

        Vector2 fallDir = SnapToFourDirections(rawDir.normalized);
        SpawnSmokeEffect(baseCrackSmokePrefab, originWorldPos, fallDir);

        StartCoroutine(DominoTiltRoutine(rawDir, fallDir, onSlamAndExplode));
    }

    private IEnumerator DominoTiltRoutine(Vector2 rawDir, Vector2 fallDir, Action onSlamAndExplode)
    {
        float elapsed = 0f;
        bool hasTriggeredNextDomino = false;
        int midSmokesSpawned = 0;

        Vector3 startPos = visualTransform.localPosition;
        Quaternion startRot = visualTransform.localRotation;
        Vector3 startScale = visualTransform.localScale;

        SpriteRenderer sr = visualTransform.GetComponent<SpriteRenderer>();
        int originalOrder = 0;
        Color startColor = Color.white;
        Color dustyColor = new Color(0.72f, 0.69f, 0.66f, 1f);

        if (sr != null)
        {
            originalOrder = sr.sortingOrder;
            startColor = sr.color;
            sr.sortingOrder += 5;
        }

        float spriteLocalHeight = (sr != null && sr.sprite != null)
            ? sr.sprite.bounds.size.y * startScale.y
            : buildingHeight * 0.5f;

        Vector3 baseLocalPos = startPos - new Vector3(0f, spriteLocalHeight * 0.5f, 0f);
        Vector3 centerOffsetFromBase = new Vector3(0f, spriteLocalHeight * 0.5f, 0f);

        float targetAngleZ;
        if (fallDir == Vector2.right)
        {
            targetAngleZ = -maxTiltAngle;
        }
        else if (fallDir == Vector2.left)
        {
            targetAngleZ = maxTiltAngle;
        }
        else
        {
            float sideSign = (rawDir.x >= 0f) ? -1f : 1f;
            targetAngleZ = sideSign * (maxTiltAngle * 0.9f);
        }

        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fallDuration);

            float tiltProgress = (t < 0.15f)
                ? Mathf.Lerp(0f, 0.04f, t / 0.15f)
                : Mathf.Lerp(0.04f, 1f, Mathf.Pow((t - 0.15f) / 0.85f, 1.8f));

            float currentAngleZ = Mathf.Lerp(0f, targetAngleZ, tiltProgress);
            Quaternion tiltRot = Quaternion.Euler(0f, 0f, currentAngleZ);
            Vector3 rotatedCenter = tiltRot * centerOffsetFromBase;
            float verticalLeanY = fallDir.y * (spriteLocalHeight * 0.25f) * tiltProgress;

            float shakeFade = 1f - tiltProgress;
            float shakeX = UnityEngine.Random.Range(-shakeIntensity, shakeIntensity) * shakeFade;
            float shakeY = UnityEngine.Random.Range(-shakeIntensity * 0.4f, shakeIntensity * 0.4f) * shakeFade;

            visualTransform.localPosition = baseLocalPos + rotatedCenter + new Vector3(shakeX, verticalLeanY + shakeY, 0f);
            visualTransform.localRotation = startRot * tiltRot;
            visualTransform.localScale = startScale;

            if (sr != null)
            {
                sr.color = Color.Lerp(startColor, dustyColor, tiltProgress);
            }

            if (midCollapseSmokeCount > 0 && midSmokesSpawned < midCollapseSmokeCount)
            {
                float nextSmokeThreshold = (float)(midSmokesSpawned + 1) / (midCollapseSmokeCount + 1);
                if (t >= nextSmokeThreshold)
                {
                    midSmokesSpawned++;
                    GameObject smokeToUse = (baseCrackSmokePrefab != null) ? baseCrackSmokePrefab : collapseDustCloudPrefab;
                    Vector2 midPos = originWorldPos + (fallDir * (buildingHeight * 0.2f * tiltProgress));
                    SpawnSmokeEffect(smokeToUse, midPos, fallDir);
                }
            }

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

        Vector2 slamCenter = originWorldPos + (fallDir * (buildingHeight * 0.4f));
        SpawnSmokeEffect(collapseDustCloudPrefab, slamCenter, fallDir);

        onSlamAndExplode?.Invoke();

        bool grantBonusCombo = !onlyDoubleWhenCrushingNext || didCrushNextTarget;
        int addedMultiplier = 1 + ((grantBonusCombo && extraDominoCombo > 0) ? extraDominoCombo : 0);

        if (Time.time - lastDominoSlamTime > 2.2f)
        {
            activeDominoChain = 0;
        }
        lastDominoSlamTime = Time.time;
        activeDominoChain += addedMultiplier;

        ChainExplodable ce = GetComponent<ChainExplodable>();
        int baseBuildingScore = (ce != null && ce.scoreValue > 0) ? ce.scoreValue : dominoCrushBonusScore;
        int totalMultipliedScore = baseBuildingScore * activeDominoChain;

        int extraScoreToAdd = multiplyBuildingScoreByCombo
            ? Mathf.Max(0, totalMultipliedScore - baseBuildingScore)
            : (dominoCrushBonusScore * Mathf.Max(0, extraDominoCombo));

        if (grantBonusCombo && extraDominoCombo > 0)
        {
            for (int i = 0; i < extraDominoCombo; i++)
            {
                int scoreChunk = (i == 0) ? extraScoreToAdd : 0;

                if (ScoreManager.Instance != null && scoreChunk > 0)
                {
                    ScoreManager.Instance.AddScore(scoreChunk);
                }

                ResultDataManager.RecordDestruction(scoreChunk, DestructibleType.Barrel);
            }
        }

        if (showFloatingCombo)
        {
            int displayedTotalScore = multiplyBuildingScoreByCombo
                ? totalMultipliedScore
                : (baseBuildingScore + extraScoreToAdd);

            string popupLabel = (showEarnedScoreOnPopup && displayedTotalScore > 0)
                ? $"x{activeDominoChain}!\n+{displayedTotalScore}"
                : $"x{activeDominoChain}!";

            Vector2 popupPos = originWorldPos + Vector2.up * 0.65f;
            DominoPopupText2D.Spawn(popupPos, popupLabel, comboPopupColor, customPopupPrefab, popupFont, popupFontSize);
        }

        if (rubbleSprite != null && sr != null)
        {
            sr.sprite = rubbleSprite;
            sr.color = Color.white;
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
        Vector2 boxCenter = originWorldPos + (fallDir * (buildingHeight * 0.55f));
        Vector2 boxSize = (fallDir.x != 0)
            ? new Vector2(buildingHeight, buildingWidth)
            : new Vector2(buildingWidth, buildingHeight);

        Collider2D[] hits = (targetLayer.value == 0)
            ? Physics2D.OverlapBoxAll(boxCenter, boxSize, 0f)
            : Physics2D.OverlapBoxAll(boxCenter, boxSize, 0f, targetLayer);

        HashSet<ChainExplodable> triggeredTargets = new HashSet<ChainExplodable>();

        foreach (Collider2D hit in hits)
        {
            if (hit.gameObject == this.gameObject) continue;

            ChainExplodable target = hit.GetComponentInParent<ChainExplodable>();
            if (target != null && target.gameObject != this.gameObject && target.destructibleType != DestructibleType.PlayerBomb)
            {
                if (triggeredTargets.Add(target))
                {
                    didCrushNextTarget = true;
                    target.Explode(originWorldPos);
                }
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