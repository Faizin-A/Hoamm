using UnityEngine;

public class EnemyAnimationEventReceiver : MonoBehaviour
{
    public EnemyAI enemyAI;

    public void AnimationAttackHit()
    {
        if (enemyAI != null)
        {
            enemyAI.AnimationAttackHit();
        }
        else
        {
            Debug.LogWarning("EnemyAI belum dihubungkan!");
        }
    }
}