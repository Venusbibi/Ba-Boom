using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BuildingDebris2D : MonoBehaviour
{
    [Header("Physics Settings")]
    public float linearDamping = 2.8f;      // แรงเสียดทานให้ซากค่อยๆ หยุดบนพื้น
    public float angularDamping = 2.0f;     // แรงหน่วงการหมุน
    public float minImpactSpeed = 1.2f;     // ความเร็วขั้นต่ำที่จะทับตึกหรือรถให้พังได้
    public float maxLifeTime = 2.2f;        // เวลาสูงสุดก่อนซากหยุดทำงาน

    [Header("Fake Height Arc (Top-Down)")]
    public float arcPeakScale = 1.35f;      // ขยายขนาดตอนลอยกลางอากาศให้ดูมีมิติ

    private Rigidbody2D rb;
    private Vector3 baseScale;
    private float elapsed = 0f;
    private bool isFinished = false;
    private GameObject ownerObject;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;               // ปิดแรงโน้มถ่วงแกน Y สำหรับ Top-Down
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;

        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;               // ใช้ Trigger เพื่อไม่ให้ซากติดขอบทางเท้าก่อนถึงรถ

        baseScale = transform.localScale;
    }

    public void Launch(Vector2 velocity, float spinSpeed, GameObject owner)
    {
        ownerObject = owner;
        rb.linearVelocity = velocity;
        rb.angularVelocity = spinSpeed;

        // แจ้ง GameResultManager ให้รอจนกว่าซากชิ้นนี้จะตกถึงพื้น
        if (GameResultManager.Instance != null)
        {
            GameResultManager.Instance.RegisterExplosion();
        }
    }

    private void Update()
    {
        if (isFinished) return;

        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / maxLifeTime);

        // จำลองการลอยโค้งในอากาศ (ขยายใหญ่ช่วงกลางทาง แล้วหดกลับเท่าเดิมตอนตกพื้น)
        float heightCurve = Mathf.Sin(progress * Mathf.PI);
        float scaleMultiplier = Mathf.Lerp(1f, arcPeakScale, heightCurve);
        transform.localScale = baseScale * scaleMultiplier;

        // ถ้าซากชะลอจนเกือบหยุดนิ่ง หรือครบเวลาแล้ว ให้จบการทำงาน
        if ((elapsed > 0.2f && rb.linearVelocity.magnitude < 0.35f) || elapsed >= maxLifeTime)
        {
            FinishDebris();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isFinished) return;
        if (other.gameObject == ownerObject) return; // ไม่ชนตึกต้นทางของตัวเอง

        // เช็คว่าซากยังมีความเร็วพอที่จะกระแทกให้พังได้หรือไม่
        if (rb.linearVelocity.magnitude < minImpactSpeed) return;

        ChainExplodable target = other.GetComponent<ChainExplodable>();
        if (target != null && target.destructibleType != DestructibleType.PlayerBomb)
        {
            // สั่งให้ตึกข้างเคียงพังทลาย หรือรถบนถนนระเบิดต่อทันที!
            target.Explode(transform.position);

            // ลดความเร็วซากลงเล็กน้อยหลังกระแทกเป้าหมาย
            rb.linearVelocity *= 0.55f;
        }
    }

    private void FinishDebris()
    {
        if (isFinished) return;
        isFinished = true;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        transform.localScale = baseScale;

        if (GameResultManager.Instance != null)
        {
            GameResultManager.Instance.ExplosionFinished();
        }

        // ค่อยๆ ลบซากออก หรือปล่อยค้างไว้บนฉากก็ได้
        Destroy(gameObject, 0.5f);
    }

    private void OnDestroy()
    {
        // ป้องกันกรณี Object ถูกลบก่อนเรียก FinishDebris
        if (!isFinished && GameResultManager.Instance != null)
        {
            isFinished = true;
            GameResultManager.Instance.ExplosionFinished();
        }
    }
}