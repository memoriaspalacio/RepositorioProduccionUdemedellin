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

        // Guarda el nombre del trigger usado para ejecutar el ataque final.
        [SerializeField] private string attackTrigger = "onAttack";

        // Define el tiempo inicial antes de comenzar a expandir el ataque.
        [SerializeField] private float preparationTime = 0.5f;

        // Define el tiempo entre cada nueva expansion de la cruz.
        [SerializeField] private float expansionInterval = 0.3f;

        // Define el tiempo minimo entre ataques completos.
        [SerializeField] private float attackCooldown = 2f;

        // Define cuantas celdas alcanza cada brazo del ataque en cruz.
        [Min(1)]
        [SerializeField] private int attackRange = 3;

        // Define el dano pequeno realizado por cada quemadura.
        [SerializeField] private float burnDamage = 0.5f;

        // Define cada cuanto tiempo puede aplicarse el dano de quemadura.
        [SerializeField] private float burnInterval = 0.25f;

        // Define el dano realizado cuando la cruz alcanza su rango maximo.
        [SerializeField] private float finalAttackDamage = 5f;

        // Guarda la referencia al componente de vida del jugador.
        [SerializeField] private PlayerHealth playerHealth;

        // Guarda el prefab usado para mostrar cada celda del ataque.
        [SerializeField] private GameObject attackCellPrefab;

        // Define la altura visual de las marcas respecto al suelo.
        [SerializeField] private float indicatorHeight = 0.05f;

        // Indica si actualmente existe un ataque en preparacion.
        private bool isPreparingAttack = false;

        // Guarda el proximo instante en el que la monja puede atacar.
        private float nextAttackTime = 0f;

        // Guarda el proximo instante permitido para realizar dano de quemadura.
        private float nextBurnTime = 0f;

        // Guarda el alcance que actualmente tiene la cruz.
        private int currentAttackRange = 0;

        // Guarda todas las marcas visuales creadas para el ataque actual.
        private readonly List<GameObject> activeIndicators =
            new List<GameObject>();

        // Guarda las celdas que actualmente forman parte del ataque.
        private readonly HashSet<Vector2Int> activeAttackCells =
            new HashSet<Vector2Int>();

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

        // Detecta al jugador y prepara un ataque expansivo sin perseguirlo.
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

        // Prepara la cruz, la expande progresivamente y ejecuta el golpe final.
        private IEnumerator PrepareCrossAttack(GameObject target)
        {
            isPreparingAttack = true;
            currentAttackRange = 0;
            nextBurnTime = Time.time;

            ClearCrossArea();

            StopMovementFor(
                preparationTime +
                expansionInterval * attackRange +
                0.2f
            );

            if (animator != null)
                animator.SetTrigger(reloadTrigger);

            yield return new WaitForSeconds(
                preparationTime
            );

            for (int distance = 1;
                 distance <= attackRange;
                 distance++)
            {
                if (isDead)
                {
                    ClearCrossArea();
                    isPreparingAttack = false;
                    yield break;
                }

                currentAttackRange = distance;

                ExpandCrossArea(
                    distance
                );

                float expansionEndTime =
                    Time.time + expansionInterval;

                while (Time.time < expansionEndTime)
                {
                    ApplyBurnDamage(target);

                    yield return null;
                }
            }

            if (isDead)
            {
                ClearCrossArea();
                isPreparingAttack = false;
                yield break;
            }

            if (animator != null)
                animator.SetTrigger(attackTrigger);

            ExecuteFinalAttack(target);

            ClearCrossArea();

            nextAttackTime =
                Time.time + attackCooldown;

            isPreparingAttack = false;
        }

        // Agrega las cuatro nuevas celdas correspondientes al alcance indicado.
        private void ExpandCrossArea(int distance)
        {
            Vector2Int centerCell =
                CurrentGridCell;

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

        // Aplica dano pequeno si el jugador permanece sobre una celda activa.
        private void ApplyBurnDamage(GameObject target)
        {
            if (target == null)
                return;

            if (Time.time < nextBurnTime)
                return;

            Vector2Int playerCell =
                Grid.WorldToCell(
                    target.transform.position
                );

            if (!activeAttackCells.Contains(playerCell))
                return;

            nextBurnTime =
                Time.time + burnInterval;

            if (playerHealth != null)
                playerHealth.Damage(burnDamage);
        }

        // Ejecuta el dano fuerte si el jugador permanece dentro de la cruz completa.
        private void ExecuteFinalAttack(GameObject target)
        {
            if (target == null)
                return;

            Vector2Int playerCell =
                Grid.WorldToCell(
                    target.transform.position
                );

            if (!activeAttackCells.Contains(playerCell))
            {
                Debug.Log(
                    $"El jugador esquivo el ataque final: {playerCell}.",
                    this
                );

                return;
            }

            Debug.Log(
                $"La monja golpeo al jugador con el ataque final en {playerCell}.",
                this
            );

            if (playerHealth != null)
                playerHealth.Damage(finalAttackDamage);
        }

        // Crea una marca visual y registra su celda como parte del ataque activo.
        private void CreateAttackIndicator(Vector2Int cell)
        {
            if (!Grid.IsCellInside(cell))
                return;

            if (activeAttackCells.Contains(cell))
                return;

            activeAttackCells.Add(cell);

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
                    Quaternion.identity,
                    transform
                );

            activeIndicators.Add(indicator);
        }

        // Elimina todas las marcas visuales y limpia las celdas activas.
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
            activeAttackCells.Clear();
            currentAttackRange = 0;
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