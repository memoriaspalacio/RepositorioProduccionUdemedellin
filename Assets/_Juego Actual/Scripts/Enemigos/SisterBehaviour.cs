using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UdeM.Characters
{
    public class SisterBehaviour : Character3DNavMeshGridNPCBehaviour
    {
        // Indica si la monja se encuentra muerta.
        private bool isDead = false;

        // Guarda el Animator usado por las animaciones de la monja.
        [SerializeField] private Animator animator;

        // Guarda el nombre del trigger usado para preparar el ataque.
        [SerializeField] private string reloadTrigger = "onReload";

        // Guarda el nombre del trigger usado para ejecutar el ataque.
        [SerializeField] private string attackTrigger = "onAttack";

        // Define el tiempo que tarda la monja en ejecutar el ataque.
        [SerializeField] private float preparationTime = 0.75f;

        // Define el tiempo minimo entre ataques consecutivos.
        [SerializeField] private float attackCooldown = 2f;

        // Define cuantas celdas alcanza cada brazo del ataque en cruz.
        [Min(1)]
        [SerializeField] private int attackRange = 3;

        // Define el dano realizado por el ataque de la monja.
        [SerializeField] private float attackDamage = 3f;

        // Guarda la referencia al componente de vida del jugador.
        [SerializeField] private PlayerHealth playerHealth;

        // Guarda el prefab usado para mostrar cada celda del ataque.
        [SerializeField] private GameObject attackCellPrefab;

        // Define la altura visual de las marcas respecto al centro de la celda.
        [SerializeField] private float indicatorHeight = 0.05f;

        // Indica si actualmente existe un ataque en preparacion.
        private bool isPreparingAttack = false;

        // Guarda el proximo instante en el que la monja puede atacar.
        private float nextAttackTime = 0f;

        // Guarda todas las marcas visuales creadas para el ataque actual.
        private readonly List<GameObject> activeIndicators =
            new List<GameObject>();

        // Busca automaticamente el Animator si no fue asignado en el Inspector.
        protected override void Start()
        {
            base.Start();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        // Detiene el comportamiento de la monja cuando muere.
        public void SetDead()
        {
            if (isDead)
                return;

            isDead = true;
            isPreparingAttack = false;
            behaviourEnabled = false;

            StopAllCoroutines();
            ClearCrossArea();
        }

        // Detecta al jugador y prepara un ataque en cruz sin perseguirlo.
        public override void PlayerDetected(GameObject detectedTarget)
        {
            if (isDead)
                return;

            if (detectedTarget == null)
                return;

            if (isPreparingAttack)
                return;

            if (Time.time < nextAttackTime)
                return;

            StartCoroutine(
                PrepareCrossAttack(detectedTarget)
            );
        }

        // Prepara el ataque, muestra la cruz y posteriormente ejecuta el dano.
        private IEnumerator PrepareCrossAttack(GameObject target)
        {
            isPreparingAttack = true;

            StopMovementFor(
                preparationTime + 0.1f
            );

            if (animator != null)
                animator.SetTrigger(reloadTrigger);

            ShowCrossArea();

            yield return new WaitForSeconds(
                preparationTime
            );

            if (isDead)
            {
                ClearCrossArea();
                isPreparingAttack = false;
                yield break;
            }

            if (animator != null)
                animator.SetTrigger(attackTrigger);

            ExecuteCrossAttack(target);

            ClearCrossArea();

            nextAttackTime =
                Time.time + attackCooldown;

            isPreparingAttack = false;
        }

        // Comprueba si el jugador se encuentra dentro de las celdas del ataque en cruz.
        private void ExecuteCrossAttack(GameObject target)
        {
            if (target == null)
                return;

            Vector2Int sisterCell =
                CurrentGridCell;

            Vector2Int playerCell =
                Grid.WorldToCell(
                    target.transform.position
                );

            Vector2Int difference =
                playerCell - sisterCell;

            bool isHorizontal =
                difference.y == 0 &&
                Mathf.Abs(difference.x) >= 1 &&
                Mathf.Abs(difference.x) <= attackRange;

            bool isVertical =
                difference.x == 0 &&
                Mathf.Abs(difference.y) >= 1 &&
                Mathf.Abs(difference.y) <= attackRange;

            if (!isHorizontal && !isVertical)
            {
                Debug.Log(
                    $"El jugador esquivo el ataque en cruz: {playerCell}.",
                    this
                );

                return;
            }

            Debug.Log(
                $"La monja ataco al jugador en la celda {playerCell}.",
                this
            );

            if (playerHealth != null)
                playerHealth.Damage(attackDamage);
        }

        // Crea visualmente las marcas de las celdas que forman la cruz.
        private void ShowCrossArea()
        {
            ClearCrossArea();

            Vector2Int centerCell =
                CurrentGridCell;

            for (int distance = 1;
                 distance <= attackRange;
                 distance++)
            {
                CreateAttackIndicator(
                    centerCell +
                    new Vector2Int(distance, 0)
                );

                CreateAttackIndicator(
                    centerCell +
                    new Vector2Int(-distance, 0)
                );

                CreateAttackIndicator(
                    centerCell +
                    new Vector2Int(0, distance)
                );

                CreateAttackIndicator(
                    centerCell +
                    new Vector2Int(0, -distance)
                );
            }
        }

        // Crea una marca visual en el centro de una celda de ataque.
        private void CreateAttackIndicator(Vector2Int cell)
        {
            if (attackCellPrefab == null)
                return;

            Vector3 worldPosition =
                Grid.CellToWorldCenter(
                    cell,
                    0f
                );

            worldPosition.y += indicatorHeight;

            GameObject indicator =
                Instantiate(
                    attackCellPrefab,
                    worldPosition,
                    Quaternion.identity
                );

            activeIndicators.Add(indicator);
        }

        // Elimina todas las marcas visuales creadas por el ataque actual.
        private void ClearCrossArea()
        {
            for (int i = 0;
                 i < activeIndicators.Count;
                 i++)
            {
                if (activeIndicators[i] != null)
                    Destroy(activeIndicators[i]);
            }

            activeIndicators.Clear();
        }

        // Desactiva el ataque normal de una celda usado por la clase base.
        protected override bool Attack(
            Vector2Int direction,
            Vector2Int targetCell)
        {
            return false;
        }
    }
}

