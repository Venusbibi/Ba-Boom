using System.Collections;
using UnityEngine;

public class ChainExplodable : MonoBehaviour
{
    [Header("Explosion Settings")]
    public float explosionRadius = 0.7f;
    public float explosionDelay = 0.2f;

    [Header("Physics Debris (ซากตึก / ชิ้นส่วนรถกระเด็น)")]
    public BuildingDebris2D[] debrisPrefabs;
    public int debrisCount = 4;
    public float debrisMinSpeed = 5f;
    public float debrisMaxSpeed = 9f;

    [Header("Score Settings")]
    public int scoreValue = 100;
    public DestructibleType destructibleType = DestructibleType.Barrel;

    [Header("Visual Effects")]
    public GameObject explosionEffectPrefab;
    public Sprite damagedSprite;

    private bool hasExploded = false;

    public void Explode()
    {
        Explode(transform.position);
    }

    public void Explode(Vector2 sourcePosition)
    {
        // ป้องกันไม่ให้เผลอทำลายตัวแม่ของฉาก
        if (gameObject.name == "City Prop")
            return;

        if (hasExploded)
            return;

        hasExploded = true;

        if (GameResultManager.Instance != null)
        {
            GameResultManager.Instance.RegisterExplosion();
        }

        // เช็คว่าเป็นตึกที่มีระบบล้มแบบโดมิโน่หรือไม่
        BuildingTopple2D topple = GetComponent<BuildingTopple2D>();
        if (topple != null)
        {
            // ล้มแบบโดมิโน่ก่อน -> พอฟาดพื้นสนิทค่อยสั่งระเบิดและคิดคะแนน
            topple.StartTopple(sourcePosition, () =>
            {
                TriggerBlastEffectsAndScore(sourcePosition, useAreaBlast: false);

                if (GameResultManager.Instance != null)
                {
                    GameResultManager.Instance.ExplosionFinished();
                }
            });
            return;
        }

        // กรณีเป็นระเบิดผู้เล่น รถ หรือถังน้ำมัน ให้ทำงานตามปกติ
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (damagedSprite != null && sr != null)
        {
            sr.sprite = damagedSprite;
        }

        StartCoroutine(ExplosionRoutine(sourcePosition));
    }

    private IEnumerator ExplosionRoutine(Vector2 sourcePosition)
    {
        yield return new WaitForSeconds(explosionDelay);

        TriggerBlastEffectsAndScore(sourcePosition, useAreaBlast: true);

        CarBounce2D carBounce = GetComponent<CarBounce2D>();
        if (carBounce != null)
        {
            carBounce.StartBounce(sourcePosition, () =>
            {
                if (GameResultManager.Instance != null)
                {
                    GameResultManager.Instance.ExplosionFinished();
                }
            });
        }
        else
        {
            if (GameResultManager.Instance != null)
            {
                GameResultManager.Instance.ExplosionFinished();
            }

            Destroy(gameObject);
        }
    }

    private void TriggerBlastEffectsAndScore(Vector2 sourcePosition, bool useAreaBlast)
    {
        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
        }

        if (BaBoomAudioManager.Instance != null)
        {
            BaBoomAudioManager.Instance.PlayExplosion();
        }

        // ระเบิดรอบตัว (ใช้เฉพาะตอนวางระเบิดลูกแรกหรือรถ/ถังระเบิด ส่วนตึกจะส่งแรงตามทิศทางล้มแทน)
        if (useAreaBlast)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
            foreach (Collider2D hit in hits)
            {
                ChainExplodable target = hit.GetComponent<ChainExplodable>();
                if (target == null || target == this)
                    continue;

                float distance = Vector2.Distance(transform.position, target.transform.position);
                if (distance <= explosionRadius)
                {
                    target.Explode(transform.position);
                }
            }
        }

        // ดีดเศษซากฟิสิกส์ออกจากจุดระเบิด
        Vector2 blastDir = ((Vector2)transform.position - sourcePosition).normalized;
        SpawnDebrisBurst(transform.position, blastDir, debrisCount);

        // บันทึกคะแนน สถิติ และ Combo
        if (destructibleType != DestructibleType.PlayerBomb)
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScore(scoreValue);
            }

            ResultDataManager.RecordDestruction(scoreValue, destructibleType);

            if (scoreValue > 0 && BaBoomAudioManager.Instance != null)
            {
                BaBoomAudioManager.Instance.PlayScore();
            }
        }
    }

    public void SpawnDebrisBurst(Vector2 origin, Vector2 preferredDir, int count)
    {
        if (debrisPrefabs == null || debrisPrefabs.Length == 0 || count <= 0)
            return;

        bool hasDirection = preferredDir.sqrMagnitude > 0.01f;

        for (int i = 0; i < count; i++)
        {
            BuildingDebris2D prefab = debrisPrefabs[Random.Range(0, debrisPrefabs.Length)];
            if (prefab == null) continue;

            float baseAngle = hasDirection
                ? Mathf.Atan2(preferredDir.y, preferredDir.x) * Mathf.Rad2Deg
                : (360f / count) * i;

            float randomSpread = hasDirection ? Random.Range(-70f, 70f) : Random.Range(-25f, 25f);
            float finalAngleRad = (baseAngle + randomSpread) * Mathf.Deg2Rad;

            Vector2 launchDir = new Vector2(Mathf.Cos(finalAngleRad), Mathf.Sin(finalAngleRad));
            float speed = Random.Range(debrisMinSpeed, debrisMaxSpeed);
            float spin = Random.Range(-360f, 360f);

            BuildingDebris2D debris = Instantiate(prefab, origin, Quaternion.identity);
            debris.Launch(launchDir * speed, spin, gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}