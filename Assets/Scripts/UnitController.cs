using UnityEngine;

namespace WW2_Commander
{
    /// <summary>
    /// 单位控制器 - 基础单位行为
    /// </summary>
    public class UnitController : MonoBehaviour
    {
        [Header("单位属性")]
        [SerializeField] private string unitName = "Soldier";
        [SerializeField] private int health = 100;
        [SerializeField] private int damage = 10;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float attackRange = 2f;

        [Header("状态")]
        [SerializeField] private bool isSelected = false;
        [SerializeField] private Transform target;

        public string UnitName => unitName;
        public int Health => health;
        public int Damage => damage;
        public bool IsSelected => isSelected;

        private Rigidbody2D rb;
        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>
        /// 选择单位
        /// </summary>
        public void Select()
        {
            isSelected = true;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.yellow;
            }
        }

        /// <summary>
        /// 取消选择
        /// </summary>
        public void Deselect()
        {
            isSelected = false;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }
        }

        /// <summary>
        /// 移动到目标位置
        /// </summary>
        public void MoveTo(Vector2 position)
        {
            if (rb != null)
            {
                Vector2 direction = (position - (Vector2)transform.position).normalized;
                rb.velocity = direction * moveSpeed;
            }
        }

        /// <summary>
        /// 攻击目标
        /// </summary>
        public void Attack(UnitController targetUnit)
        {
            if (targetUnit != null)
            {
                float distance = Vector2.Distance(transform.position, targetUnit.transform.position);
                if (distance <= attackRange)
                {
                    targetUnit.TakeDamage(damage);
                }
            }
        }

        /// <summary>
        /// 受到伤害
        /// </summary>
        public void TakeDamage(int amount)
        {
            health = Mathf.Max(0, health - amount);
            if (health <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// 单位死亡
        /// </summary>
        private void Die()
        {
            Debug.Log($"{unitName} 已被消灭");
            Destroy(gameObject);
        }
    }
}
