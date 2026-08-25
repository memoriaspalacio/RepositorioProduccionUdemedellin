using System.Collections;
using UnityEngine;

namespace UdeM.Characters
{
    public class SkeletonBehaviour
        : Character3DNavMeshGridNPCBehaviour
    {
        private bool isDead = false;

        [SerializeField] private Animator animator;
        private float hitValue;
        private float hitEnemy;


        [SerializeField]
        private string reloadTrigger =
            "onReload";

        [SerializeField]
        private string attackTrigger =
            "onAttack";

        [SerializeField] private float reloadTime = 0.5f;

        private bool isPreparingAttack;

        public PlayerHealth playerHealth;

        protected override void Start()
        {
            base.Start();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        public void SetDead()
        {
            if (isDead)
                return;

            isDead = true;
            isPreparingAttack = false;
            behaviourEnabled = false;

            StopAllCoroutines();
        }

        private float HitFunction()
        {
            hitValue = Random.Range(1, 7);

            return hitValue;
        }

        protected override bool Attack(
            Vector2Int direction,
            Vector2Int targetCell)
        {
            if (isDead)
                return false;

            if (isPreparingAttack)
                return false;

            StartCoroutine(
                PrepareAndAttack(direction, targetCell)
            );

            return true;
        }

        private IEnumerator PrepareAndAttack(
            Vector2Int direction,
            Vector2Int targetCell)
        {

            if (isDead)
                yield break;

            isPreparingAttack = true;

            FaceGridDirection(direction);

            StopMovementFor(2.10f);

            if (animator != null)
                animator.SetTrigger(reloadTrigger);
                StopMovementFor(2.10f);

            yield return new WaitForSeconds(reloadTime);

            if (CurrentTarget != null)
            {
                Vector2Int currentTargetCell =
                    Grid.WorldToCell(
                        CurrentTarget.transform.position
                    );

                Vector2Int difference =
                    currentTargetCell - CurrentGridCell;

                int distance =
                    Mathf.Abs(difference.x) +
                    Mathf.Abs(difference.y);

                if (distance == 1)
                {
                    FaceGridDirection(difference);

                    if (animator != null)
                        animator.SetTrigger(attackTrigger);

                    Debug.Log(
                        $"El esqueleto atac� la celda " +
                        $"{currentTargetCell}.",
                        this
                    );
                    hitEnemy = HitFunction();
                    playerHealth.Damage(hitEnemy);
                    


                }
            }

            isPreparingAttack = false;
        }
    }
}